namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// O recorte sobre o qual uma listagem paginada corre: as partes do filtro, na ordem em que a listagem as declara, e o
/// instante congelado do percurso, quando a listagem congela um. Entra no resumo que amarra o cursor à consulta: quem
/// continua com outro recorte recebe <see cref="CursorAnchorMismatchException"/> em vez de pular itens em silêncio.
/// </summary>
/// <remarks>
/// Entram só os parâmetros que o cliente informa, com o mesmo valor efetivo que vai ao <c>WHERE</c>. Ficam de fora o
/// relógio, o que vem do token ou da rota e a projeção de colunas.
/// </remarks>
public sealed class CondicoesDoFiltro
{
    private readonly IReadOnlyList<ParteDoFiltro> _partes;

    private CondicoesDoFiltro(IReadOnlyList<ParteDoFiltro> partes, DateTimeOffset? instanteCongelado)
    {
        _partes = partes;
        InstanteCongelado = instanteCongelado;
    }

    /// <summary>Para a listagem que não tem filtro nem busca, ou cujo recorte é fixo no servidor.</summary>
    public static CondicoesDoFiltro Nenhuma { get; } = new([], instanteCongelado: null);

    public DateTimeOffset? InstanteCongelado { get; }

    public static CondicoesDoFiltro De(params ParteDoFiltro[] partes)
    {
        ArgumentNullException.ThrowIfNull(partes);

        return new CondicoesDoFiltro([.. partes], instanteCongelado: null);
    }

    /// <summary>As mesmas condições, com o instante que o percurso inteiro da listagem usa.</summary>
    public CondicoesDoFiltro CongeladasEm(DateTimeOffset instante) => new(_partes, instante);

    /// <summary>Chave de continuação da listagem paginada só por <c>Id</c>, igual para todos os itens.</summary>
    public string EmitirChave() => ChaveDeContinuacao.Compor(Resumo([]), InstanteCongelado, []);

    /// <exception cref="CursorAnchorMismatchException">
    /// A chave está ausente, malformada ou é de outra consulta.
    /// </exception>
    public void ConferirChave(string? chave)
    {
        if (!ChaveDeContinuacao.TentarLer(chave, colunas: 0, out ChaveDeContinuacao.LeituraDaChave leitura)
            || !string.Equals(leitura.Resumo, Resumo([]), StringComparison.Ordinal))
        {
            throw new CursorAnchorMismatchException();
        }
    }

    /// <summary>Resumo da consulta: ordenação (token e sentido de cada coluna), condições e instante congelado.</summary>
    internal string Resumo(IEnumerable<string> ordenacao) =>
        ResumoCanonico.De(
            CompositeSortKey.Serialize([.. ordenacao]),
            CompositeSortKey.Serialize([.. _partes.Select(static p => p.Forma)]),
            InstanteCongelado is { } instante ? InstanteDoCursor.Escrever(instante) : string.Empty);
}
