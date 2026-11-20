namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Wolverine.Runtime;
using Wolverine.Runtime.Serialization;

/// <summary>
/// O código de contrato dos enums nas mensagens do Host e do Portal, pelo serializador que a configuração do backbone
/// Wolverine (<see cref="WolverineOutboxConfiguration"/>) entrega ao host construído sem iniciar.
/// </summary>
public sealed class SerializacaoDosCodigosNasMensagensDoHostTests : SerializacaoDosCodigosNasMensagensTestsBase
{
    private const string NomeDaConnectionString = "UniPlusDb";

    private static readonly IMessageSerializer SerializadorDoBackbone = ResolverODoBackbone();

    protected override IMessageSerializer Serializador => SerializadorDoBackbone;

    // A construção do host não conecta ao banco: a connection string só precisa existir.
    private static IMessageSerializer ResolverODoBackbone()
    {
        IConfiguration configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{NomeDaConnectionString}"] = "Host=localhost;Port=5432;Database=x;Username=u;Password=p",
            })
            .Build();

        using IHost host = Host.CreateDefaultBuilder()
            .UseWolverineOutboxCascading(configuracao, NomeDaConnectionString)
            .Build();

        return host.Services.GetRequiredService<IWolverineRuntime>().Options.DefaultSerializer;
    }
}
