namespace Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Direção de navegação numa coleção paginada por cursor keyset (ADR-0089).
/// <c>Next</c> avança (itens após a âncora); <c>Prev</c> retrocede (itens antes
/// da âncora). Em ambos os casos a página é apresentada em ordem ascendente
/// canônica por <c>Id</c>.
/// <para>
/// Vive no Kernel porque é parte do contrato de portas de repositório (Domain),
/// além de ser consumida por Application (query) e Infrastructure (cursor/binder).
/// </para>
/// </summary>
public enum PaginationDirection
{
    /// <summary>Próxima página: itens com <c>Id</c> maior que a âncora.</summary>
    Next = 0,

    /// <summary>Página anterior: itens com <c>Id</c> menor que a âncora.</summary>
    Prev = 1,
}
