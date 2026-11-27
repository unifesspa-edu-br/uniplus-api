namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Como a recusa de um produto se identifica para o dono que o declara: os códigos de erro, o qualificador
/// que a mensagem acrescenta ao que foi declarado (vazio quando o dono já está claro pelo contexto).
/// </summary>
internal sealed record RotuloDoDonoDoProduto(
    string CodigoDoPapelDesconhecido,
    string CodigoDoAtoNaoEncontrado,
    string CodigoDoPapelEmAtoQueNaoEhResultado,
    string Qualificador)
{
    public static RotuloDoDonoDoProduto DaEtapa() => new(
        "ProdutoDaEtapa.PapelDesconhecido",
        "ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo",
        "ProdutoDaEtapa.PapelEmAtoQueNaoEhResultado",
        ", declarado pela etapa,");

    public static RotuloDoDonoDoProduto DaFase() => new(
        "ProdutoDaFase.PapelDesconhecido",
        "ProdutoDaFase.AtoNaoEncontradoNoCatalogo",
        "ProdutoDaFase.PapelEmAtoQueNaoEhResultado",
        string.Empty);
}
