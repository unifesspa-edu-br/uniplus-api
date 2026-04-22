namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.DependencyInjection;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;

public class CorrelationIdServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCorrelationIdAccessor_DeveResolverMesmaInstanciaParaAmbasInterfaces()
    {
        // Protege o invariante da registração: ICorrelationIdAccessor (reader)
        // e ICorrelationIdWriter (writer) devem apontar para a MESMA singleton
        // concreta. Se alguém trocar por AddSingleton<I, Impl>() separado, as
        // duas interfaces passam a resolver instâncias distintas e o fluxo de
        // correlação entre middleware e consumers se quebra silenciosamente.
        ServiceCollection services = new();
        services.AddCorrelationIdAccessor();

        using ServiceProvider sp = services.BuildServiceProvider();

        ICorrelationIdAccessor reader = sp.GetRequiredService<ICorrelationIdAccessor>();
        ICorrelationIdWriter writer = sp.GetRequiredService<ICorrelationIdWriter>();
        CorrelationIdAccessor concreta = sp.GetRequiredService<CorrelationIdAccessor>();

        reader.Should().BeSameAs(writer);
        reader.Should().BeSameAs(concreta);
    }

    [Fact]
    public void AddCorrelationIdAccessor_ComServicesNulo_DeveLancarArgumentNullException()
    {
        IServiceCollection? services = null;

        Action acao = () => services!.AddCorrelationIdAccessor();

        acao.Should().Throw<ArgumentNullException>();
    }
}
