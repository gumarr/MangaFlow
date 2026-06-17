using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LLama;
using LLama.Common;
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

    public string Name => "LlamaCpp";
    public bool IsModelLoaded => _isLoaded;
    public int CacheHits { get; private set; }
    public int CacheMisses { get; private set; }

    public LlamaCppTranslationProvider(ILogger<LlamaCppTranslationProvider> logger)
    {
        _logger = logger;
    }

    public async Task EnsureModelLoadedAsync(string modelPath, int cpuThreads = 4, bool useGpu = false, float temperature = 0.3f)
    {
        if (_isLoaded && _loadedModelPath == modelPath)
            return;

        await _inferLock.WaitAsync();
        try
        {
            if (_isLoaded && _loadedModelPath == modelPath)
                return;

            await UnloadAsync();

            _logger.LogInformation("Loading GGUF model from: {Path}", modelPath);
            var sw = Stopwatch.StartNew();

            var parameters = new ModelParams(modelPath)
            {
                ContextSize = 4096,
                GpuLayerCount = useGpu ? 32 : 0,
                Threads = cpuThreads,
                BatchSize = 512,
            };

            _model = await LLamaWeights.LoadFromFileAsync(parameters);
            _context = _model.CreateContext(parameters);
            _executor = new StatelessExecutor(_model, parameters);

            _isLoaded = true;
            _loadedModelPath = modelPath;
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
                AntiPrompts = ["<|im_end|>", "<|im_start|>", "\n\n\n"],
                SamplingPipeline = new LLama.Sampling.DefaultSamplingPipeline
                {
                    Temperature = 0.3f,
                    TopP = 0.9f,
                    RepeatPenalty = 1.1f,
                    Seed = 0,
                }
            };

            var sb = new System.Text.StringBuilder();
            await foreach (var token in _executor.InferAsync(fullPrompt, inferenceParams))
            {
                sb.Append(token);
            }

            sw.Stop();
            var translation = sb.ToString().Trim();

            // Strip any trailing anti-prompts that leaked through
            foreach (var anti in new[] { "<|im_end|>", "<|im_start|>" })
            {
                int idx = translation.IndexOf(anti, StringComparison.Ordinal);
                if (idx >= 0)
                    translation = translation[..idx].Trim();
            }

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

    private Task UnloadAsync()
    {
        _executor = null;

        _context?.Dispose();
        _context = null;

        _model?.Dispose();
        _model = null;

        _isLoaded = false;
        _loadedModelPath = string.Empty;

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
