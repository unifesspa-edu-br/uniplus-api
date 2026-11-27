namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using Unifesspa.UniPlus.Kernel.Extensions;

/// <summary>
/// Configuração global de cursor pagination (ADR-0026): TTL do cursor e
/// intervalo aceitável de <c>limit</c>. Bind a partir da seção
/// <c>UniPlus:Pagination:Cursor</c>; defaults sensatos garantem que
/// projetos sem configuração explícita rodam imediatamente.
/// </summary>
public sealed record CursorPaginationOptions
{
    public const string SectionName = "UniPlus:Pagination:Cursor";

    /// <summary>
    /// Chave canônica provisionada pelo chart <c>platform/vault-transit-bootstrap</c> do
    /// uniplus-infra e compartilhada com a Idempotency-Key, sob a policy
    /// <c>uniplus-api-transit</c>.
    /// </summary>
    public const string DefaultKeyName = "uniplus-idempotency-aesgcm";

    /// <summary>
    /// Tempo de validade do cursor opaco emitido pelo binder. Configurado em qualquer precisão, é guardado em
    /// microssegundos inteiros, a precisão do instante de expiração que o cursor carrega.
    /// </summary>
    public TimeSpan CursorTtl { get; init => field = value.TruncadoEmMicrossegundos(); } = TimeSpan.FromMinutes(15);

    /// <summary>Tamanho de página padrão quando nem QS nem cursor especificam.</summary>
    public int LimitDefault { get; init; } = 20;

    /// <summary>Limite mínimo aceito; valores abaixo retornam 422.</summary>
    public int LimitMin { get; init; } = 1;

    /// <summary>Limite máximo aceito; valores acima retornam 422 (QS) ou são clampados (cursor).</summary>
    public int LimitMax { get; init; } = 100;

    /// <summary>
    /// Maior valor de filtro, sem os espaços das pontas, que o link de navegação repete; um valor maior fica de fora do
    /// link. Não pode ficar abaixo do maior termo de busca que as listagens aceitam.
    /// </summary>
    public int ComprimentoMaximoDoValorNoLink { get; init; } = 256;

    /// <summary>
    /// Maior tamanho do cabeçalho <c>Link</c>; acima dele, os links saem só com a paginação. Cada cursor carrega a
    /// chave de continuação, então o cabeçalho de uso legítimo passa de alguns milhares de caracteres.
    /// </summary>
    public int ComprimentoMaximoDoCabecalhoLink { get; init; } = 16384;

    /// <summary>
    /// Nome da chave com que o cursor é cifrado em <see cref="Cryptography.IUniPlusEncryptionService"/>.
    /// Um deployable que compõe a coleção de outro com paginação própria usa chave própria: quem
    /// detém a chave consegue emitir cursor para qualquer coleção cifrada com ela (ADR-0131).
    /// </summary>
    public string KeyName { get; init; } = DefaultKeyName;
}
