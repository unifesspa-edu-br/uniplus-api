namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CausasDaProvaPresencialNoEnvelopeTests
{
    [Fact]
    public void Ler_CodigoInvalido_ApontaCodigoDoMunicipio()
    {
        JsonObject documento = Documento(true);
        LeitorEnvelope leitor = new();
        LeituraDaProvaPresencial.Ler(leitor, documento);
        FieldError causa = leitor.Falha<object>().Errors.Single();
        ConferirCausaNoDocumento.Conferir(documento, causa, "provaPresencial.municipios[0].codigoIbge", "CidadeReferencia.CodigoIbgeFormatoInvalido", "O código do IBGE deve ter sete dígitos e o prefixo de uma UF.");
        causa.Error.Message.Should().NotContain("VALOR_FICTICIO_X");
        causa.Field.Should().NotContain("VALOR_FICTICIO_X");
    }

    [Fact]
    public void Ler_SemProvaECodigoInvalido_PropagaAmbas()
    {
        JsonObject documento = Documento(false);
        LeitorEnvelope leitor = new();
        LeituraDaProvaPresencial.Ler(leitor, documento);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(2);
        ConferirCausaNoDocumento.Conferir(documento, causas[0], "provaPresencial.municipios", "ProvaPresencial.MunicipiosSemProvaPresencial", "O processo não declara prova presencial e não pode declarar municípios de prova.");
        ConferirCausaNoDocumento.Conferir(documento, causas[1], "provaPresencial.municipios[0].codigoIbge", "CidadeReferencia.CodigoIbgeFormatoInvalido", "O código do IBGE deve ter sete dígitos e o prefixo de uma UF.");
        causas.Should().OnlyContain(causa => !causa.Error.Message.Contains("VALOR_FICTICIO_X") && !causa.Field!.Contains("VALOR_FICTICIO_X"));
    }

    private static JsonObject Documento(bool temProva) => new()
    {
        ["provaPresencial"] = new JsonObject
        {
            ["temProvaPresencial"] = temProva,
            ["municipios"] = new JsonArray(new JsonObject { ["codigoIbge"] = "VALOR_FICTICIO_X", ["uf"] = "PA", ["nome"] = "Cidade fictícia" }),
        },
    };
}
