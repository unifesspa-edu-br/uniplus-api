namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TermosConsentimento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Lista termos de consentimento vivos, paginados por cursor bidirecional
/// (ADR-0026 + ADR-0089). Sem as versões promovidas — use
/// <c>ObterTermoConsentimentoPorIdQuery</c> para o termo completo.
/// </summary>
/// <param name="AfterId">Âncora da página anterior; <c>null</c> retorna a primeira janela.</param>
/// <param name="Limit">Tamanho máximo da página a retornar.</param>
/// <param name="Direction">Direção de navegação (<c>Next</c>/<c>Prev</c>, ADR-0089).</param>
/// <param name="Busca">
/// Termo de busca livre (caixa-insensível) sobre <c>Nome</c>; <c>null</c>/vazio = sem
/// filtro textual. O handler normaliza. Trocar o termo entre páginas de uma mesma
/// navegação produz resultados inconsistentes — o cursor não vincula o filtro
/// (ADR-0026), então o cliente deve manter <c>Busca</c> fixo ao seguir prev/next
/// (os links já o preservam automaticamente).
/// </param>
public sealed record ListarTermosConsentimentoQuery(
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction,
    string? Busca) : IQuery<ListarTermosConsentimentoResult>;
