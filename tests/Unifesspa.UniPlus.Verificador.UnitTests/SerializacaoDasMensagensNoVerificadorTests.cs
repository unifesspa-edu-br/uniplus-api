namespace Unifesspa.UniPlus.Verificador.UnitTests;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Wolverine;
using Wolverine.Runtime;
using Wolverine.Runtime.Serialization;

/// <summary>
/// As mensagens do verificador são lidas com o mesmo contrato de instantes da API, que está do outro lado das filas:
/// instante em microssegundos e instante não anulável obrigatório no corpo.
/// </summary>
public sealed class SerializacaoDasMensagensNoVerificadorTests
{
    private static readonly string[] ConfiguracaoMinima =
    [
        "--Verificador:Armazenamento:Endpoint=minio:9000",
        "--Verificador:Armazenamento:AccessKey=acesso-de-teste",
        "--Verificador:Armazenamento:SecretKey=segredo-de-teste",
        "--Verificador:Armazenamento:BucketEnvios=envios",
        "--Verificador:Armazenamento:BucketArquivos=arquivos",
        "--Verificador:RabbitMq:Host=rabbit",
    ];

    [Fact(DisplayName = "O serializador do verificador lê o instante com 7 casas truncado em microssegundos")]
    public void Serializador_InstanteComSeteCasas_LidoComSeis()
    {
        IMessageSerializer serializador = SerializadorDaComposicaoCompleta();

        MensagemComInstante lida = (MensagemComInstante)serializador.ReadFromData(
            typeof(MensagemComInstante), Corpo("{\"ocorridoEm\":\"2026-11-12T10:00:00.1234567Z\"}"));

        lida.OcorridoEm.Should().Be(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero).AddTicks(1_234_560));
    }

    [Fact(DisplayName = "O serializador do verificador recusa a mensagem sem o instante obrigatório")]
    public void Serializador_InstanteAusente_LancaJsonException()
    {
        IMessageSerializer serializador = SerializadorDaComposicaoCompleta();

        Action ler = () => serializador.ReadFromData(typeof(MensagemComInstante), Corpo("{}"));

        ler.Should().Throw<JsonException>().WithMessage("*ocorridoEm*");
    }

    private static Envelope Corpo(string json) => new() { Data = Encoding.UTF8.GetBytes(json) };

    private static IMessageSerializer SerializadorDaComposicaoCompleta()
    {
        using IHost host = PontoDeEntradaDoVerificador.ConstruirSemIniciar(ConfiguracaoMinima);

        return host.Services.GetRequiredService<IWolverineRuntime>().Options.DefaultSerializer;
    }

    private sealed record MensagemComInstante(DateTimeOffset OcorridoEm);
}
