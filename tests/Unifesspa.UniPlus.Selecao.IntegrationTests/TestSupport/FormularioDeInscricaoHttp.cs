namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Regras.Entradas;

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

    /// <summary>
    /// O item na forma do corpo: a apresentação e as regras aninhadas, e a obrigatoriedade com o tipo. Sem
    /// <paramref name="etapaCodigo"/>, o item vai sem seção, e <see cref="CorpoDosItens"/> o põe na dos dados.
    /// </summary>
    public static object Item(
        string fatoCodigo,
        int ordem,
        string rotulo,
        string tipoDeCampo,
        string obrigatoriedade = "SEMPRE",
        object? exibicao = null,
        string? etapaCodigo = null)
    {
        Dictionary<string, object?> item = new()
        {
            ["fatoCodigo"] = fatoCodigo,
            ["ordem"] = ordem,
            ["apresentacao"] = new { rotulo, tipoDeCampo },
            ["regras"] = new { exibicao, obrigatoriedade = new { tipo = obrigatoriedade } },
        };
        if (etapaCodigo is not null)
        {
            item["etapaCodigo"] = etapaCodigo;
        }

        return item;
    }

    /// <summary>
    /// O grupo na forma do corpo: as ocorrências e as regras aninhadas, e a obrigatoriedade com o tipo; os campos
    /// vão como <paramref name="subitens"/>.
    /// </summary>
    public static object Grupo(
        string codigo,
        int ordem,
        string rotulo,
        string etapaCodigo,
        object[] subitens,
        OcorrenciasDoGrupoInput? ocorrencias = null,
        string obrigatoriedade = "NUNCA") => new
        {
            codigo,
            ordem,
            rotulo,
            etapaCodigo,
            ocorrencias = ocorrencias ?? new OcorrenciasDoGrupoInput(0, false),
            regras = new { exibicao = (object?)null, obrigatoriedade = new { tipo = obrigatoriedade } },
            subitens,
        };

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
