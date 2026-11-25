namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CausasDaCascataNoEnvelopeTests
{
    [Fact]
    public void LerCascataRemanejamento_FallbackESemDestinos_PropagaAmbas()
    {
        JsonObject documento = Documento("VALOR_FICTICIO_X!", new JsonArray());
        LeitorEnvelope leitor = new();
        LeituraDaCascata.LerCascataRemanejamento(leitor, documento);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(2);
        ConferirCausaNoDocumento.Conferir(documento, causas[0], "ordemDeRemanejamento", "ConfiguracaoCascataRemanejamento.FallbackObrigatorio", "Informe o código do destino final com até 60 caracteres, começando com letra maiúscula e usando letras maiúsculas, números ou sublinhados isolados.");
        ConferirCausaNoDocumento.Conferir(documento, causas[1], "ordemDeRemanejamento", "ConfiguracaoCascataRemanejamento.SemDestinos", "A ordem de remanejamento precisa de ao menos um destino.");
        causas.Should().OnlyContain(causa => !causa.Error.Message.Contains("VALOR_FICTICIO_X") && !causa.Field!.Contains("VALOR_FICTICIO_X"));
    }

    [Fact]
    public void LerCascataRemanejamento_OrigemEDestinoInvalidos_ApontaItem()
    {
        JsonArray ordens = new(new JsonObject { ["origem"] = "VALOR_FICTICIO_X!", ["destinos"] = new JsonArray("destino_invalido") });
        JsonObject documento = Documento("AC", ordens);
        LeitorEnvelope leitor = new();
        LeituraDaCascata.LerCascataRemanejamento(leitor, documento);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(2);
        ConferirCausaNoDocumento.Conferir(documento, causas[0], "ordemDeRemanejamento.ordens[0]", "ConfiguracaoCascataRemanejamento.CodigoInvalido", "Código de origem inválido — deve começar com letra maiúscula, usar letras maiúsculas, números ou sublinhados isolados e ter no máximo 60 caracteres.");
        ConferirCausaNoDocumento.Conferir(documento, causas[1], "ordemDeRemanejamento.ordens[0]", "ConfiguracaoCascataRemanejamento.CodigoInvalido", "Código de destino inválido — deve começar com letra maiúscula, usar letras maiúsculas, números ou sublinhados isolados e ter no máximo 60 caracteres.");
        causas.Should().OnlyContain(causa => !causa.Error.Message.Contains("VALOR_FICTICIO_X") && !causa.Field!.Contains("VALOR_FICTICIO_X")
            && !causa.Error.Message.Contains("destino_invalido") && !causa.Field.Contains("destino_invalido"));
    }

    private static JsonObject Documento(string destinoFinal, JsonArray ordens) => new()
    {
        ["ordemDeRemanejamento"] = new JsonObject
        {
            ["presente"] = true,
            ["destinoFinalCodigo"] = destinoFinal,
            ["ordens"] = ordens,
            ["regra"] = new JsonObject { ["codigo"] = "REMANEJAMENTO_ORDEM_LEI_12711", ["versao"] = "v1", ["hash"] = new string('a', 64) },
        },
    };
}
