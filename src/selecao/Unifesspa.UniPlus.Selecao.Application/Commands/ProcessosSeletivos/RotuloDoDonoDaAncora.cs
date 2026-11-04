namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Como a recusa da âncora do recurso se identifica para o dono da janela recursal: os códigos de erro e o
/// qualificador que a mensagem acrescenta ao tipo de ato (vazio quando o dono já está claro pelo contexto).
/// </summary>
internal sealed record RotuloDoDonoDaAncora(
    string CodigoDoAtoNaoEncontrado,
    string CodigoDaAncoraCongelante,
    string CodigoDaAncoraIrreversivel,
    string Qualificador)
{
    public static RotuloDoDonoDaAncora DaEtapa(string nomeDaEtapa) => new(
        "ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo",
        "RecursoDaEtapa.AncoraEmAtoCongelante",
        "RecursoDaEtapa.AncoraEmAtoIrreversivel",
        $", declarado pela etapa '{nomeDaEtapa}',");

    public static RotuloDoDonoDaAncora DaFase { get; } = new(
        "ProdutoDaFase.AtoNaoEncontradoNoCatalogo",
        "RegraRecursoFase.AncoraEmAtoCongelante",
        "RegraRecursoFase.AncoraEmAtoIrreversivel",
        string.Empty);
}
