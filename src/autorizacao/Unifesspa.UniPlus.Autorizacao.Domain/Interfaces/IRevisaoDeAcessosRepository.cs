namespace Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Item da revisão com a concessão e o nome do perfil que ele revê, para a resposta.</summary>
/// <param name="ConcessaoAtiva">A concessão está ativa no instante da leitura.</param>
public sealed record ItemDaRevisaoComConcessao(ItemDaRevisao Item, Concessao Concessao, string PerfilNome, bool ConcessaoAtiva);

/// <summary>Revisão com seus itens lidos junto das concessões.</summary>
public sealed record RevisaoDetalhada(RevisaoDeAcessos Revisao, IReadOnlyList<ItemDaRevisaoComConcessao> Itens);

/// <summary>Revisão com a contagem dos itens e dos que ainda esperam decisão sobre concessão ativa.</summary>
public sealed record ResumoDaRevisao(RevisaoDeAcessos Revisao, int TotalDeItens, int Pendentes) : IIdentificavel
{
    /// <summary>Id da revisão, exposto para a paginação keyset operar sobre a projeção.</summary>
    public Guid Id { get; init; } = Revisao.Id;
}

/// <summary>Repositório das revisões periódicas dos acessos.</summary>
public interface IRevisaoDeAcessosRepository : IRepository<RevisaoDeAcessos>
{
    /// <summary>A revisão com os itens, rastreada, para quem decide um item.</summary>
    Task<RevisaoDeAcessos?> ObterParaAlterarAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>A revisão com os itens, as concessões e os nomes dos perfis, sem rastreamento.</summary>
    Task<RevisaoDetalhada?> ObterDetalhadaAsync(Guid id, DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista as revisões paginadas por cursor keyset bidirecional (ADR-0026 + ADR-0089), ordenadas por <c>Id</c>, sem
    /// rastreamento.
    /// </summary>
    Task<(IReadOnlyList<ResumoDaRevisao> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        DateTimeOffset agora,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default);

    /// <summary>Indica se alguma revisão tem item sem decisão sobre concessão ainda ativa.</summary>
    Task<bool> ExisteEmAndamentoAsync(DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializa a abertura de revisões até o fim da transação: a conferência de que não há revisão em andamento e a
    /// gravação da nova rodam sob esta trava.
    /// </summary>
    Task TravarAberturaAsync(CancellationToken cancellationToken = default);

    /// <summary>Os perfis com ao menos uma concessão ativa, sem rastreamento.</summary>
    Task<IReadOnlyList<Perfil>> ListarPerfisComConcessaoAtivaAsync(DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>As concessões ativas dos perfis, com o perfil e o beneficiário de cada uma.</summary>
    Task<IReadOnlyList<(Guid ConcessaoId, Guid PerfilId, Identidade Beneficiario)>> ListarConcessoesAtivasDosPerfisAsync(
        IReadOnlyCollection<Guid> perfilIds, DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>Os itens já decididos da concessão, do mais antigo ao mais recente, sem rastreamento.</summary>
    Task<IReadOnlyList<ItemDaRevisao>> ListarDecisoesDaConcessaoAsync(Guid concessaoId, CancellationToken cancellationToken = default);
}
