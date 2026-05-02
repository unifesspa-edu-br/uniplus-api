namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.DependencyInjection;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Messaging;

public class WolverineMessagingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddWolverineMessaging_DeveRegistrarICommandBusComoWolverineCommandBus()
    {
        // Protege o contrato da extension: ICommandBus (canônico do projeto, ADR-0003)
        // resolve para WolverineCommandBus, que é o único caminho aprovado para
        // delegar a Wolverine.IMessageBus. Se alguém remover a registração ou
        // trocar por outra implementação, este teste quebra antes do startup.
        ServiceCollection services = new();
        services.AddScoped(_ => Substitute.For<Wolverine.IMessageBus>());
        services.AddWolverineMessaging();

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        ICommandBus bus = scope.ServiceProvider.GetRequiredService<ICommandBus>();

        bus.Should().NotBeNull();
        bus.Should().BeOfType<WolverineCommandBus>();
    }

    [Fact]
    public void AddWolverineMessaging_DeveRegistrarIQueryBusComoWolverineQueryBus()
    {
        // Protege o contrato da extension para o lado de leitura do CQRS: IQueryBus
        // resolve para WolverineQueryBus, único caminho aprovado para delegar a
        // Wolverine.IMessageBus em queries. Se alguém remover a registração ou
        // trocar por outra implementação, este teste quebra antes do startup.
        ServiceCollection services = new();
        services.AddScoped(_ => Substitute.For<Wolverine.IMessageBus>());
        services.AddWolverineMessaging();

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        IQueryBus bus = scope.ServiceProvider.GetRequiredService<IQueryBus>();

        bus.Should().NotBeNull();
        bus.Should().BeOfType<WolverineQueryBus>();
    }

    [Fact]
    public void AddWolverineMessaging_ComServicesNulo_DeveLancarArgumentNullException()
    {
        IServiceCollection? services = null;

        Action acao = () => services!.AddWolverineMessaging();

        acao.Should().Throw<ArgumentNullException>();
    }
}
