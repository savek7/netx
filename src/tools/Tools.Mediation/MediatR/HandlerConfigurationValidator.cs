using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tools.Mediation.Abstractions;

namespace Tools.Mediation.MediatR;

internal sealed class HandlerConfigurationValidator(IServiceCollection services, IEnumerable<Type> messageCandidates)
    : IHostedLifecycleService
{
    private static readonly Dictionary<Type, Type> handlerContracts = new()
    {
        [typeof(ICommand)] = typeof(ICommandHandler<>),
        [typeof(ICommand<>)] = typeof(ICommandHandler<,>),
        [typeof(IQuery<>)] = typeof(IQueryHandler<,>),
    };

    private readonly IServiceCollection services = services;
    private readonly IEnumerable<Type> messageCandidates = messageCandidates;

    public Task StartingAsync(CancellationToken cancellationToken)
    {
        ILookup<Type, ServiceDescriptor> handlers = services
            .Where(descriptor => !descriptor.IsKeyedService)
            .ToLookup(descriptor => descriptor.ServiceType);
        List<string> errors = [];

        foreach (Type message in GetMessages(handlers))
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (Type contract in message.GetInterfaces())
            {
                Type? handlerContract = GetHandlerContract(message, contract);
                if (handlerContract is null)
                {
                    continue;
                }

                Type handlerDefinition = handlerContract.GetGenericTypeDefinition();
                int count = handlers[handlerContract].Count()
                    + handlers[handlerDefinition].Count(descriptor => MatchesOpenHandler(descriptor, handlerContract));
                if (count == 0)
                {
                    errors.Add($"No handler registered for '{message.FullName}' ({contract}).");
                }
                else if (count > 1)
                {
                    errors.Add($"Multiple handlers ({count}) registered for '{message.FullName}' ({contract}).");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"Invalid mediation configuration:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }

        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private IEnumerable<Type> GetMessages(ILookup<Type, ServiceDescriptor> handlers)
    {
        Type[] candidates = messageCandidates.Where(type => !type.IsAbstract).ToArray();
        HashSet<Type> genericDefinitions = candidates.Where(type => type.IsGenericTypeDefinition).ToHashSet();
        IEnumerable<Type> configuredGenericMessages = handlers
            .Select(group => group.Key)
            .Where(type => type.IsConstructedGenericType && handlerContracts.ContainsValue(type.GetGenericTypeDefinition()))
            .Select(type => type.GenericTypeArguments[0])
            .Where(type => type.IsConstructedGenericType
                && !type.ContainsGenericParameters
                && genericDefinitions.Contains(type.GetGenericTypeDefinition()));

        return candidates.Where(type => !type.ContainsGenericParameters).Union(configuredGenericMessages);
    }

    private static Type? GetHandlerContract(Type message, Type contract)
    {
        Type definition = contract.IsGenericType ? contract.GetGenericTypeDefinition() : contract;
        return handlerContracts.TryGetValue(definition, out Type? handlerDefinition)
            ? handlerDefinition.MakeGenericType([message, .. contract.GenericTypeArguments])
            : null;
    }

    private static bool MatchesOpenHandler(ServiceDescriptor descriptor, Type handlerContract)
    {
        try
        {
            Type implementation = descriptor.ImplementationType!.MakeGenericType(handlerContract.GenericTypeArguments);
            return handlerContract.IsAssignableFrom(implementation);
        }
        catch (ArgumentException)
        {
            // DI omits open registrations with incompatible generic constraints when resolving multiple services.
            return false;
        }
    }
}