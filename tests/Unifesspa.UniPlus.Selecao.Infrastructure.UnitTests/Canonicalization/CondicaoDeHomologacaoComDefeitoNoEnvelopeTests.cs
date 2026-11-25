namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class CondicaoDeHomologacaoComDefeitoNoEnvelopeTests
{
    [Fact]
    public void Ler_CondicaoEEnquadramentoInvalidos_AcumulaCausasNaOrdem()
    {
        JsonObject condicao = new() { ["tipo"] = "RESULTADO_NO_RETORNO_DO_INEP", ["edicaoDoExame"] = CondicaoDeHomologacaoComDefeitoDeTeste.Edicao, ["motivoDesfavoravelCodigo"] = CondicaoDeHomologacaoComDefeitoDeTeste.Motivo };
        condicao["enquadramento"] = new JsonArray(new JsonArray(CondicoesNoEnvelope.Condicao("ALGUM_DE", JsonValue.Create(CondicaoDeHomologacaoComDefeitoDeTeste.Valor))));
        JsonObject fase = new() { ["regraDeHomologacao"] = new JsonObject { ["resultadoAtoCodigo"] = null, ["alternativas"] = new JsonArray(new JsonObject { ["condicoes"] = new JsonArray(condicao) }) } };
        LeitorEnvelope leitor = new();
        RegraDeHomologacaoNoEnvelope.Ler(leitor, fase, "fase");
        leitor.Falha<object>().Errors.Select(erro => (erro.Error.Code, erro.Field)).Should().Equal(
            CondicaoDeHomologacaoComDefeitoDeTeste.Causas.Select(causa => (causa.Codigo, (string?)("fase.regraDeHomologacao.alternativas[0].condicoes[0]." + causa.Campo))));
    }
}
