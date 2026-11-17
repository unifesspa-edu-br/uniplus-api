namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.Middleware;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging.Middleware;

using Wolverine;
using Wolverine.FluentValidation;
using Wolverine.Runtime.Handlers;

public sealed class MessagingMiddlewarePoliciesTests
{
    private sealed class AcaoDeFalhaDoModulo<T> : IFailureAction<T>
    {
        public void Throw(T message, IReadOnlyList<FluentValidation.Results.ValidationFailure> failures) =>
            throw new InvalidOperationException();
    }

    private static List<ServiceDescriptor> AcoesDeFalha(WolverineOptions opcoes) =>
        opcoes.Services.Where(descritor => descritor.ServiceType == typeof(IFailureAction<>)).ToList();

    [Fact]
    public void AddCommandQueryMiddleware_SemAcaoDeFalhaRegistrada_RegistraASemDadoPessoalComoSingleton()
    {
        WolverineOptions opcoes = new();

        opcoes.AddCommandQueryMiddleware();

        ServiceDescriptor registro = AcoesDeFalha(opcoes).Should().ContainSingle().Subject;
        registro.ImplementationType.Should().Be(typeof(PiiSafeValidationFailureAction<>));
        registro.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddCommandQueryMiddleware_ComAcaoDeFalhaJaRegistrada_MantemARegistradaAntes()
    {
        WolverineOptions opcoes = new();
        opcoes.Services.AddSingleton(typeof(IFailureAction<>), typeof(AcaoDeFalhaDoModulo<>));

        opcoes.AddCommandQueryMiddleware();

        AcoesDeFalha(opcoes).Should().ContainSingle()
            .Which.ImplementationType.Should().Be(typeof(AcaoDeFalhaDoModulo<>));
    }

    [Fact]
    public void AddCommandQueryMiddleware_OpcoesNulas_LancaArgumentNull()
    {
        Action registrar = () => MessagingMiddlewarePolicies.AddCommandQueryMiddleware(null!);

        registrar.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddCorrelationIdMiddleware_OpcoesNulas_LancaArgumentNull()
    {
        Action registrar = () => MessagingMiddlewarePolicies.AddCorrelationIdMiddleware(null!);

        registrar.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task IsCommandOrQueryChain_CadeiaDeCommandECadeiaDeEvento_ReconheceSoOCommand()
    {
        using IHost host = Host.CreateDefaultBuilder()
            .UseWolverine(opcoes => opcoes.Discovery.IncludeType(typeof(PingHandler)).IncludeType(typeof(EventoDeTesteHandler)))
            .Build();
        await host.StartAsync();

        HandlerGraph grafo = host.Services.GetRequiredService<HandlerGraph>();

        MessagingMiddlewarePolicies.IsCommandOrQueryChain(grafo.ChainFor<PingCommand>()!).Should().BeTrue();
        MessagingMiddlewarePolicies.IsCommandOrQueryChain(grafo.ChainFor<EventoDeTeste>()!).Should().BeFalse();
        await host.StopAsync();
    }

    [Fact]
    public void IsCommandOrQueryChain_CadeiaNula_LancaArgumentNull()
    {
        Action avaliar = () => MessagingMiddlewarePolicies.IsCommandOrQueryChain(null!);

        avaliar.Should().Throw<ArgumentNullException>();
    }
}

public sealed record EventoDeTeste(string Descricao);

public static class EventoDeTesteHandler
{
    public static void Handle(EventoDeTeste evento) => ArgumentNullException.ThrowIfNull(evento);
}
