namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CausasDaDerivacaoNoEnvelopeTests
{
    [Fact]
    public void LerRegrasDaDerivacao_OrdemNegativa_SituaCausaNaOrdem()
    {
        JsonObject configuracao = new()
        {
            ["regras"] = new JsonArray(new JsonObject
            {
                ["ordem"] = -1,
                ["contribui"] = null,
                ["quando"] = null,
            })
        };
        JsonObject documento = new() { ["configuracao"] = configuracao };
        LeitorEnvelope leitor = new();
        LeituraDaDerivacao.LerRegrasDaDerivacao(leitor, configuracao, "configuracao");
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().ContainSingle();
        FieldError causa = causas[0];
        causa.Should().Be(new FieldError("configuracao.regras[0].ordem", new DomainError(
            "RegraDerivacaoConfigurada.OrdemInvalida", "A ordem da regra não pode ser negativa.")));
        CaminhoNoDocumento.Existe(documento, causa.Field!).Should().BeTrue();
        causa.Error.Message.Should().NotContain("-1");
        causa.Field.Should().NotContain("-1");
    }
    [Fact]
    public void LerRegrasDerivacao_OrdemDuplicadaEContribuicoesMistas_PropagaAmbas()
    {
        JsonObject documento = JsonNode.Parse("""
            {"calculosDosDados":[{"codigoDoDado":"VALOR_FICTICIO_X","regras":[
            {"ordem":0,"contribui":null,"quando":null},
            {"ordem":0,"contribui":"VALOR_FICTICIO_X","quando":null}]}]}
            """)!.AsObject();
        LeitorEnvelope leitor = new();
        LeituraDaDerivacao.LerRegrasDerivacao(leitor, documento);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(2);
        ConferirCausaNoDocumento.Conferir(documento, causas[0], "calculosDosDados[0].regras", "ConfiguracaoDerivacaoFato.OrdemRegraDuplicada", "A ordem de uma regra é usada por mais de uma regra na mesma derivação.");
        ConferirCausaNoDocumento.Conferir(documento, causas[1], "calculosDosDados[0].regras", "ConfiguracaoDerivacaoFato.RegrasComESemCodigo", "As regras de uma derivação contribuem código todas, no derivado categórico, ou nenhuma, no derivado booleano.");
        causas.Should().OnlyContain(causa => !causa.Error.Message.Contains("VALOR_FICTICIO_X") && !causa.Field!.Contains("VALOR_FICTICIO_X"));
    }
}
