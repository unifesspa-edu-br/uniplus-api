namespace Unifesspa.UniPlus.Verificador.UnitTests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Testes.Compartilhado;

using Wolverine.Runtime;
using Wolverine.Runtime.Serialization;

/// <summary>
/// O código de contrato dos enums nas mensagens do verificador, pelo serializador que o Wolverine do ponto de entrada
/// real entrega ao host construído sem iniciar.
/// </summary>
public sealed class SerializacaoDosCodigosNasMensagensDoVerificadorTests : SerializacaoDosCodigosNasMensagensTestsBase
{
    private static readonly IMessageSerializer SerializadorDoVerificador = ResolverODoVerificador();

    protected override IMessageSerializer Serializador => SerializadorDoVerificador;

    private static IMessageSerializer ResolverODoVerificador()
    {
        using IHost host = PontoDeEntradaDoVerificador.ConstruirSemIniciar(PontoDeEntradaDoVerificador.ConfiguracaoMinima);

        return host.Services.GetRequiredService<IWolverineRuntime>().Options.DefaultSerializer;
    }
}
