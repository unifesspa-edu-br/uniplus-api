namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Payload claro do cursor opaco AES-GCM (ADR-0026). Carregamos apenas os
/// campos estritamente necessários para retomar a paginação e o escopo do
/// recurso — nunca PII (CPF, nome, e-mail, número de inscrição), conforme
/// ADR-0019.
/// </summary>
/// <param name="After">Chave de continuação (id ou sort key serializada do item de fronteira da página anterior).</param>
/// <param name="Limit">Tamanho de página efetivamente solicitado.</param>
/// <param name="ResourceTag">Identificador do recurso paginado (ex.: <c>editais</c>); previne reuso cross-resource.</param>
/// <param name="ExpiresAt">Instante UTC após o qual o cursor é rejeitado com 410.</param>
/// <param name="Direction">
/// Direção para a qual o cursor foi emitido (ADR-0089). Vincula o cursor à sua
/// direção: o boundary valida <c>query.direction == payload.Direction</c> e
/// rejeita reuso incoerente (cursor de <c>next</c> chamado como <c>prev</c>)
/// como adulteração (400). O sistema não está em produção — não há cursores
/// legados sem o campo a preservar.
/// </param>
/// <param name="UserId">
/// Sub claim do JWT de quem emitiu o cursor. <c>null</c> em recursos públicos
/// (ex.: <c>/api/editais</c>); obrigatório em recursos user-scoped
/// (<c>/api/inscricoes/minhas</c>) — <see cref="FromCursorAttribute.RequireUserBinding"/>
/// fixa a invariante. Previne metadata leak entre clientes diferentes:
/// cliente A não consegue replay de cursor emitido para cliente B (ADR-0026
/// §"User-binding em cursores user-scoped").
/// </param>
public sealed record CursorPayload(
    string After,
    int Limit,
    string ResourceTag,
    DateTimeOffset ExpiresAt,
    PaginationDirection Direction,
    string? UserId = null);
