namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.AdulteracaoDeEnvelopeDeTeste;

/// <summary>
/// Uma etapa do envelope com dois defeitos ao mesmo tempo é recusada pelo primeiro na ordem em que a leitura
/// confere a etapa: forma da etapa, forma dos produtos, forma e regra da janela, tipos de decisão, forma das
/// bancas, banca duplicada, produto duplicado e, por fim, os recursos. A ordem decide qual recusa o cadastro devolve, e a
/// leitura dividida em passos não pode trocá-la.
/// </summary>
public sealed class EnvelopeCodecRecusaEtapaComDefeitosSimultaneosTests
{
    [Fact(DisplayName = "Produto sem chave e janela invertida: recusa o produto")]
    public void ProdutoSemChave_JanelaInvertida_RecusaOProduto()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            InverterAJanela(PrimeiraEtapa(envelope));
            PrimeiraEtapa(envelope)["produtos"]![0]!.AsObject().Remove("atoCodigo");
        });

        ConferirRecusaEm(resultado, "etapas[0].produtos[0]");
    }

    [Fact(DisplayName = "Janela invertida e banca com o código repetido: recusa a janela")]
    public void JanelaInvertida_BancaRepetida_RecusaAJanela()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            InverterAJanela(PrimeiraEtapa(envelope));
            RepetirOCodigoDaBanca(PrimeiraEtapa(envelope));
        });

        ConferirRecusaEm(resultado, "etapas[0]", "a janela da etapa está invertida");
    }

    [Fact(DisplayName = "Janela invertida e tipos de decisão fora de ordem: recusa a janela")]
    public void JanelaInvertida_TiposForaDeOrdem_RecusaAJanela()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            InverterAJanela(PrimeiraEtapa(envelope));
            DesordenarOsTiposDeDecisao(PrimeiraEtapa(envelope));
        });

        ConferirRecusaEm(resultado, "etapas[0]", "a janela da etapa está invertida");
    }

    [Fact(DisplayName = "Tipos de decisão fora de ordem e banca com o código repetido: recusa os tipos")]
    public void TiposForaDeOrdem_BancaRepetida_RecusaOsTipos()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            DesordenarOsTiposDeDecisao(PrimeiraEtapa(envelope));
            RepetirOCodigoDaBanca(PrimeiraEtapa(envelope));
        });

        ConferirRecusaEm(resultado, "etapas[0].tiposDeDecisao");
    }

    [Fact(DisplayName = "Banca com o código repetido e produto repetido: recusa as bancas")]
    public void BancaRepetida_ProdutoRepetido_RecusaAsBancas()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            RepetirOCodigoDaBanca(PrimeiraEtapa(envelope));
            RepetirOProduto(PrimeiraEtapa(envelope));
        });

        ConferirRecusaEm(resultado, "etapas[0].bancas");
    }

    [Fact(DisplayName = "Produto repetido e recurso ancorado em produto que não existe: recusa os produtos")]
    public void ProdutoRepetido_AncoraSemProduto_RecusaOsProdutos()
    {
        Result<EnvelopeReidratado> resultado = ReidratarComEnvelopeAdulterado(envelope =>
        {
            RepetirOProduto(PrimeiraEtapa(envelope));
            PrimeiraEtapa(envelope)["recursos"]![0]!["produtoAncoraId"] = Guid.CreateVersion7().ToString();
        });

        ConferirRecusaEm(resultado, "etapas[0].produtos");
    }

    private static void ConferirRecusaEm(Result<EnvelopeReidratado> resultado, string caminho, string? detalhe = null)
    {
        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        resultado.Error.Message.Should().Contain($"'{caminho}'");
        if (detalhe is not null)
        {
            resultado.Error.Message.Should().Contain(detalhe);
        }
    }

    private static JsonObject PrimeiraEtapa(JsonObject envelope) => envelope["etapas"]!.AsArray()[0]!.AsObject();

    private static void InverterAJanela(JsonObject etapa)
    {
        DateTimeOffset inicio = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
        etapa["inicio"] = HashCanonicalComputer.SerializeInstantCanonical(inicio);
        etapa["fim"] = HashCanonicalComputer.SerializeInstantCanonical(inicio.AddDays(-1));
    }

    private static void DesordenarOsTiposDeDecisao(JsonObject etapa) =>
        etapa["tiposDeDecisao"] = new JsonArray("NOTA_DA_ETAPA", "ANALISE_DOCUMENTAL");

    private static void RepetirOCodigoDaBanca(JsonObject etapa)
    {
        JsonArray bancas = etapa["bancas"]!.AsArray();
        bancas[1]!["codigo"] = bancas[0]!["codigo"]!.GetValue<string>();
    }

    private static void RepetirOProduto(JsonObject etapa)
    {
        JsonArray produtos = etapa["produtos"]!.AsArray();
        produtos[1]!["atoCodigo"] = produtos[0]!["atoCodigo"]!.GetValue<string>();
        produtos[1]!["papel"] = produtos[0]!["papel"]!.GetValue<string>();
    }
}
