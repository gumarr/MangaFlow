# GPU Acceleration & Installer Packaging

This document covers how MangaFlow runs the local LLM on CPU or GPU, what ships in
the installer, and what the **target machine** needs for GPU to actually engage.

## How backend selection works

MangaFlow bundles **two** native llama.cpp backends side by side:

| Folder in output | Backend | Used when |
|---|---|---|
| `runtimes/win-x64/native/avx2/` | CPU (AVX2) | always available; default |
| `runtimes/win-x64/native/cuda12/` | NVIDIA CUDA 12 | an NVIDIA GPU + CUDA 12 runtime is present |

At startup, `LlamaCppTranslationProvider.ConfigureNativeLibrary()` calls
`NativeLibraryConfig.All.WithAutoFallback(true)`. LLamaSharp probes for a usable CUDA
runtime; if found it loads the `cuda12` DLLs, otherwise it silently falls back to the
`avx2` CPU build. Nothing crashes on a CPU-only machine.

The active backend is surfaced in the UI:
- **Settings → "Apply & Reload Model"** shows `Running on: GPU (99 layers)` or `Running on: CPU`.
- **Translation Playground** model status shows the same.

## Settings the end user controls

All under **Settings → LLM Translation Model**:

| Setting | Meaning |
|---|---|
| Model File Path (.gguf) | Path to the GGUF model on the user's disk |
| CPU Threads | Physical core count (e.g. 8). Used for CPU inference / prompt eval |
| Temperature | Sampling temperature (0.1–0.4 recommended for consistent translation) |
| Context Size | KV-cache window. 2048 is plenty for manga bubbles |
| GPU Layers | Layers offloaded to GPU when GPU is on (99 = all). Lower if VRAM-limited |
| Use GPU Acceleration | Master toggle. Off = pure CPU |
| Apply & Reload Model | Persists settings and reloads the model with them |

The model is a **singleton** loaded once. Changing path / threads / context / GPU
options requires **Apply & Reload Model** (or an app restart) to take effect.

## What the TARGET machine needs for GPU

Bundling the CUDA DLLs is **necessary but not sufficient**. The user's machine still needs:

1. **An NVIDIA GPU** (GTX 10-series or newer; RTX recommended).
2. **NVIDIA driver** recent enough for CUDA 12 (driver R525+; newer is safer).
3. **CUDA 12 runtime libraries** — specifically `cudart64_12.dll` and `cublas64_12.dll`.
   - These are NOT bundled by default (they are large and licensed by NVIDIA).
   - They come with the NVIDIA driver on most gaming machines, OR via the
     [CUDA Toolkit 12.x](https://developer.nvidia.com/cuda-downloads) redistributable.
   - If they're missing, auto-fallback drops to CPU — the app still works, just slower.

### Built-in CUDA runtime detection

The bundled `ggml-cuda.dll` dynamically links to `cudart64_12.dll`, `cublas64_12.dll`
and `cublasLt64_12.dll`, which are **NOT** shipped with the app. When the user enables
**Use GPU Acceleration** and reloads, `LlamaCppTranslationProvider.DiagnoseCudaRuntime()`
tries to `LoadLibrary` each of those DLLs:

- **All present** → GPU loads normally; status shows `Running on: GPU (N layers)`.
- **Any missing** → the model loads on **CPU** instead, and the UI shows a clear warning:
  > ⚠ NVIDIA CUDA 12 runtime not found ('cudart64_12.dll' could not be loaded).
  > Install a recent NVIDIA driver or the CUDA 12 Toolkit redistributable…

  This appears in **Settings → backend status** and as a short note in the Translation
  Playground model status. So a machine that *has* an RTX card but is *missing* the CUDA
  runtime no longer fails silently — the user is told exactly what to install.

To fix on the target machine, install either:
- a recent **NVIDIA driver** (most include the CUDA runtime), or
- the **CUDA 12 Toolkit** / its redistributable from
  https://developer.nvidia.com/cuda-downloads

If GPU does not engage on a machine you expect it to, check
`%LocalAppData%\MangaFlow\activation.log` for the `[native:...]` lines, the
`Loading model | ... | gpuLayers=N` line, and any `GPU requested but unavailable` line.

## Installer size impact

The CUDA Windows backend adds **~500 MB** (`ggml-cuda.dll` alone is ~500 MB of
precompiled cuBLAS kernels). Total app output is ~790 MB before the GGUF model.

### Options to manage size

- **Ship one universal installer (current setup):** ~790 MB. Simplest; runs everywhere.
- **Ship two installers:** a CPU-only build (drop the
  `LLamaSharp.Backend.Cuda12.Windows` PackageReference) for ~280 MB, plus a CUDA build
  for NVIDIA users. Smaller downloads, more release management.
- **Vulkan instead of CUDA:** `LLamaSharp.Backend.Vulkan` runs on NVIDIA *and* AMD/Intel
  GPUs and is smaller, at ~10–20% lower throughput than native CUDA. Consider this if
  you want one GPU build that also covers AMD machines.

## The model file is never bundled

The `.gguf` (4–5 GB) is downloaded by the user and pointed to via Settings. The
installer only ships the app + native backends, never the model.
