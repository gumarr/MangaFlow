# Building the MangaFlow installer

MangaFlow ships as a **self-contained, unpackaged WinUI 3 desktop app** (no MSIX, no
.NET runtime needed on the target machine). The installer is built with **Inno Setup**.

## Prerequisites

1. **.NET 8 SDK** (already required to build the app).
2. **Inno Setup 6** — download from https://jrsoftware.org/isdl.php and install.

## One-command build

```powershell
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
```

This publishes the app (Release, win-x64, self-contained) and compiles the installer.
Output: `installer\Output\MangaFlow-Setup-1.0.0.exe`.

Options:
- `-SkipPublish` — reuse the existing publish output (faster when iterating on the .iss).
- `-Version 1.2.3` — set the installer version.

## Manual build (two steps)

```powershell
# 1. Publish
dotnet publish MangaFlow.App\MangaFlow.App.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishProfile=win-x64

# 2. Compile installer
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\MangaFlow.iss
```

## What the installer contains (~900 MB)

- `MangaFlow.App.exe` + all managed DLLs
- WindowsAppSDK self-contained runtime (so no separate install needed)
- **Both** llama.cpp native backends: `avx2` (CPU) and `cuda12` (NVIDIA GPU)
- Bundled RapidOCR ONNX models under `models\v5\` (OCR works out of the box)

The CUDA backend (`ggml-cuda.dll`, ~500 MB) is the bulk of the size. See
[../docs/GPU_AND_PACKAGING.md](../docs/GPU_AND_PACKAGING.md) for how to ship a smaller
CPU-only build, or use Vulkan, if installer size matters.

## What the installer does NOT contain

- **The GGUF translation model (4–5 GB)** — the user downloads it and points to it in
  Settings → LLM Translation Model.
- **NVIDIA CUDA runtime** (`cudart64_12.dll`, `cublas64_12.dll`) — comes with the user's
  NVIDIA driver. If absent, the app detects it and warns, then runs on CPU.

## Runtime paths (no admin write needed after install)

- App + OCR models install to `Program Files\MangaFlow` (read-only at runtime).
- Database, settings and logs are written to `%LocalAppData%\MangaFlow`.
- OCR model path defaults to the install dir's `models\v5`, resolved via
  `AppDomain.CurrentDomain.BaseDirectory`, so it works regardless of install location.
