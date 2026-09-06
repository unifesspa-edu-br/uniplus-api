namespace Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Sentido de uma coluna dentro de uma ordenação.
/// <para>
/// Vive no Kernel pela mesma razão de <see cref="PaginationDirection"/>: quando a
/// ordenação é escolhida por quem consulta, o sentido atravessa todas as camadas
/// — chega como parâmetro na borda, viaja na query e termina como cláusula na
/// porta do repositório.
/// </para>
/// </summary>
public enum SortDirection
{
    /// <summary>Crescente.</summary>
    Ascending = 0,

    /// <summary>Decrescente.</summary>
    Descending = 1,
}

/// <summary>
/// Um campo de ordenação pedido por quem consulta: o nome público do campo e o
/// sentido. A prioridade não está aqui — é a posição do campo na sequência.
/// </summary>
/// <param name="Campo">Nome público do campo, como aparece no parâmetro de consulta.</param>
/// <param name="Direcao">Sentido pedido para este campo.</param>
public sealed record SortField(string Campo, SortDirection Direcao);
