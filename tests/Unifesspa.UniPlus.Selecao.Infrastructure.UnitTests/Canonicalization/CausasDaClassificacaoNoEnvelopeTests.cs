namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CausasDaClassificacaoNoEnvelopeTests
{
    [Fact]
    public void LerCriteriosDesempate_OrdemEIdadeInvalidas_PropagaAmbas()
    {
        JsonObject documento = DocumentoComCriterio();
        LeitorEnvelope leitor = new();
        LeituraDoDesempate.LerCriteriosDesempate(leitor, documento);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().Equal(
            new FieldError("criteriosDesempate[0].ordem", new DomainError("CriterioDesempate.OrdemInvalida", "A ordem do critério de desempate deve ser maior que zero.")),
            new FieldError("criteriosDesempate[0]", new DomainError("CriterioDesempate.IdadeMinimaInvalida", "A idade mínima do critério IDOSO deve ser maior que zero.")));
        causas.Should().OnlyContain(causa => CaminhoNoDocumento.Existe(documento, causa.Field!));
    }

    [Fact]
    public void LerClassificacao_CalculoLocalSemArredondamento_PropagaCausas()
    {
        JsonObject documento = DocumentoClassificacao();
        LeitorEnvelope leitor = new();
        LeituraDaClassificacao.LerClassificacao(leitor, documento);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(2);
        ConferirCausaNoDocumento.Conferir(documento, causas[0], "classificacao", "ConfiguracaoClassificacao.ArredondamentoObrigatorio", "Cálculo local exige regra de arredondamento.");
        ConferirCausaNoDocumento.Conferir(documento, causas[1], "classificacao.casasArredondamento", "ConfiguracaoClassificacao.CasasArredondamentoObrigatorio", "Cálculo local exige casas decimais de arredondamento maior que zero.");
    }

    [Fact]
    public void LerClassificacao_NotaMinimaNegativa_ApontaRegraDeEliminacao()
    {
        JsonObject documento = DocumentoClassificacao();
        documento["classificacao"]!["regrasEliminacao"] = new JsonArray(new JsonObject
        {
            ["regra"] = Referencia(RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa.Codigo()),
            ["parametros"] = new JsonObject { ["etapaId"] = Guid.NewGuid().ToString(), ["notaMinima"] = "-1.0000" },
        });
        LeitorEnvelope leitor = new();
        LeituraDaClassificacao.LerClassificacao(leitor, documento);
        FieldError causa = leitor.Falha<object>().Errors.Single();
        ConferirCausaNoDocumento.Conferir(documento, causa, "classificacao.regrasEliminacao[0]", "RegraEliminacao.NotaMinimaInvalida", "A nota mínima da eliminação deve ser não negativa.");
        causa.Error.Message.Should().NotContain("-1.0000");
        causa.Field.Should().NotContain("-1.0000");
    }

    [Fact]
    public void LerQuadroPesoAreaEnem_PesoECorteInvalidos_PropagaAmbas()
    {
        JsonObject bloco = JsonNode.Parse("""
            {"quadroPesoAreaEnem":[{"grupoAreaEnem":{"codigo":"GRUPO","rotulo":"Grupo"},"baseLegal":"Base fictícia",
            "areas":[{"codigo":"AREA","rotulo":"Matemática","peso":"-1.0000","corte":"-1.0000"}]}]}
            """)!.AsObject();
        JsonObject documento = new() { ["classificacao"] = bloco };
        LeitorEnvelope leitor = new();
        LeituraDaClassificacao.LerQuadroPesoAreaEnem(leitor, bloco);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(2);
        causas[0].Field.Should().Be("classificacao.quadroPesoAreaEnem[0].areas[0].peso");
        causas[0].Error.Code.Should().Be("GrupoPesoAreaEnemCongelado.PesoNegativo");
        causas[1].Field.Should().Be("classificacao.quadroPesoAreaEnem[0].areas[0].corte");
        causas[1].Error.Code.Should().Be("GrupoPesoAreaEnemCongelado.CorteForaDaFaixa");
        // A mensagem do domínio repete o rótulo; a correção está na api#2843.
        foreach (FieldError causa in causas)
        {
            CaminhoNoDocumento.Existe(documento, causa.Field!).Should().BeTrue();
            causa.Field.Should().NotContain("VALOR_FICTICIO_X");
            causa.Error.Code.Should().NotContain("VALOR_FICTICIO_X");
            causa.Error.Message.Should().NotContain("VALOR_FICTICIO_X");
        }
    }

    private static JsonObject DocumentoClassificacao() => new()
    {
        ["classificacao"] = new JsonObject
        {
            ["regraCalculo"] = Referencia(RegraDeCalculoDaNota.MediaPonderada.Codigo()),
            ["regraArredondamento"] = null,
            ["casasArredondamento"] = null,
            ["escalaNotaFinal"] = null,
            ["regraOrdemAlocacao"] = Referencia(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo()),
            ["regrasEliminacao"] = new JsonArray(),
            ["baseadoEmEnem"] = false,
            ["resolucaoPesoAreaEnem"] = null,
            ["quadroPesoAreaEnem"] = new JsonArray(),
        },
    };

    private static JsonObject Referencia(string codigo) => new() { ["codigo"] = codigo, ["versao"] = "v1", ["hash"] = new string('a', 64) };

    private static JsonObject DocumentoComCriterio() => new()
    {
        ["criteriosDesempate"] = new JsonArray(new JsonObject
        {
            ["ordem"] = 0,
            ["regra"] = new JsonObject { ["codigo"] = RegraDeDesempate.Idoso.Codigo(), ["versao"] = "v1", ["hash"] = new string('a', 64) },
            ["parametros"] = new JsonObject { ["idadeMinima"] = 0 },
        }),
    };
}
