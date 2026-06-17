using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LLama;
using LLama.Common;
using LLama.Native;
using Microsoft.Extensions.Logging;
using MangaFlow.Application.DTOs;
using MangaFlow.Application.Interfaces;

namespace MangaFlow.AI;

public sealed class LlamaCppTranslationProvider : ITranslationProvider, IDisposable
{
    private readonly ILogger<LlamaCppTranslationProvider> _logger;
    private readonly TranslationCache _cache = new();

    private LLamaWeights? _model;
    private LLamaContext? _context;
    private StatelessExecutor? _executor;
    private readonly SemaphoreSlim _inferLock = new(1, 1);

    private bool _isLoaded;
    private string _loadedModelPath = string.Empty;
    private int _loadedContextSize = 2048;
    private int _loadedGpuLayers;

    public string Name => "LlamaCpp";
    public bool IsModelLoaded => _isLoaded;
    public int CacheHits { get; private set; }
    public int CacheMisses { get; private set; }

    /// <summary>
    /// Set after a load when GPU was requested but the CUDA runtime is missing, so the
    /// model fell back to CPU. Null when no GPU issue. The UI surfaces this to the user.
    /// </summary>
    public string? GpuWarning { get; private set; }

    /// <summary>Context window the currently loaded model was created with.</summary>
    public int ActiveContextSize => _loadedContextSize;

    /// <summary>Human-readable backend the model is running on, for diagnostics UI.</summary>
    public string ActiveBackend => !_isLoaded
        ? "Not loaded"
        : _loadedGpuLayers > 0 ? $"GPU ({_loadedGpuLayers} layers)" : "CPU";

