namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Repositório do cadastro único de motivos de decisão (UNI-REQ-0283).</summary>
public interface IMotivoDeDecisaoRepository : IRepository<MotivoDeDecisao>
{
    /// <summary>
    /// Lista motivos paginados por cursor keyset bidirecional (ADR-0026 + ADR-0089), com filtro
    /// opcional por tipo, por subclassificação e por situação. Ordenação estável por <c>Id</c>.
    /// </summary>
    /// <param name="apenasAtivos">
    /// Quando verdadeiro, devolve só os motivos que ainda entram em novas publicações. Quem monta
    /// uma publicação usa esta visão; quem administra o cadastro precisa ver os desativados.
    /// </param>
    Task<(IReadOnlyList<MotivoDeDecisao> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        FiltroDosMotivos filtro,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica se o código já pertence a algum motivo do mesmo tipo, inclusive desativado: o código
    /// é citado nas decisões já proferidas, e reaproveitá-lo faria duas coisas responderem pelo
    /// mesmo rótulo no histórico. O mesmo código em outro tipo é outro motivo.
    /// </summary>
    Task<bool> CodigoExisteAsync(
        TipoMotivoDeDecisao tipo,
        string codigo,
        CancellationToken cancellationToken = default);
}

/// <summary>O filtro da listagem do cadastro; nulo em cada eixo significa sem filtro nele.</summary>
public sealed record FiltroDosMotivos(TipoMotivoDeDecisao? Tipo, string? Subclassificacao, bool ApenasAtivos);
