namespace KokoroSharp.Core;

using Microsoft.ML.OnnxRuntime;

using System.Collections.Concurrent;

/// <summary> Lower-level module that contains the core pipeline that enables inference with the model. </summary>
/// <remarks> Contains a background worker thread that dispatches queued jobs/actions linearly. </remarks>
public class KokoroEngine : IDisposable {
    protected readonly KokoroModel model;
    protected readonly BlockingCollection<KokoroJob> queuedJobs = [];
    readonly CancellationTokenSource cancellation = new();

    /// <summary> Creates a new Kokoro Engine instance, loading the model into memory and initializing a background worker thread to continuously scan for newly queued jobs, dispatching them in order, when it's free. </summary>
    /// <remarks> If 'options' is specified, the model will be loaded with them. This is particularly useful when needing to run on non-CPU backends, as the default backend otherwise is the CPU with 8 threads. </remarks>
    public KokoroEngine(string modelPath, SessionOptions options = null) {
        model = new KokoroModel(modelPath, options);

        new Thread(() =>
        {
            try {
                while (queuedJobs.TryTake(out var kokoroJob, -1, cancellation.Token))
                    while (!cancellation.IsCancellationRequested && !kokoroJob.isDone)
                        kokoroJob.Progress(model);
            }
            catch (OperationCanceledException) {
                // If cancellation was requested just swallow the cancellation exception and exit.
            }
        }).Start();
    }

    /// <summary> Enqueues a job for the Kokoro TTS engine, scheduling it to be processed when the engine is free. </summary>
    /// <remarks> The job will be automatically dispatched when all prior jobs have been completed or canceled. Canceled jobs resolve and get skipped when their order arrives. </remarks>
    public KokoroJob EnqueueJob(KokoroJob job) {
        ObjectDisposedException.ThrowIf(cancellation.IsCancellationRequested, this);
        queuedJobs.Add(job);
        return job;
    }

    /// <summary> Immediately cancels any ongoing registered jobs and playbacks, frees memory taken by the model, and notifies the background worker thread to exit. </summary>
    /// <remarks> Note that this will not free up memory that the voices take (~25MB if ALL languages are loaded). </remarks>
    public virtual void Dispose() {
        cancellation.Cancel();
        foreach (var job in queuedJobs) { job.Cancel(); }
        queuedJobs.Dispose();
        model.Dispose();
        GC.SuppressFinalize(this);
    }
}
