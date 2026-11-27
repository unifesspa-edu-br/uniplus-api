namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Payload claro do cursor opaco AES-GCM (ADR-0026). Carregamos apenas os
/// campos estritamente necessários para retomar a paginação e o escopo do
/// recurso — nunca PII (CPF, nome, e-mail, número de inscrição), conforme
/// ADR-0019.
/// </summary>
/// <param name="After"><c>Id</c> do item de fronteira da página anterior.</param>
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
/// <param name="ResumoDaConta">
/// Resumo SHA-256 (Base64Url) do par emissor e identificador da conta de quem emitiu o cursor, nunca a claim.
/// <c>null</c> em recursos públicos; obrigatório em recursos que dependem da conta
/// (<c>/api/pedidos-de-concessao</c>) — <see cref="FromCursorAttribute.RequireUserBinding"/>
/// fixa a invariante. Previne metadata leak entre clientes diferentes:
/// cliente A não consegue replay de cursor emitido para cliente B (ADR-0026
/// §"User-binding em cursores user-scoped").
/// </param>
/// <param name="SortKey">
/// Chave de continuação (<see cref="ChaveDeContinuacao"/>): resumo da consulta, instante congelado e valores das colunas de
/// ordenação do item de fronteira (ADR-0094); a continuação é a tupla <c>(SortKey, After)</c>. Presente em toda
/// continuação. Nunca PII: o texto da busca e os filtros só entram no resumo (ADR-0019).
/// </param>
public sealed record CursorPayload(
    string After,
    int Limit,
    string ResourceTag,
    DateTimeOffset ExpiresAt,
    PaginationDirection Direction,
    string? ResumoDaConta = null,
    string? SortKey = null);
