namespace Unifesspa.UniPlus.Portal.Application.Certames.Origens;

/// <summary>
/// Os atos de um certame lidos em Publicações, na ordem em que a origem os devolve (data de
/// publicação ascendente, identificador de desempate), e o que impediu a leitura de terminar.
/// </summary>
/// <param name="Atos">Os atos lidos até onde a leitura chegou.</param>
/// <param name="Falha">Nulo quando todas as páginas foram lidas.</param>
public sealed record LeituraDosAtos(IReadOnlyList<AtoDaOrigem> Atos, FalhaNaLeituraDosAtos? Falha);

/// <summary>
/// Por que a leitura dos atos não chegou ao fim.
/// </summary>
public enum FalhaNaLeituraDosAtos
{
    /// <summary>A origem não respondeu à primeira página.</summary>
    OrigemIndisponivel = 1,

    /// <summary>A origem deixou de responder, ou recusou o cursor, depois da primeira página.</summary>
    FalhaNaPaginacao = 2,

    /// <summary>Havia mais páginas do que o teto configurado permite percorrer.</summary>
    TetoDePaginas = 3,
}