    /// <summary>
    /// Probes whether the NVIDIA CUDA 12 runtime libraries that ggml-cuda.dll depends on
    /// (cudart64_12 + cublas64_12) can actually be loaded on this machine. The bundled
    /// backend does NOT ship these — they come with the NVIDIA driver / CUDA Toolkit.
    /// Returns a reason string when something is missing, or null when CUDA is usable.
    /// </summary>
    public static string? DiagnoseCudaRuntime()
    {
        // Each entry: friendly name -> the DLL ggml-cuda links against at runtime.
        var required = new[] { "cudart64_12", "cublas64_12", "cublasLt64_12" };
        foreach (var dll in required)
        {
            var handle = NativeMethods.LoadLibrary(dll + ".dll");
            if (handle == IntPtr.Zero)
            {
                return $"NVIDIA CUDA 12 runtime not found ('{dll}.dll' could not be loaded). " +
                       "Install a recent NVIDIA driver or the CUDA 12 Toolkit redistributable, " +
                       "otherwise translation runs on CPU.";
            }
            NativeMethods.FreeLibrary(handle);
        }
        return null;
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("kernel32", CharSet = System.Runtime.InteropServices.CharSet.Ansi, SetLastError = true)]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [System.Runtime.InteropServices.DllImport("kernel32", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool FreeLibrary(IntPtr hModule);
    }

    // NativeLibraryConfig must be set exactly once, before any llama.cpp native call.
    private static int _nativeConfigured;

    public LlamaCppTranslationProvider(ILogger<LlamaCppTranslationProvider> logger)
    {
        _logger = logger;
        ConfigureNativeLibrary();
    }

    private void ConfigureNativeLibrary()
    {
        // Guard: run once per process. Interlocked makes it thread-safe even if two
        // providers were ever constructed concurrently.
        if (Interlocked.Exchange(ref _nativeConfigured, 1) != 0)
            return;

        try
        {
            // Auto-fallback: try CUDA first, gracefully fall back to the CPU build when
            // there's no NVIDIA GPU / CUDA runtime on the target machine.
            NativeLibraryConfig.All
                .WithAutoFallback(true)
                .WithLogCallback((level, message) =>
                {
                    if (level <= LLamaLogLevel.Warning)
                        LogToFile($"[native:{level}] {message?.TrimEnd()}");
                });
            LogToFile("NativeLibraryConfig: auto-fallback enabled (CUDA → CPU).");
        }
        catch (Exception ex)
        {
            // If native config was already locked (e.g. a prior load), don't crash —
            // the previously selected backend stays in effect.
            LogToFile($"NativeLibraryConfig setup skipped: {ex.Message}");
        }
    }

    public async Task EnsureModelLoadedAsync(
        string modelPath,
        int cpuThreads = 8,
        bool useGpu = false,
        float temperature = 0.3f,
        int gpuLayers = 99,
        int contextSize = 2048)
    {
        // Reload when any load-time parameter changed, not just the path.
        if (_isLoaded && _loadedModelPath == modelPath
            && _loadedContextSize == contextSize
            && _loadedGpuLayers == (useGpu ? gpuLayers : 0))
            return;

        await _inferLock.WaitAsync();
        try
        {
            if (_isLoaded && _loadedModelPath == modelPath
                && _loadedContextSize == contextSize
                && _loadedGpuLayers == (useGpu ? gpuLayers : 0))
                return;

            await UnloadAsync();

            _logger.LogInformation("Loading GGUF model from: {Path}", modelPath);
            var sw = Stopwatch.StartNew();

            // Clamp threads to a sensible range; default to physical cores when unset.
            int threads = cpuThreads > 0 ? cpuThreads : Math.Max(1, Environment.ProcessorCount / 2);
            int ctx = contextSize > 0 ? contextSize : 2048;
            int gpu = useGpu ? Math.Max(0, gpuLayers) : 0;

            // If the user asked for GPU, verify the CUDA runtime is actually present.
            // Missing runtime -> force CPU and record a warning the UI can show, instead of
            // silently falling back and leaving the user wondering why GPU "didn't help".
            GpuWarning = null;
            if (gpu > 0)
            {
                var cudaIssue = DiagnoseCudaRuntime();
                if (cudaIssue != null)
                {
                    GpuWarning = cudaIssue;
                    _logger.LogWarning("GPU requested but unavailable: {Reason}", cudaIssue);
                    LogToFile($"GPU requested but unavailable, falling back to CPU: {cudaIssue}");
                    gpu = 0; // fall back to CPU explicitly
                }
            }

            var parameters = new ModelParams(modelPath)
            {
                ContextSize = (uint)ctx,
                GpuLayerCount = gpu,
                Threads = threads,
                BatchSize = 512,
            };
            _logger.LogInformation("Loading model | threads={Threads} | context={Ctx} | gpuLayers={Gpu}", threads, ctx, gpu);
            LogToFile($"Loading model | threads={threads} | context={ctx} | gpuLayers={gpu} | useGpu={useGpu}");

            _model = await LLamaWeights.LoadFromFileAsync(parameters);
            _context = _model.CreateContext(parameters);
            _executor = new StatelessExecutor(_model, parameters);

            _isLoaded = true;
            _loadedModelPath = modelPath;
            _loadedContextSize = ctx;
            _loadedGpuLayers = gpu;
            sw.Stop();

            _logger.LogInformation("Model loaded in {Elapsed}ms", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load model: {Path}", modelPath);
            _isLoaded = false;
            throw;
        }
        finally
        {
            _inferLock.Release();
        }
    }

    public async Task<TranslationResult> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        TranslationContext context)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TranslationResult { TranslatedText = string.Empty, ProviderName = Name, IsSuccess = true };
        }

        if (!_isLoaded || _executor == null)
        {
            return new TranslationResult
            {
                TranslatedText = string.Empty,
                ProviderName = Name,
                IsSuccess = false,
                ErrorMessage = "LLM model not loaded. Set the model path in Settings."
            };
        }

        // Cache lookup
        if (_cache.TryGet(text, out var cached))
        {
            CacheHits++;
            _logger.LogDebug("Cache hit for text (len={Len})", text.Length);
            return new TranslationResult
            {
                TranslatedText = cached,
                ProviderName = Name + " [cache]",
                IsSuccess = true,
                ElapsedMilliseconds = 0
            };
        }

