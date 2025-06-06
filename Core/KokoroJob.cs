namespace KokoroSharp.Core;

using System.Linq;

public enum KokoroJobState { Queued, Running, Completed, Canceled }

/// <summary> An inference job that is to be dispatched by the <see cref="KokoroTTS"/> engine. </summary>
/// <remarks> Consists of one or multiple steps, which are processed in order. </remarks>
public class KokoroJob {
    public KokoroJobState State { get; protected set; }
    public List<KokoroJobStep> Steps { get; init; }
    public int StepIndex { get; protected set; }

    /// <summary> Will be true if the job was either canceled or completed. </summary>
    public virtual bool isDone => State == KokoroJobState.Completed || State == KokoroJobState.Canceled;


    /// <summary> Progresses the job by processing the next job step, and returns the model's output (audio samples). </summary>
    /// <remarks> Users will typically never need to call this manually, as it's handled by the <see cref="KokoroTTS"/> instance. </remarks>
    public virtual void Progress(KokoroModel model) {
        if (State == KokoroJobState.Canceled) { return; }
        if (StepIndex >= Steps.Count) { State = KokoroJobState.Completed; return; }
        if (StepIndex == 0) { State = KokoroJobState.Running; }

        var step = Steps[StepIndex];
        step.OnStepStarted?.Invoke();

        var output = model.Infer(step.Tokens, step.VoiceStyle, step.Speed);
        if (State == KokoroJobState.Canceled) { return; }

        step.OnStepComplete?.Invoke(output);
        if (++StepIndex >= Steps.Count) { State = KokoroJobState.Completed; }
    }

    /// <summary> Marks the job as canceled. Canceled jobs will not run, and will not trigger their callback if they happen to be running. </summary>
    public virtual void Cancel() => State = KokoroJobState.Canceled;

    /// <summary> Creates a single-step job. When the step is completed, the callback will be invoked with the output waveform. </summary>
    public static KokoroJob Create(int[] tokens, float[,,] voiceStyle, float speed, Action<float[]> OnComplete) => new() { Steps = [new(tokens, voiceStyle, speed, OnComplete)] };

    /// <summary> Creates a multi-step job, usually with pre-segmented token chunks. When the step is completed, the callback will be invoked with the output waveform. </summary>
    public static KokoroJob Create(List<(int[] Tokens, float[,,] VoiceStyle, float Speed)> steps, Action<float[]> OnComplete) => new() { Steps = steps.Select(x => new KokoroJobStep(x.Tokens, x.VoiceStyle, x.Speed, OnComplete)).ToList() };

    /// <summary> Creates a multi-step job, usually with pre-segmented token chunks. When the step is completed, the callback will be invoked with the output waveform. </summary>
    public static KokoroJob Create(List<int[]> segments, float[,,] voiceStyle, float speed, Action<float[]> OnComplete) => new() { Steps = segments.Select(x => new KokoroJobStep(x, voiceStyle, speed, OnComplete)).ToList() };


    /// <summary> An instance of an inference step of a KokoroJob. This typically contains a segment (part of the whole input). </summary>
    /// <remarks> All steps are performed in order, and will block execution of future steps and jobs, enabling linear scheduling. </remarks>
    public class KokoroJobStep {
        public float Speed { get; init; }
        public int[] Tokens { get; init; }
        public float[,,] VoiceStyle { get; init; }

        /// <summary> Gets invoked when this step gets into scope and sent to the model for inference. </summary>
        public Action OnStepStarted { get; set; }

        /// <summary> Gets invoked after this step is fully processed by the engine, with an array of the output audio samples as parameter. </summary>
        public Action<float[]> OnStepComplete { get; set; }

        public KokoroJobStep(int[] tokens, float[,,] voiceStyle, float speed, Action<float[]> OnComplete) {
            (Tokens, VoiceStyle, Speed) = (tokens, voiceStyle, speed);
            OnStepComplete = OnComplete;
        }
    }
}
