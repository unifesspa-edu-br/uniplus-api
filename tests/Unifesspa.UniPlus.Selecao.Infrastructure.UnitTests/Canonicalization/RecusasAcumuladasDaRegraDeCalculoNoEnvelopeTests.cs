namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class RecusasAcumuladasDaRegraDeCalculoNoEnvelopeTests
{
    private const string Regra = "calculosDosDados[0].regras[0]";
    private const string Quando = Regra + ".quando";
    private static readonly DomainError ValorEscalarExigido = new("CondicaoDnf.FormaIncoerenteComOperador", "Os operadores IGUAL, DIFERENTE, MAIOR_IGUAL e MENOR_IGUAL exigem um valor JSON escalar não branco.");
    private static readonly DomainError ListaExigida = new("CondicaoDnf.FormaIncoerenteComOperador", "Os operadores ALGUM_DE e NENHUM_DE exigem um array JSON, sem itens de texto em branco (array vazio é aceito).");
    private static readonly DomainError OrdemNegativa = new("RegraDerivacaoConfigurada.OrdemInvalida", "A ordem da regra não pode ser negativa.");
    private static readonly DomainError ContribuiEmBranco = new("RegraDerivacaoConfigurada.ContribuiEmBranco", "O código contribuído pela regra não pode ficar em branco; omita-o só no derivado booleano.");

    [Fact]
    public void LerRegrasDerivacao_CondicaoInvalidaEOrdemNegativa_PropagaAmbas()
    {
        JsonObject regra = RegraComCondicoes(-1, "CATEGORIA_X", CondicoesNoEnvelope.Invalidas(false, true));
        Conferir(regra, [(Quando + "[0][0].valor", ValorEscalarExigido), (Regra + ".ordem", OrdemNegativa)]);
    }

    [Fact]
    public void LerRegrasDerivacao_CondicaoInvalidaEContribuiEmBranco_PropagaAmbas()
    {
        JsonObject regra = RegraComCondicoes(0, "   ", CondicoesNoEnvelope.Invalidas(false, true));
        Conferir(regra, [(Quando + "[0][0].valor", ValorEscalarExigido), (Regra + ".contribui", ContribuiEmBranco)]);
    }

    [Fact]
    public void LerRegrasDerivacao_DuasCondicoesInvalidas_RecusaUmaPorCondicao()
    {
        JsonObject regra = RegraComCondicoes(0, "CATEGORIA_X", CondicoesNoEnvelope.Invalidas(true));
        Conferir(regra, [(Quando + "[0][0].valor", ListaExigida), (Quando + "[1][0].valor", ValorEscalarExigido)]);
    }

    [Fact]
    public void LerRegrasDerivacao_CondicaoInvalidaEmRegraSimOuNaoComOrdemNegativa_PropagaAmbas()
    {
        JsonObject regra = RegraComCondicoes(-1, null, CondicoesNoEnvelope.Invalidas(false, true));
        Conferir(regra, [(Quando + "[0][0].valor", ValorEscalarExigido), (Regra + ".ordem", OrdemNegativa)]);
    }

    [Fact]
    public void LerRegrasDerivacao_SoCondicaoInvalida_NaoGeraRecusaExtra()
    {
        JsonObject regra = RegraComCondicoes(0, "CATEGORIA_X", CondicoesNoEnvelope.Invalidas(false, true));
        Conferir(regra, [(Quando + "[0][0].valor", ValorEscalarExigido)]);
    }

    [Fact]
    public void LerRegrasDerivacao_SoOrdemNegativa_MantemCodigoETexto()
    {
        JsonObject regra = RegraComCondicoes(-1, "CATEGORIA_X", null);
        Conferir(regra, [(Regra + ".ordem", OrdemNegativa)]);
    }

    [Fact]
    public void LerRegrasDerivacao_SoContribuiEmBranco_MantemCodigoETexto()
    {
        JsonObject regra = RegraComCondicoes(0, "   ", null);
        Conferir(regra, [(Regra + ".contribui", ContribuiEmBranco)]);
    }

    [Fact]
    public void LerRegrasDerivacao_FormatoDeChaveRecusadoComCondicaoInvalida_EncerraOItemSoComOFormato()
    {
        JsonObject regra = RegraComCondicoes(-1, "CATEGORIA_X", CondicoesNoEnvelope.Invalidas(false, true));
        regra["contribui"] = 7;
        JsonObject documento = Documento(regra);
        LeitorEnvelope leitor = new();
        LeituraDaDerivacao.LerRegrasDerivacao(leitor, documento);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().ContainSingle().Which.Error.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        causas[0].Error.Message.Should().Contain("contribui");
        CaminhoNoDocumento.Existe(documento, Regra + ".contribui").Should().BeTrue();
    }

    private static JsonObject RegraComCondicoes(int ordem, string? contribui, JsonArray? quando) => new()
    {
        ["ordem"] = ordem,
        ["contribui"] = contribui,
        ["quando"] = quando,
    };

    private static JsonObject Documento(JsonObject regra) => new()
    {
        ["calculosDosDados"] = new JsonArray(new JsonObject
        {
            ["codigoDoDado"] = "DADO_DERIVADO",
            ["regras"] = new JsonArray(regra),
        }),
    };

    private static void Conferir(JsonObject regra, (string Campo, DomainError Erro)[] esperadas)
    {
        JsonObject documento = Documento(regra);
        LeitorEnvelope leitor = new();
        LeituraDaDerivacao.LerRegrasDerivacao(leitor, documento);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Select(causa => (causa.Field!, causa.Error)).Should().Equal(esperadas);
        causas.Should().OnlyHaveUniqueItems();
        causas.Should().OnlyContain(causa => CaminhoNoDocumento.Existe(documento, causa.Field!));
        causas.Should().OnlyContain(causa => !causa.Error.Message.Contains("VALOR_FICTICIO_X") && !causa.Field!.Contains("VALOR_FICTICIO_X"));
    }
}
