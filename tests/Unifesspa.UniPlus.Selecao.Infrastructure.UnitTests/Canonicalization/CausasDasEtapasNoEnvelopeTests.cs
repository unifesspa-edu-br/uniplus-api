namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Cenarios;

public sealed class CausasDasEtapasNoEnvelopeTests
{
    [Fact]
    public void LerTipoDaEtapa_SemCarater_SituaCausaNoTipo()
    {
        JsonObject etapa = new()
        {
            ["tipoEtapa"] = new JsonObject
            {
                ["origemId"] = Guid.NewGuid().ToString(),
                ["codigo"] = "TIPO",
                ["nome"] = "Tipo fictício",
                ["admitePontuacao"] = false,
                ["admiteEliminacao"] = false,
                ["notaDeOrigemNoEnem"] = false,
                ["exigeCorrecaoIndependente"] = false,
            }
        };
        JsonObject documento = new() { ["etapa"] = etapa };
        LeitorEnvelope leitor = new();
        LeituraDoTipoDaEtapa.Ler(leitor, etapa, "etapa");
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().ContainSingle();
        FieldError causa = causas[0];
        causa.Should().Be(new FieldError("etapa.tipoEtapa", new DomainError(
            "TipoEtapaSnapshot.SemCaraterAdmitido", "A cópia congelada do tipo de etapa deve admitir compor a nota final, eliminar candidato, ou os dois.")));
        CaminhoNoDocumento.Existe(documento, causa.Field!).Should().BeTrue();
        causa.Error.Message.Should().NotContain("Tipo fictício");
        causa.Field.Should().NotContain("Tipo fictício");
    }
    [Fact]
    public void LerDeliberacao_ModoInvalido_SituaCausaNoModo()
    {
        JsonObject banca = BancaComModoInvalido();
        JsonObject documento = new() { ["banca"] = banca };
        LeitorEnvelope leitor = new();
        RegraDeDeliberacaoNoEnvelope.Ler(leitor, banca, "banca");
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().Equal(new FieldError("banca.regraDeDeliberacao.modo", new DomainError(
            "Banca.ModoDeDeliberacaoInvalido", "O modo de deliberação não é uma das opções disponíveis: colegiada com votação ou ato individual.")));
        CaminhoNoDocumento.Existe(documento, causas[0].Field!).Should().BeTrue();
        causas[0].Error.Message.Should().NotContain("VALOR_FICTICIO_X");
    }

    [Fact]
    public void LerEtapas_TipoDeDecisaoDesconhecido_RecusaNaLeituraDaLista()
    {
        JsonObject documentoOriginal = new() { ["etapas"] = SerializacaoDasEtapas.SerializarEtapas(ProcessoComEtapaBuilder.Criar()) };
        byte[] bytes = PerfilCanonicoV1.Instancia.Serializar(documentoOriginal);
        JsonObject documento = JsonNode.Parse(bytes)!.AsObject();
        documento["etapas"]![0]!["tiposDeDecisao"] = new JsonArray("VALOR_FICTICIO_X");
        documento["etapas"]![0]!["contexto"]!.AsObject().Remove("tiposDeDecisaoRotulos");
        LeitorEnvelope leitor = new();
        LeituraDasEtapas.LerEtapas(leitor, documento);
        leitor.Erro.Should().Be(new DomainError("EnvelopeCodec.EnvelopeMalformado", "Envelope malformado em 'etapas[0].tiposDeDecisao': os tipos de decisão devem ser do vocabulário, sem repetição e em ordem alfabética."));
        CaminhoNoDocumento.Existe(documento, "etapas[0].tiposDeDecisao").Should().BeTrue();
        leitor.Erro.Message.Should().NotContain("VALOR_FICTICIO_X");
    }

    [Fact]
    public void LerDeliberacao_ComposicaoInvalida_PropagaTodasAsCausas()
    {
        JsonObject banca = BancaComModoInvalido();
        banca["regraDeDeliberacao"]!["integrantes"] = 0;
        banca["regraDeDeliberacao"]!["suplentes"] = -1;
        JsonObject documento = new() { ["banca"] = banca };
        LeitorEnvelope leitor = new();
        RegraDeDeliberacaoNoEnvelope.Ler(leitor, banca, "banca");
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(3);
        ConferirCausaNoDocumento.Conferir(documento, causas[0], "banca.regraDeDeliberacao.modo", "Banca.ModoDeDeliberacaoInvalido", "O modo de deliberação não é uma das opções disponíveis: colegiada com votação ou ato individual.");
        ConferirCausaNoDocumento.Conferir(documento, causas[1], "banca.regraDeDeliberacao.integrantes", "Banca.IntegrantesInvalidos", "A banca deve ter no mínimo 1 integrante.");
        ConferirCausaNoDocumento.Conferir(documento, causas[2], "banca.regraDeDeliberacao.suplentes", "Banca.SuplentesInvalidos", "Os suplentes da banca não podem ser negativos.");
        causas[0].Error.Message.Should().NotContain("VALOR_FICTICIO_X");
        causas.Should().OnlyContain(causa => !causa.Field!.Contains("VALOR_FICTICIO_X"));
    }

    private static JsonObject BancaComModoInvalido() => new()
    {
        ["regraDeDeliberacao"] = new JsonObject
        {
            ["modo"] = "VALOR_FICTICIO_X",
            ["integrantes"] = 1,
            ["suplentes"] = 0,
            ["quorum"] = null,
            ["criterio"] = null,
            ["desempate"] = null,
            ["encerramento"] = null,
            ["semUnanimidade"] = null,
        },
    };
}
