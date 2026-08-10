[![NuGet](https://img.shields.io/nuget/v/KokoroSharp.svg)](https://www.nuget.org/packages/KokoroSharp/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/KokoroSharp.svg)](https://www.nuget.org/packages/KokoroSharp/)

https://github.com/user-attachments/assets/67abbc2f-44e4-4044-a7bd-6a67b9238a49

# KokoroSharp
KokoroSharp is a fully-featured inference engine for [Kokoro TTS](https://huggingface.co/spaces/hexgrad/Kokoro-TTS), built entirely in C# with ONNX runtime.
It enables developers to perform flexible and fast text-to-speech synthesis utilizing multiple speakers and languages.

## Features
- Plug & Play integration via the nuget package. All dependencies are handled automatically.
- Nuget package includes ALL voices released by hexgrad with their [Kokoro 82M v1.0](https://huggingface.co/hexgrad/Kokoro-82M/tree/main/voices) and [v1.1-zh](https://huggingface.co/hexgrad/Kokoro-82M-v1.1-zh/tree/main/voices) releases.
- High-level interface designed to suit both beginners and power users.
- Text-segment streaming for seamless text-to-speech. Responses feel instant.
- Voice mixing with no restrictions on the amounts of voices mixed, and ability to save/load mixed voices.
- Linear job scheduling with background worker as dispatcher.
- Optional multi-platform playback support with pre-integrated audio queue handling.

Supports languages/accents:
- `[American English, British English, MandarinChinese, Japanese, Hindi, Spanish, French, Italian, Brazilian/Portuguese]`.

## How to setup
- **On Windows, Linux, and MacOS:** Install via **Nuget** ([Package Manager](https://learn.microsoft.com/en-us/nuget/quickstart/install-and-use-a-package-in-visual-studio) or [CLI](https://learn.microsoft.com/en-us/nuget/quickstart/install-and-use-a-package-using-the-dotnet-cli)), and you're set!
- **Selecting the correct package:** [KokoroSharp.CPU](https://www.nuget.org/packages/KokoroSharp.CPU) is plug-and-play. For GPU support, see [RUNNING_ON_GPU.md](https://github.com/Lyrcaxis/KokoroSharp/blob/main/RUNNING_ON_GPU.md).
- **On Android**: KokoroSharp **should** work on mobile as of v0.8.3, using [KokoroSharp.Android](https://www.nuget.org/packages/KokoroSharp.Android).
- **On iOS**: iOS is supported as of v0.8.1. Full synthesis, and playback with a custom `CrossPlatformHelper.CustomAudioPlayer`.


## Getting started with the KokoroSharp.CPU package:
```csharp
KokoroTTS tts = KokoroTTS.LoadModel(); // Load or download the model (~320MB for full precision)
KokoroVoice heartVoice = KokoroVoiceManager.GetVoice("af_heart"); // Grab a voice of your liking,
while (true) { tts.SpeakFast(Console.ReadLine(), heartVoice); } // .. and have it speak your text!
// Note: Language detection is automated based on what the loaded voice supports.
```

###### For running on GPU, check out [RUNNING_ON_GPU.md](https://github.com/Lyrcaxis/KokoroSharp/blob/main/RUNNING_ON_GPU.md).
###### For `KokoroSharp.Android`, just call `KokoroSharpAndroid.Init()` before the standard workflow. This will also download the voices synchronously.

Above is a simple way to get started on the highest level. For more control, check out [the example Program](https://github.com/Lyrcaxis/KokoroSharp/blob/main/Program.cs), which covers more advanced parts like job scheduling, voice mixing, and long-term, speaker-agnostic playback queuing.

###### Models can be found on [KokoroSharpBinaries' releases](https://github.com/Lyrcaxis/KokoroSharpBinaries/releases), and can be loaded via `KokoroTTS.LoadModel("path/to/model")`, or downloaded automatically with `KokoroTTS.LoadModel()`. Check out the various overloads of `KokoroTTS.LoadModel` for background loading.

## Kokoro v1.1-zh (Chinese model & voices)
```csharp
KokoroTTS tts = KokoroTTS.LoadModel(KModel.zh_float32); // Load or download the Kokoro v1.1-zh model,
tts.SpeakFast("你好，世界！", KokoroVoiceManager.GetVoice("zf_001")); // .. and speak with any of its voices!
```
###### The v1.1-zh voices come bundled with the package in `voices/voices-zh`, loaded automatically alongside the v1.0 ones.

## Synthesizing to WAV (no playback)
```csharp
KokoroWavSynthesizer synth = KokoroWavSynthesizer.LoadModel();
byte[] audioBytes = synth.Synthesize("Hello world!", KokoroVoiceManager.GetVoice("af_heart")); // ..or an async equivalent.
KokoroWavSynthesizer.SaveAudioToFile(audioBytes, "hello.wav"); //.. or play back with your output of choice.
```
###### Similarly, `synth.SynthesizeWithTimestampsAsync` can provide the per-phoneme timestamps, for lip syncing or other purposes.

## Notes
- All communication with the AI model and playback devices happens on background threads, letting the main thread focus on rendering the UI in peace. The library is carefully designed with thread-safety in mind.

- The `voices` folder is automatically copied to your build path when you build and is ready to be accessed. Developers may opt to remove it when shipping their apps.

- Mind that `LoadVoicesFromPath` exists as an option, in case developers want to implement their custom voice-loading logic when shipping a project that utilizes KokoroSharp for text-to-speech synthesis.

- In addition, the built-in tokenization (`text -> tokens`) is NOT mandatory, and can be bypassed for platforms like `Android/iOS`, given developers provide pre-phonemized input with their phonemization solution of choice.

- KokoroSharp uses [MisakiSharp](https://github.com/Lyrcaxis/MisakiSharp), which can also be used as a standalone phonemization solution, native in C#.

## License
- This project is licensed under the [MIT License](https://github.com/Lyrcaxis/KokoroSharp/blob/main/LICENSE).
- The [Kokoro 82M model](https://huggingface.co/hexgrad/Kokoro-82M) and its voices are released under the [Apache License](https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/apache-2.0.md).
