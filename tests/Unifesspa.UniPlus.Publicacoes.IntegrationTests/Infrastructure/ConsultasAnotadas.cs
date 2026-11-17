namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

using System.Collections.Concurrent;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Host;

/// <summary>Barramento de consultas que só anota o que recebe, para provar que uma recusa da fronteira saiu antes da consulta.</summary>
internal sealed class ConsultasAnotadas : IQueryBus
{
    private readonly ConcurrentQueue<object> _consultas = new();

    internal IReadOnlyCollection<object> Enviadas => _consultas;

    public Task<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken ct = default)
    {
        _consultas.Enqueue(query);
        return Task.FromResult(default(TResponse)!);
    }

    internal WebApplicationFactory<HostAssemblyMarker> SubstituirNoHost(WebApplicationFactory<HostAssemblyMarker> host) =>
        host.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IQueryBus>();
            services.AddSingleton<IQueryBus>(this);
        }));
}
