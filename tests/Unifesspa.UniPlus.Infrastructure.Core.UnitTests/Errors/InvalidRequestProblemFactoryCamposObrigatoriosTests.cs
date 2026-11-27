namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Errors;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

using static ApoioDaRecusaDeRequisicao;

/// <summary>
/// O texto não anulável omitido ou <c>null</c> reprova no <c>ModelState</c> só pela obrigatoriedade
/// implícita do ASP.NET, e a fábrica o reconhece pela descrição do tipo do corpo, sem ler a mensagem
/// do framework.
/// </summary>
public sealed class InvalidRequestProblemFactoryCamposObrigatoriosTests
{
    private const string ChaveDoFatoNoItem = "[0].Documento.Aplicabilidade.Condicoes[0].Fato";

    [Fact(DisplayName = "Campo de item de lista omitido responde 400 com o código de campo obrigatório e o ponteiro em camelCase com índice")]
    public void TryBuild_CampoDeItemDeLista_RespondeComPonteiroNoPayload()
    {
        ActionContext contexto = ContextoDoCorpo(typeof(IReadOnlyList<ItemDoCorpo>), ChaveDoFatoNoItem);

        ProblemDetails problema = Executar(contexto);

        problema.Status.Should().Be(400);
        problema.Extensions["code"].Should().Be("uniplus.requisicao.campo_obrigatorio_ausente");
        problema.Extensions["traceId"].Should().NotBeNull();
        problema.Detail.Should().Be(
            "A requisição não traz valor para o campo obrigatório '[0].documento.aplicabilidade.condicoes[0].fato'.");
        ItensDe(problema).Should().ContainSingle().Which.Field.Should().Be("[0].documento.aplicabilidade.condicoes[0].fato");
    }

    [Fact(DisplayName = "Cada campo recusado vira um item de errors com o mesmo código, e o detail os enumera")]
    public void TryBuild_VariosCampos_UmItemPorCampoEDetailEnumerado()
    {
        ActionContext contexto = ContextoDoCorpo(typeof(ItemDoCorpo), "Orgao", "Documento.Titulo");

        ProblemDetails problema = Executar(contexto);

        problema.Detail.Should().Be("A requisição não traz valor para os campos obrigatórios 'orgao' e 'documento.titulo'.");
        ItemDeErroDeCampo[] itens = ItensDe(problema);
        itens.Select(static i => i.Field).Should().Equal("orgao", "documento.titulo");
        itens.Should().OnlyContain(static i => i.Code == "uniplus.requisicao.campo_obrigatorio_ausente");
    }

    [Fact(DisplayName = "A chave prefixada com o nome do parâmetro do corpo aponta o mesmo campo")]
    public void TryBuild_ChaveComNomeDoParametro_RetiraOPrefixo()
    {
        ActionContext contexto = ContextoDoCorpo(typeof(ItemDoCorpo), "comando.Orgao");

        ProblemDetails problema = Executar(contexto);

        ItensDe(problema).Should().ContainSingle().Which.Field.Should().Be("orgao");
    }

    [Fact(DisplayName = "A resposta não carrega o nome de tipo do corpo nem a mensagem do framework")]
    public void TryBuild_Resposta_NaoVazaTipoNemMensagemDoFramework()
    {
        ActionContext contexto = ContextoDoCorpo(typeof(ItemDoCorpo), "Orgao");

        string json = JsonSerializer.Serialize(Executar(contexto));

        json.Should().NotContain("ItemDoCorpo").And.NotContain("field is required").And.NotContain("Unifesspa");
    }

    [Fact(DisplayName = "Campo obrigatório junto de outra falha não reconhecida delega à cadeia")]
    public void TryBuild_MisturaComOutraFalha_Delega()
    {
        ActionContext contexto = ContextoDoCorpo(typeof(ItemDoCorpo), "Orgao", "ChaveQueNaoExiste");

        InvalidRequestProblemFactory.TryBuild(contexto).Should().BeNull();
    }

    [Theory(DisplayName = "Propriedade que tem outra validação ou aceita ausência delega à cadeia")]
    [InlineData("Documento.Aplicabilidade.Condicoes[0].Limitada")]
    [InlineData("Documento.Aplicabilidade.Condicoes[0].ComMensagem")]
    [InlineData("Observacao")]
    [InlineData("Documento.Aplicabilidade.Condicoes[0]")]
    public void TryBuild_PropriedadeComOutraValidacao_Delega(string chave)
    {
        ActionContext contexto = ContextoDoCorpo(typeof(ItemDoCorpo), chave);

        InvalidRequestProblemFactory.TryBuild(contexto).Should().BeNull();
    }

    private static ActionContext ContextoDoCorpo(Type tipoDoCorpo, params string[] chavesRecusadas)
    {
        ActionContext contexto = Contexto(ParametroDeCorpo("comando", tipoDoCorpo));
        foreach (string chave in chavesRecusadas)
        {
            contexto.ModelState.AddModelError(chave, $"The {chave} field is required.");
        }

        return contexto;
    }

    private static ItemDeErroDeCampo[] ItensDe(ProblemDetails problema) =>
        [.. ((IEnumerable<object>)problema.Extensions["errors"]!).Cast<ItemDeErroDeCampo>()];
}
