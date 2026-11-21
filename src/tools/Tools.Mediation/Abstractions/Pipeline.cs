namespace Tools.Mediation.Abstractions
{
    public delegate Task<TResult> PipelineNext<TResult>();

    public interface IPipelineBehavior<in TMessage, TResult>
    {
        Task<TResult> HandleAsync(TMessage message, PipelineNext<TResult> invokeNext, CancellationToken cancellationToken);
    }
}