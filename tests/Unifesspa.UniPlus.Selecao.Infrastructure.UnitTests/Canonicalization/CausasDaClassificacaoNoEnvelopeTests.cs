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
        (JsonObject documento, IReadOnlyList<FieldError> causas) = LerQuadro("""
            {"quadroPesoAreaEnem":[{"grupoAreaEnem":{"codigo":"GRUPO","rotulo":"Grupo"},"baseLegal":"Base fictícia",
            "areas":[{"codigo":"AREA","rotulo":"VALOR_FICTICIO_X","peso":"-1.0000","corte":"-1.0000"}]}]}
            """);

        causas.Select(causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("classificacao.quadroPesoAreaEnem[0].areas[0].peso", "GrupoPesoAreaEnemCongelado.PesoNegativo"),
            ("classificacao.quadroPesoAreaEnem[0].areas[0].corte", "GrupoPesoAreaEnemCongelado.CorteForaDaFaixa"));
        ConferirCaminhosSemOValor(documento, causas, "VALOR_FICTICIO_X");
    }

    [Fact(DisplayName = "Grupo, código da área e rótulo da área inválidos no documento voltam juntos, cada um no seu caminho, sem repetir o valor escrito")]
    public void LerQuadroPesoAreaEnem_GrupoEAreaInvalidos_PropagaTodasSemRepetirOValor()
    {
        (JsonObject documento, IReadOnlyList<FieldError> causas) = LerQuadro("""
            {"quadroPesoAreaEnem":[{"grupoAreaEnem":{"codigo":"GRUPO-FICTICIO-\u0000-X","rotulo":"ROTULO-FICTICIO-\u0000-X"},"baseLegal":"Base fictícia",
            "areas":[{"codigo":"AREA-FICTICIA-\u0000-X","rotulo":"AREA-FICTICIA-\u0000-Y","peso":"1.0000","corte":null}]}]}
            """);

        causas.Select(causa => (causa.Field, causa.Error.Code)).Should().Equal(
            ("classificacao.quadroPesoAreaEnem[0].grupoAreaEnem.codigo", "GrupoAreaEnemSnapshot.CaractereInvalido"),
            ("classificacao.quadroPesoAreaEnem[0].grupoAreaEnem.rotulo", "GrupoAreaEnemSnapshot.CaractereInvalido"),
            ("classificacao.quadroPesoAreaEnem[0].areas[0].codigo", "GrupoPesoAreaEnemCongelado.AreaInvalida"),
            ("classificacao.quadroPesoAreaEnem[0].areas[0].rotulo", "GrupoPesoAreaEnemCongelado.RotuloDaAreaInvalido"));
        ConferirCaminhosSemOValor(documento, causas, "FICTICI");
    }

    private static (JsonObject Documento, IReadOnlyList<FieldError> Causas) LerQuadro(string classificacao)
    {
        JsonObject bloco = JsonNode.Parse(classificacao)!.AsObject();
        LeitorEnvelope leitor = new();
        LeituraDaClassificacao.LerQuadroPesoAreaEnem(leitor, bloco);
        return (new JsonObject { ["classificacao"] = bloco }, leitor.Falha<object>().Errors);
    }

    private static void ConferirCaminhosSemOValor(JsonObject documento, IReadOnlyList<FieldError> causas, string valor)
    {
        foreach (FieldError causa in causas)
        {
            CaminhoNoDocumento.Existe(documento, causa.Field!).Should().BeTrue();
            causa.Field.Should().NotContain(valor);
            causa.Error.Code.Should().NotContain(valor);
            causa.Error.Message.Should().NotContain(valor);
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
