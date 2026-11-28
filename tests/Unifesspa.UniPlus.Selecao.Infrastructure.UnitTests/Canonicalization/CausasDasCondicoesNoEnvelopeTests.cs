namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CausasDasCondicoesNoEnvelopeTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void LerObrigatoriedadeRecusandoNaHora_CondicoesInvalidas_PropagaCausas(bool separadas, bool unica)
    {
        JsonObject documento = new() { ["obrigatoriedade"] = new JsonObject { ["tipo"] = "QUANDO", ["quando"] = CondicoesNoEnvelope.Invalidas(separadas, unica) } };
        LeitorEnvelope leitor = new();
        LeituraDasCondicoes.LerObrigatoriedadeRecusandoNaHora(leitor, documento, "item");
        CondicoesNoEnvelope.Conferir(new JsonObject { ["item"] = documento }, leitor, "item.obrigatoriedade.quando", separadas, unica);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void LerImpedimento_CondicoesInvalidas_PropagaCausas(bool separadas, bool unica)
    {
        JsonObject documento = new() { ["impedimento"] = new JsonObject { ["mensagem"] = "Mensagem", ["quando"] = CondicoesNoEnvelope.Invalidas(separadas, unica) } };
        LeitorEnvelope leitor = new();
        LeituraDasCondicoes.LerImpedimento(leitor, documento, "item");
        CondicoesNoEnvelope.Conferir(new JsonObject { ["item"] = documento }, leitor, "item.impedimento.quando", separadas, unica);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void LerCondicaoGatilho_CondicoesInvalidas_PropagaCausas(bool separadas, bool unica)
    {
        JsonObject item = new() { ["condicoes"] = CondicoesNoEnvelope.Invalidas(separadas, unica) };
        LeitorEnvelope leitor = new();
        LeituraDasCondicoes.LerCondicaoGatilho(leitor, item, "item");
        CondicoesNoEnvelope.Conferir(new JsonObject { ["item"] = item }, leitor, "item.condicoes", separadas, unica);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[[]]")]
    public void LerCondicaoGatilho_ClausulasVazias_Recusa(string condicoes)
    {
        JsonObject item = new() { ["condicoes"] = JsonNode.Parse(condicoes) };
        LeitorEnvelope leitor = new();
        LeituraDasCondicoes.LerCondicaoGatilho(leitor, item, "item");
        leitor.Falhou.Should().BeTrue();
    }

    [Fact]
    public void LerCondicaoGatilho_ValorInvalidoAntesDeOperadorDesconhecido_RecusaOperador()
    {
        JsonObject item = new()
        {
            ["condicoes"] = new JsonArray(
                new JsonArray(CondicoesNoEnvelope.Condicao("IGUAL", new JsonArray("VALOR_FICTICIO_X"))),
                new JsonArray(CondicoesNoEnvelope.Condicao("DESCONHECIDO", JsonValue.Create(true)))),
        };
        LeitorEnvelope leitor = new();

        LeituraDasCondicoes.LerCondicaoGatilho(leitor, item, "item");

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Should().Be(new DomainError("EnvelopeCodec.EnvelopeMalformado",
            "Envelope malformado em 'item.condicoes[1][0].operador': o operador não é um dos códigos aceitos: IGUAL, ALGUM_DE, MAIOR_IGUAL, MENOR_IGUAL, DIFERENTE, NENHUM_DE."));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void LerRegrasDaDerivacao_CondicoesInvalidas_PropagaCausas(bool separadas, bool unica)
    {
        JsonObject configuracao = new() { ["regras"] = new JsonArray(new JsonObject { ["ordem"] = 0, ["contribui"] = null, ["quando"] = CondicoesNoEnvelope.Invalidas(separadas, unica) }) };
        LeitorEnvelope leitor = new();
        LeituraDaDerivacao.LerRegrasDaDerivacao(leitor, configuracao, "configuracao");
        CondicoesNoEnvelope.Conferir(new JsonObject { ["configuracao"] = configuracao }, leitor, "configuracao.regras[0].quando", separadas, unica);
    }

    [Fact]
    public void LerParametrosDesempate_ValorInvalido_SituaCausa()
    {
        JsonObject parametros = CondicoesNoEnvelope.Condicao("IGUAL", new JsonArray("VALOR_FICTICIO_X"));
        LeitorEnvelope leitor = new();
        LeituraDoDesempate.LerParametrosDesempate(leitor, RegraDeDesempate.CondicaoSobreDado, parametros, "parametros");
        CondicoesNoEnvelope.ConferirCausa(new JsonObject { ["parametros"] = parametros }, leitor.Falha<object>().Errors[0], "parametros.valor", false);
    }

    [Fact]
    public void PredicadoOpcional_CondicaoIntegra_PreservaPredicado()
    {
        JsonObject item = new() { ["quando"] = new JsonArray(new JsonArray(CondicoesNoEnvelope.Condicao("IGUAL", JsonValue.Create(true)))) };
        LeitorEnvelope leitor = new();
        PredicadoDnf? predicado = LeituraDasCondicoes.PredicadoOpcional(LeituraDasCondicoes.LerDnf(leitor, item, "quando", "item")).Value;
        predicado.Should().NotBeNull();
        CondicaoDnf condicao = CondicaoDnf.Criar("DADO", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;
        PredicadoDnf anterior = PredicadoDnf.CriarDeCondicoesAgrupadas([(0, condicao)]).Value!;
        predicado!.MesmoConteudo(anterior).Should().BeTrue();
    }
}
