[![NuGet](https://img.shields.io/nuget/v/KokoroSharp.svg)](https://www.nuget.org/packages/KokoroSharp/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/KokoroSharp.svg)](https://www.nuget.org/packages/KokoroSharp/)

https://github.com/user-attachments/assets/82a32382-2e9b-4233-a66f-987b2802717e

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
- **Selecting the correct package:** [KokoroSharp.CPU](https://www.nuget.org/packages/KokoroSharp.CPU) is plug-and-play. For GPU support, see [RUNNING_ON_GPU.md]().
- **On Other platforms**: For platforms other than the ones above, developers are expected to provide their own phonemization solution. The built-in tokenizer supports raw `(phonemes -> tokens)` conversion.

###### The package is accessible on all .NET platforms, yet integrated phonemization is only available with the eSpeak NG backend atm.

## Getting started with the KokoroSharp.CPU package:
```csharp
KokoroTTS tts = KokoroTTS.LoadModel(); // Load or download the model (~320MB for full precision)
KokoroVoice heartVoice = KokoroVoiceManager.GetVoice("af_heart"); // Grab a voice of your liking,
while (true) { tts.SpeakFast(Console.ReadLine(), heartVoice); } // .. and have it speak your text!
// Note: Language detection is automated based on what the loaded voice supports.
```

###### For running on GPU, check out [RUNNING_ON_GPU.md](https://github.com/Lyrcaxis/KokoroSharp/blob/main/RUNNING_ON_GPU.md).

Above is a simple way to get started on the highest level. For more control, check out [the example Program](https://github.com/Lyrcaxis/KokoroSharp/blob/main/Program.cs), which covers more advanced parts like job scheduling, voice mixing, and long-term, speaker-agnostic playback queuing.

###### Models can be found on [KokoroSharpBinaries' releases](https://github.com/Lyrcaxis/KokoroSharpBinaries/releases), and can be loaded via `KokoroTTS.LoadModel("path/to/model")`, or downloaded automatically with `KokoroTTS.LoadModel()`. Check out the various overloads of `KokoroTTS.LoadModel` for background loading.

## Kokoro v1.1-zh (Chinese model & voices)
```csharp
KokoroTTS tts = KokoroTTS.LoadModel(KModel.zh_float32); // Load or download the Kokoro v1.1-zh model,
tts.SpeakFast("你好，世界！", KokoroVoiceManager.GetVoice("zf_001")); // .. and speak with any of its voices!
```
###### The v1.1-zh voices come bundled with the package in `voices/voices-zh`, loaded automatically alongside the v1.0 ones.

## Notes
- KokoroSharp prioritizes a smooth developer experience by logging potential misuse instead of throwing exceptions. Wherever possible, the library attempts to automatically resolve issues to minimize disruptions.

- All communication with the AI model and playback devices happens on background threads, letting the main thread focus on rendering the UI in peace. The library is carefully designed with thread-safety in mind.

- The `voices` folder are automatically copied to your build path when you build and are ready to be accessed. Same with the mentioned `espeak` backends. Developers may opt to remove them when shipping their apps.

- Mind that `LoadVoicesFromPath` exists as an option, in case developers want to implement their custom voice-loading logic when shipping a project that utilizes KokoroSharp for text-to-speech synthesis.

- In addition, the built-in tokenization (`text -> tokens`) is NOT mandatory, and can be bypassed for platforms like `Android/iOS`, given developers provide pre-phonemized input with their phonemization solution of choice.

- For Phoneme Literals, you can use the following syntax: `"[tomato](/təmeɪtoʊ/) [tomato](/təmɑːtoʊ/)."`.

## License
- This project is licensed under the [MIT License](https://github.com/Lyrcaxis/KokoroSharp/blob/main/LICENSE).
- The [Kokoro 82M model](https://huggingface.co/hexgrad/Kokoro-82M) and its voices are released under the [Apache License](https://huggingface.co/datasets/choosealicense/licenses/blob/main/markdown/apache-2.0.md).
- eSpeak NG is licensed under the [GPLv3 License](https://github.com/espeak-ng/espeak-ng/blob/master/COPYING).
