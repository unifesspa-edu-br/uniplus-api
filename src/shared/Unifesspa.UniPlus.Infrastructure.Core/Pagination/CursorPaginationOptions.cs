namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// Configuração global de cursor pagination (ADR-0026): TTL do cursor e
/// range aceitável de <c>limit</c>. Bind a partir da seção
/// <c>UniPlus:Pagination:Cursor</c>; defaults sensatos garantem que
/// projetos sem configuração explícita rodam imediatamente.
/// </summary>
public sealed record CursorPaginationOptions
{
    public const string SectionName = "UniPlus:Pagination:Cursor";

    /// <summary>Tempo de validade do cursor opaco emitido pelo binder.</summary>
    public TimeSpan CursorTtl { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>Tamanho de página padrão quando nem QS nem cursor especificam.</summary>
    public int LimitDefault { get; init; } = 20;

    /// <summary>Limite mínimo aceito; valores abaixo retornam 422.</summary>
    public int LimitMin { get; init; } = 1;

    /// <summary>Limite máximo aceito; valores acima retornam 422 (QS) ou são clampados (cursor).</summary>
    public int LimitMax { get; init; } = 100;
}
