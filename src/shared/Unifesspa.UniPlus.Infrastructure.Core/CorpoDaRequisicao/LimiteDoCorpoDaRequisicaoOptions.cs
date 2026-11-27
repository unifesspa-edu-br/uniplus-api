namespace Unifesspa.UniPlus.Infrastructure.Core.CorpoDaRequisicao;

/// <summary>
/// Tamanho máximo do corpo de uma requisição. Bind a partir da seção <c>UniPlus:LimiteDoCorpoDaRequisicao</c>.
/// </summary>
public sealed record LimiteDoCorpoDaRequisicaoOptions
{
    public const string SectionName = "UniPlus:LimiteDoCorpoDaRequisicao";

    /// <summary>
    /// 2 MiB: o maior pedido legítimo medido vezes quatro, arredondado para cima em MiB. O maior pedido legítimo é o cronograma
    /// de pior caso (14 fases, 10 bancas por fase e 10 categorias por banca), com 315.847 bytes; vezes quatro dá 1.263.388 bytes,
    /// que cabem em 2 MiB. O maior modelo de formulário semeado tem 26.291 bytes. O modelo de formulário com todos os itens no
    /// tamanho máximo de cada campo (2.422.658 bytes) não é pedido legítimo e passa deste limite de propósito.
    /// </summary>
    public const long PadraoEmBytes = 2L * 1024 * 1024;

    /// <summary>
    /// Limite das operações que não declaram o próprio com <c>[RequestSizeLimit]</c>. É valor técnico de implantação, não
    /// regra de negócio.
    /// </summary>
    public long TamanhoMaximoEmBytes { get; init; } = PadraoEmBytes;
}
