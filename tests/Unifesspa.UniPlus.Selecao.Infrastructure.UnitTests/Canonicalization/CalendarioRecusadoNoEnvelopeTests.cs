namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CalendarioRecusadoNoEnvelopeTests
{
    [Fact]
    public void LerCalendarioDiasUteis_AbrangenciaInvalida_NaoRepeteValor()
    {
        const string valor = "ExemploAbrangenciaInvalida";
        JsonObject dia = new() { ["data"] = "2026-11-25", ["abrangencia"] = valor, ["municipioIbge"] = null, ["municipioNome"] = null, ["uf"] = null };
        JsonObject documento = Documento(new JsonArray(dia));
        LeitorEnvelope leitor = new();
        LeituraDoCalendario.LerCalendarioDiasUteis(leitor, documento, [], []);
        Conferir(leitor, valor, "'calendarioDiasUteis.diasNaoUteis[0]' congelado não é um dia não útil válido.");
    }

    [Fact]
    public void LerCalendarioDiasUteis_DiaRepetido_NaoRepeteValor()
    {
        const string valor = "2026-11-25";
        JsonObject dia = new() { ["data"] = valor, ["abrangencia"] = "NACIONAL", ["municipioIbge"] = null, ["municipioNome"] = null, ["uf"] = null };
        JsonObject documento = Documento(new JsonArray(dia, dia.DeepClone()));
        LeitorEnvelope leitor = new();
        LeituraDoCalendario.LerCalendarioDiasUteis(leitor, documento, [], []);
        Conferir(leitor, valor, "'calendarioDiasUteis' deve ter identificador de origem, versão de até 60 caracteres e ao menos um dia não útil, sem repetir data, abrangência e território.");
    }

    private static JsonObject Documento(JsonArray dias) => new()
    {
        ["calendarioDiasUteis"] = new JsonObject
        {
            ["presente"] = true,
            ["origemId"] = "11111111-1111-1111-1111-111111111111",
            ["versaoDoCalendario"] = "exemplo",
            ["diasNaoUteis"] = dias,
        }
    };

    private static void Conferir(LeitorEnvelope leitor, string valor, string mensagem)
    {
        FieldError causa = leitor.Falha<object>().Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Be(mensagem).And.NotContainEquivalentOf(valor);
        causa.Field.Should().BeNull();
    }
}
