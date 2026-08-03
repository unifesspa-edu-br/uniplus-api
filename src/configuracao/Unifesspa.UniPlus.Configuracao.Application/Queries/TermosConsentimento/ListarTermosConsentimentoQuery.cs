namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TermosConsentimento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista termos de consentimento vivos, paginados por cursor bidirecional
/// (ADR-0026 + ADR-0089). Sem as versões promovidas — use
/// <c>ObterTermoConsentimentoPorIdQuery</c> para o termo completo.
/// </summary>
public sealed record ListarTermosConsentimentoQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<ListarTermosConsentimentoResult>;