        CacheMisses++;
        var sw = Stopwatch.StartNew();

        await _inferLock.WaitAsync();
        try
        {
            var userPrompt = TranslationPromptBuilder.BuildUserPrompt(text, context);
            var fullPrompt = $"<|im_start|>system\n{TranslationPromptBuilder.SystemPrompt}<|im_end|>\n" +
                             $"<|im_start|>user\n{userPrompt}<|im_end|>\n" +
                             $"<|im_start|>assistant\n";

            var inferenceParams = new InferenceParams
            {
                MaxTokens = 512,
                AntiPrompts = ["<|im_end|>", "<|im_start|>", "<|endoftext|>"],
                SamplingPipeline = new LLama.Sampling.DefaultSamplingPipeline
                {
                    Temperature = 0.3f,
                    TopP = 0.9f,
                    RepeatPenalty = 1.1f,
                    Seed = 0,
                }
            };

            LogToFile($"LLM prompt ({fullPrompt.Length} chars):\n{fullPrompt}");

            var sb = new System.Text.StringBuilder();
            int tokenCount = 0;
            await foreach (var token in _executor.InferAsync(fullPrompt, inferenceParams))
            {
                sb.Append(token);
                tokenCount++;
            }

            sw.Stop();
            LogToFile($"LLM raw output ({tokenCount} tokens, {sw.ElapsedMilliseconds}ms): [{sb}]");
            _logger.LogInformation("Raw LLM output ({TokenCount} tokens, {Elapsed}ms): [{Raw}]",
                tokenCount, sw.ElapsedMilliseconds, sb.ToString());

            var translation = CleanOutput(sb.ToString());

            _cache.Set(text, translation);

            _logger.LogInformation("LLM inference done in {Elapsed}ms | cache size={Count}", sw.ElapsedMilliseconds, _cache.Count);

            return new TranslationResult
            {
                TranslatedText = translation,
                ProviderName = Name,
                IsSuccess = true,
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "LLM inference failed for text (len={Len})", text.Length);
            return new TranslationResult
            {
                TranslatedText = string.Empty,
                ProviderName = Name,
                IsSuccess = false,
                ErrorMessage = ex.Message,
                ElapsedMilliseconds = sw.ElapsedMilliseconds
            };
        }
        finally
        {
            _inferLock.Release();
        }
    }

    private static readonly System.Text.RegularExpressions.Regex ThinkBlockRegex =
        new(@"<think>.*?</think>", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string CleanOutput(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        // Remove Qwen3 reasoning block (kept empty by /no_think, but the tags still appear)
        var text = ThinkBlockRegex.Replace(raw, string.Empty);

        // Strip any lone / unclosed think tags and ChatML markers that leaked through
        foreach (var tag in new[] { "<think>", "</think>", "<|im_end|>", "<|im_start|>", "<|endoftext|>" })
        {
            text = text.Replace(tag, string.Empty);
        }

        // Drop a leading "assistant" role token if the template echoed it
        text = text.Trim();
        if (text.StartsWith("assistant", StringComparison.OrdinalIgnoreCase))
            text = text["assistant".Length..].TrimStart(':', ' ', '\n', '\r');

        return text.Trim();
    }

    private static void LogToFile(string message)
    {
        try
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MangaFlow", "activation.log");
            var dir = System.IO.Path.GetDirectoryName(logPath)!;
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(logPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    private Task UnloadAsync()
    {
        _executor = null;

        _context?.Dispose();
        _context = null;

        _model?.Dispose();
        _model = null;

        _isLoaded = false;
        _loadedModelPath = string.Empty;
        _loadedGpuLayers = 0;

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _inferLock.Wait();
        try
        {
            _executor = null;
            _context?.Dispose();
            _model?.Dispose();
        }
        finally
        {
            _inferLock.Release();
            _inferLock.Dispose();
        }
    }
}
