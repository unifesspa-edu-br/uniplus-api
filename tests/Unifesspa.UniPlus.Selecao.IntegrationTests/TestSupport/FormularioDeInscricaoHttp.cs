namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Os itens do formulário de inscrição pelo HTTP, para os testes que não tratam da estrutura
/// dele: a rota administrativa dos itens e o corpo, com cada item sem etapa posto na seção de
/// dados do formulário mínimo — o item fora de seção é aceito no rascunho, mas trava a publicação.
/// </summary>
internal static class FormularioDeInscricaoHttp
{
    public const string Secao = "DADOS";

    public static Uri RotaDosItens(Guid processoId) =>
        new($"/api/selecao/admin/processos-seletivos/{processoId}/formularios/INSCRICAO/itens", UriKind.Relative);

    public static object CorpoDosItens(IEnumerable<object> itens) => new
    {
        itens = itens.Select(static item =>
        {
            JsonObject objeto = JsonSerializer.SerializeToNode(item)!.AsObject();
            objeto.TryAdd("etapaCodigo", Secao);
            return objeto;
        }).ToArray(),
    };
}
