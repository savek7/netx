using Microsoft.Extensions.DependencyInjection;
using Tools.Mediation.Abstractions;

namespace Tools.Mediation.MediatR
{
    internal static class MediationPipeline
    {
        internal static Task<TResult> RunAsync<TMessage, TResult>(
            IServiceProvider services, TMessage message, PipelineNext<TResult> invokeHandler, CancellationToken cancellationToken)
        {
            PipelineNext<TResult> invokeNext = invokeHandler;
            foreach (IPipelineBehavior<TMessage, TResult> behavior in services.GetServices<IPipelineBehavior<TMessage, TResult>>().Reverse())
            {
                PipelineNext<TResult> continuation = invokeNext;
                invokeNext = () => behavior.HandleAsync(message, continuation, cancellationToken);
            }

            return invokeNext();
        }
    }
}