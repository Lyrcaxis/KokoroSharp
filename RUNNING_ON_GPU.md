# Running KokoroSharp on GPU

This guide covers how to use KokoroSharp with GPU acceleration for faster inference.

## Available Packages

KokoroSharp offers several packages:

| Package | Platform | GPU Support | Requirements |
|---------|----------|-------------|--------------|
| [KokoroSharp](https://www.nuget.org/packages/KokoroSharp) | All platforms | Runtime-dependant | Expects devs to bring their own runtime |
| [KokoroSharp.CPU](https://www.nuget.org/packages/KokoroSharp) | All Platforms | CPU-ONLY | Plug & Play with just CPU |
| [KokoroSharp.GPU](https://www.nuget.org/packages/KokoroSharp.GPU) | Windows, Linux, Mac (Intel) | NVIDIA (CUDA) | CUDA Toolkit + cuDNN |
| [KokoroSharp.GPU.Windows](https://www.nuget.org/packages/KokoroSharp.GPU.Windows) | Windows | NVIDIA (CUDA) | CUDA Toolkit + cuDNN |
| [KokoroSharp.GPU.Linux](https://www.nuget.org/packages/KokoroSharp.GPU.Linux) | Linux | NVIDIA (CUDA) | CUDA Toolkit + cuDNN |

## Quick Start

### CPU (Here for reference)

The CPU runtime ***just works***, and should be quick for real-time speech synthesis, even on old CPUs:
- Install [KokoroSharp.CPU](https://www.nuget.org/packages/KokoroSharp.CPU) via NuGet, then run:
```cs
KokoroTTS tts = KokoroTTS.LoadModel();
tts.SpeakFast("Hello from GPU!", KokoroVoiceManager.GetVoice("af_heart"));
```

### CUDA (Cross-platform)

For NVIDIA GPUs with CUDA support:
- Choose either [KokoroSharp.GPU.Windows](https://www.nuget.org/packages/KokoroSharp.GPU.Windows), [KokoroSharp.GPU.Linux](https://www.nuget.org/packages/KokoroSharp.GPU.Linux), or [KokoroSharp.GPU](https://www.nuget.org/packages/KokoroSharp.GPU).
- Download & install [CUDA Toolkit (v12.x)](https://developer.nvidia.com/cuda-12-4-0-download-archive), and make sure it's in SYSTEM PATH or put the libraries next to your compiled executable.
- Download & install [cuDNN](https://developer.nvidia.com/cudnn), and make sure it's in SYSTEM PATH.
- Restart your IDE/terminal after installation, then, run:
```cs
var options = new SessionOptions();
options.AppendExecutionProvider_CUDA();
KokoroTTS tts = KokoroTTS.LoadModel(sessionOptions: options);
tts.SpeakFast("Hello from GPU!", KokoroVoiceManager.GetVoice("af_heart"));
```


## CoreML

I currently haven't managed to make KokoroSharp with CoreML. ONNX Supports it via `options.AppendExecutionProvider_CoreML();`, but there's no package that brings the runtime.
The standard workflow would be:
- Install [KokoroSharp](https://www.nuget.org/packages/KokoroSharp) via NuGet.
- Build and install CoreML ONNX runtime, then run:
```cs
var options = new SessionOptions();
options.AppendExecutionProvider_CoreML();
KokoroTTS tts = KokoroTTS.LoadModel(sessionOptions: options);
tts.SpeakFast("Hello from GPU!", KokoroVoiceManager.GetVoice("af_heart"));
```

If you manage to make KokoroSharp work with CoreML, please consider contributing to this file.


## Resolving Errors

If you're getting an `EntryPointNotFoundException`, this means that you don't have the correct KokoroSharp package installed.

It's also possible to receive this error if you have more packages than you should. Deleting excess runtimes should resolve this.

`DllNotFoundException` means you have no runtime AT ALL. For plug & play support (still fast), use the [KokoroSharp.CPU](https://www.nuget.org/packages/KokoroSharp.CPU) package.

Always reset your IDE/terminal after installing dependencies, to make sure the system path is in sync.



### Exact list of CUDA / CUDNN libraries nessesary for inference
<details>
  <summary>(Preview)</summary>
  - cudnn_engines_runtime_compiled64_9.dll <br>
  - cudnn_engines_precompiled64_9.dll <br>
  - cudnn_heuristic64_9.dll <br>
  - cudnn_graph64_9.dll <br>
  - cublasLt64_12.dll <br>
  - cudnn_adv64_9.dll <br>
  - cudnn_ops64_9.dll <br>
  - cublas64_12.dll <br>
  - cudart64_12.dll <br>
  - cufft64_11.dll <br>
  - cudnn64_9.dll <br>
</details>
