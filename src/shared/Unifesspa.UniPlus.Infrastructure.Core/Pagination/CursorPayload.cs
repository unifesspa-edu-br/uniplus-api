namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// Payload claro do cursor opaco AES-GCM (ADR-0026). Carregamos apenas os
/// campos estritamente necessários para retomar a paginação e o escopo do
/// recurso — nunca PII (CPF, nome, e-mail, número de inscrição), conforme
/// ADR-0019.
/// </summary>
/// <param name="After">Chave de continuação (id ou sort key serializada do último item da página anterior).</param>
/// <param name="Limit">Tamanho de página efetivamente solicitado.</param>
/// <param name="ResourceTag">Identificador do recurso paginado (ex.: <c>editais</c>); previne reuso cross-resource.</param>
/// <param name="ExpiresAt">Instante UTC após o qual o cursor é rejeitado com 410.</param>
public sealed record CursorPayload(
    string After,
    int Limit,
    string ResourceTag,
    DateTimeOffset ExpiresAt);
