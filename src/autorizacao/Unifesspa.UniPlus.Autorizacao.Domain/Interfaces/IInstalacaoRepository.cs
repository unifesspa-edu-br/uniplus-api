namespace Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Repositório das instalações dos primeiros gestores e das tentativas de confirmá-las.</summary>
public interface IInstalacaoRepository : IRepository<Instalacao>
{
    /// <summary>A instalação pendente do executor, rastreada, para cancelá-la ao iniciar outra.</summary>
    Task<Instalacao?> ObterPendenteDoExecutorAsync(Identidade executor, CancellationToken cancellationToken = default);

    /// <summary>A instalação do executor aguardando código, rastreada; nula quando ele não tem nenhuma.</summary>
    Task<Instalacao?> ObterAguardandoCodigoDoExecutorAsync(Identidade executor, CancellationToken cancellationToken = default);

    /// <summary>A instalação, rastreada, só se for do executor; a de outra pessoa não se distingue da inexistente.</summary>
    Task<Instalacao?> ObterDoExecutorAsync(Guid id, Identidade executor, CancellationToken cancellationToken = default);

    /// <summary>A instalação sem rastreamento, para a leitura da trilha.</summary>
    Task<Instalacao?> ObterParaLeituraAsync(Guid id, CancellationToken cancellationToken = default);

    Task AdicionarTentativaAsync(TentativaDeInstalacao tentativa, CancellationToken cancellationToken = default);

    /// <summary>As tentativas da instalação, da mais antiga à mais recente, sem rastreamento.</summary>
    Task<IReadOnlyList<TentativaDeInstalacao>> ListarTentativasAsync(Guid instalacaoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// As tentativas de quem não tinha instalação aguardando código, paginadas por cursor keyset
    /// bidirecional (ADR-0026 + ADR-0089), sem rastreamento.
    /// </summary>
    Task<(IReadOnlyList<TentativaDeInstalacao> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarTentativasSemPendenciaAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default);
}
