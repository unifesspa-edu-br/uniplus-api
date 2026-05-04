namespace Unifesspa.UniPlus.Selecao.API.Configuration;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Knobs de paginação cursor-based do <c>EditalController</c>, conforme ADR-0026
/// (TTL configurável por endpoint, range de <c>limit</c> validado no boundary).
/// Bind a partir da seção <c>Selecao:Edital:Pagination</c>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Consumido como parâmetro do construtor do EditalController público (ASP.NET Core exige controllers públicos); acessibilidade do options precisa acompanhar.")]
public sealed record EditalPaginationOptions
{
    public const string SectionName = "Selecao:Edital:Pagination";

    /// <summary>Tempo de validade do cursor opaco emitido pelo controller.</summary>
    public TimeSpan CursorTtl { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>Tamanho de página padrão quando nem QS nem cursor especificam.</summary>
    public int LimitDefault { get; init; } = 20;

    /// <summary>Limite mínimo aceito; valores abaixo retornam 422.</summary>
    public int LimitMin { get; init; } = 1;

    /// <summary>Limite máximo aceito; valores acima retornam 422 (QS) ou são clampados (cursor).</summary>
    public int LimitMax { get; init; } = 100;
}
