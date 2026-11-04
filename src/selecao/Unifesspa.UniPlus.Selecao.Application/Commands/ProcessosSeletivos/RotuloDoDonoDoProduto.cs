namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Como a recusa de um produto se identifica para o dono que o declara: os códigos de erro, o qualificador
/// que a mensagem acrescenta ao que foi declarado (vazio quando o dono já está claro pelo contexto) e o
/// lugar que a violação do produto nomeia.
/// </summary>
internal sealed record RotuloDoDonoDoProduto(
    string CodigoDoPapelDesconhecido,
    string CodigoDoAtoNaoEncontrado,
    string CodigoDoPapelEmAtoQueNaoEhResultado,
    string Qualificador,
    string Lugar)
{
    public static RotuloDoDonoDoProduto DaEtapa(string nomeDaEtapa) => new(
        "ProdutoDaEtapa.PapelDesconhecido",
        "ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo",
        "ProdutoDaEtapa.PapelEmAtoQueNaoEhResultado",
        $", declarado pela etapa '{nomeDaEtapa}',",
        $"na etapa '{nomeDaEtapa}'");

    public static RotuloDoDonoDoProduto DaFase(string faseCodigo) => new(
        "ProdutoDaFase.PapelDesconhecido",
        "ProdutoDaFase.AtoNaoEncontradoNoCatalogo",
        "ProdutoDaFase.PapelEmAtoQueNaoEhResultado",
        string.Empty,
        $"na fase '{faseCodigo}'");
}
