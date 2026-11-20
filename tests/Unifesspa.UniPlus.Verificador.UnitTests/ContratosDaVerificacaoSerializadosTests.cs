namespace Unifesspa.UniPlus.Verificador.UnitTests;

using System.Text;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Verificador.Contratos;

using Wolverine.Runtime;
using Wolverine.Runtime.Serialization;

/// <summary>
/// O corpo que o verificador grava nas filas é o contrato com a API e com os consumidores: o pedido e o resultado reais,
/// com todos os campos e todos os valores das enumerações, serializados pela configuração efetiva do Wolverine, são
/// iguais ao corpo de referência.
/// </summary>
public sealed class ContratosDaVerificacaoSerializadosTests
{
    private const string ArquivoDeReferencia = "mensagens-serializadas.txt";

    private static readonly Guid ArquivoId = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

    [Fact(DisplayName = "O pedido e o resultado serializados pelo Wolverine do verificador são iguais ao corpo de referência")]
    public void Serializar_PedidoEResultadoReais_IguaisAoCorpoDeReferencia()
    {
        IMessageSerializer serializador = SerializadorDaComposicaoCompleta();

        string[] obtido = [.. Mensagens().Select(m => $"{m.Nome} {Corpo(serializador, m.Mensagem)}")];

        obtido.Should().Equal(File.ReadAllLines(Path.Join(AppContext.BaseDirectory, "Fixtures", ArquivoDeReferencia)));
    }

    internal static IEnumerable<(string Nome, object Mensagem)> Mensagens() =>
        [.. Pedidos(), .. ResultadosPorVeredictoEMotivo(), .. ResultadosVerificadosPorFormato()];

    private static IEnumerable<(string Nome, object Mensagem)> Pedidos() =>
        from destino in Enum.GetValues<DestinoDoResultado>()
        from formato in Enum.GetValues<FormatoDoArquivo>()
        select (
            $"pedido/{destino}/{formato}",
            (object)new PedidoDeVerificacaoDeArquivo(ArquivoId, "envio/chave", "\"etag-confirmado\"", "definitivo/chave", formato, 20L * 1024 * 1024, destino));

    private static IEnumerable<(string Nome, object Mensagem)> ResultadosPorVeredictoEMotivo() =>
        from veredicto in Enum.GetValues<VeredictoDaVerificacao>()
        from motivo in Enum.GetValues<MotivoDaRecusa>()
        select (
            $"resultado/{veredicto}/{motivo}/sem-formato",
            (object)new ResultadoDaVerificacaoDeArquivo(ArquivoId, veredicto, motivo, null, null, null));

    private static IEnumerable<(string Nome, object Mensagem)> ResultadosVerificadosPorFormato() =>
        from formato in Enum.GetValues<FormatoDoArquivo>()
        select (
            $"resultado/Verificado/{formato}",
            (object)new ResultadoDaVerificacaoDeArquivo(ArquivoId, VeredictoDaVerificacao.Verificado, MotivoDaRecusa.NaoSeAplica, "ab12", 2048, formato));

    private static string Corpo(IMessageSerializer serializador, object mensagem) =>
        Encoding.UTF8.GetString(serializador.WriteMessage(mensagem));

    private static IMessageSerializer SerializadorDaComposicaoCompleta()
    {
        using IHost host = PontoDeEntradaDoVerificador.ConstruirSemIniciar(PontoDeEntradaDoVerificador.ConfiguracaoMinima);

        return host.Services.GetRequiredService<IWolverineRuntime>().Options.DefaultSerializer;
    }
}
