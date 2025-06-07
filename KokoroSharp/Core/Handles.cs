namespace KokoroSharp.Core;

public enum KokoroPlaybackHandleState { Queued, InProgress, Completed, Aborted }

/// <summary> Handle for audio samples that are queued to be spoken, and progress callbacks regarding their playback. </summary>
/// <remarks> The included delegates can be subscribed to, and/or playback can be aborted using this handle, if needed. </remarks>
public class PlaybackHandle {
    public float[] Samples;
    public Action OnStarted;
    public Action OnSpoken;
    public Action<(float time, float percentage)> OnCanceled;
    public Action OnAborted;

    /// <summary> The playback instance that owns this handle. </summary>
    public KokoroPlayback Owner { get; init; }

    public KokoroPlaybackHandleState State { get; set; } = KokoroPlaybackHandleState.Queued;
    public bool Aborted => State == KokoroPlaybackHandleState.Aborted;

    /// <summary> Abort playback of these samples, marking them as something to never be spoken of. </summary>
    /// <remarks> Optionally, the `OnCanceled` event can be raised on demand. </remarks>
    public void Abort(bool raiseCancelCallback = false) {
        if (State == KokoroPlaybackHandleState.Completed || State == KokoroPlaybackHandleState.Aborted) { return; }
        State = KokoroPlaybackHandleState.Aborted;
        if (raiseCancelCallback) { OnCanceled?.Invoke((0f, 0f)); }
        OnAborted?.Invoke();
    }

    public PlaybackHandle(float[] samples, Action OnStarted, Action OnSpoken, Action<(float time, float percentage)> OnCanceled) => (Samples, this.OnStarted, this.OnSpoken, this.OnCanceled) = (samples, OnStarted, OnSpoken, OnCanceled);
}

/// <summary> A handle containing callbacks covering the full lifetime of a 'Speak' request. </summary>
/// <remarks> The included delegates can be subscribed to for fine updates over the synthesis's lifetime. </remarks>
public class SynthesisHandle {
    /// <summary> Callback raised when the info of a specific job step got sent to the model for inference. (NOT for playback) </summary>
    /// <remarks> Once the inference starts, the ONNX does not support its cancellation, so it has to be waited out. </remarks>
    public Action<KokoroJob.KokoroJobStep> OnInferenceStepStarted;

    /// <summary> Callback raised when a specific inference job step was completed. (NOT the playback) </summary>
    /// <remarks> Once an inference step is completed, a playback handle is created, which is passed to this callback. </remarks>
    public Action<KokoroJob.KokoroJobStep, PlaybackHandle> OnInferenceStepCompleted;

    /// <summary> Callback raised when playback for given speech request just started. </summary>
    /// <remarks> Can be used to retrieve info about the original task, including spoken text, and phonemes. </remarks>
    public Action<SpeechStartPacket> OnSpeechStarted;

    /// <summary> Callback raised when a text segment was spoken successfully, progressing the speech to the next segment. </summary>
    /// <remarks> Note that some contents of this packet are GUESSED, which means they might not be accurate. </remarks>
    public Action<SpeechProgressPacket> OnSpeechProgressed;

    /// <summary> Callback raised when the whole given text was spoken successfully. </summary>
    /// <remarks> Can be used to retrieve info about the original task, including spoken text, and phonemes. </remarks>
    public Action<SpeechCompletionPacket> OnSpeechCompleted;

    /// <summary> Callback raised when the playback was stopped amidst speech. Can retrieve which parts were spoken, in part or in full. </summary>
    /// <remarks> Note that "Cancel" will NOT BE CALLED for speeches whose playback never ever started. Consider subscribing to this in `OnSpeechStarted`. </remarks>
    public Action<SpeechCancellationPacket> OnSpeechCanceled;


    /// <summary> The inference job this handle is connected to. </summary>
    public KokoroJob Job { get; init; }

    /// <summary> The text this handle's job is responsible of speaking. </summary>
    public string TextToSpeak { get; init; }

    /// <summary> Contains the handles of the audio playback instances that are ready to be played. </summary>
    public List<PlaybackHandle> ReadyPlaybackHandles { get; } = [];
}
