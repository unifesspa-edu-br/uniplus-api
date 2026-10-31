namespace Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Situação da concessão pedida como filtro da lista.</summary>
public enum SituacaoDaConcessao
{
    Ativa = 1,
    Revogada = 2,

    /// <summary>Não revogada, com a vigência já vencida.</summary>
    Vencida = 3,

    Todas = 4,
}

/// <summary>Filtros da lista de concessões. A pessoa é o par emissor e <c>sub</c>, nunca CPF.</summary>
public sealed record FiltroDeConcessoes(
    string? PessoaEmissor,
    string? PessoaSub,
    Guid? UnidadeId,
    Guid? PerfilId,
    SituacaoDaConcessao Situacao,
    DateTimeOffset Agora);

/// <summary>Titular de uma concessão ativa e o e-mail que a concessão guarda dele.</summary>
public sealed record TitularComEmail(Identidade Pessoa, string Email);

/// <summary>Concessão com o nome do perfil que ela concede, para a resposta.</summary>
public sealed record ConcessaoComPerfil(Concessao Concessao, string PerfilNome) : IIdentificavel
{
    /// <summary>Id da concessão, exposto para a paginação keyset operar sobre a projeção.</summary>
    public Guid Id { get; init; } = Concessao.Id;
}

/// <summary>Repositório das concessões de acesso.</summary>
public interface IConcessaoRepository : IRepository<Concessao>
{
    /// <summary>
    /// Lê o perfil a conceder travando-o com <c>FOR SHARE</c> até o fim da transação: a
    /// desativação simultânea espera ou conflita, sem corrida entre "perfil ativo" e a gravação
    /// da concessão. É o caminho obrigatório de quem chama <see cref="Concessao.Conceder"/>.
    /// Quem concede também revoga, antes, a concessão vencida igual: a vencida não é encerrada
    /// sozinha e, enquanto não revogada, o índice único recusa a nova.
    /// </summary>
    Task<Perfil?> ObterParaConcederAsync(Guid perfilId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A concessão não revogada, vigente ou vencida, igual à que se concederia (mesmo beneficiário, perfil e
    /// escopo), rastreada; nula quando não há. É a que o índice único barra.
    /// </summary>
    Task<Concessao?> ObterNaoRevogadaIgualAsync(
        Identidade beneficiario, Guid perfilId, EscopoDaConcessao escopo, CancellationToken cancellationToken = default);

    /// <summary>Indica se a pessoa tem concessão ativa do perfil, no escopo da instituição.</summary>
    Task<bool> TemConcessaoAtivaDoPerfilAsync(
        Identidade pessoa, Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>Lê a concessão com o nome do perfil, sem rastreamento.</summary>
    Task<ConcessaoComPerfil?> ObterComPerfilAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista as concessões paginadas por cursor keyset bidirecional (ADR-0026 + ADR-0089),
    /// ordenadas por <c>Id</c>, sem rastreamento.
    /// </summary>
    Task<(IReadOnlyList<ConcessaoComPerfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        FiltroDeConcessoes filtro,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica se o perfil tem alguma concessão não revogada, vigente ou vencida: é o que torna a
    /// edição do perfil uma alteração do acesso de quem o recebeu.
    /// </summary>
    Task<bool> ExisteNaoRevogadaDoPerfilAsync(Guid perfilId, CancellationToken cancellationToken = default);

    /// <summary>Governança do perfil da concessão, para saber se a revogação conta gestores.</summary>
    Task<TipoDeGovernanca?> ObterGovernancaDoPerfilAsync(Guid perfilId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializa as operações que podem reduzir o número de gestores de acessos, até o fim da
    /// transação. A contagem e a revogação que a segue rodam sob esta trava.
    /// </summary>
    Task TravarGovernancaParaEscritaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Conta os beneficiários distintos com concessão ativa, no escopo da instituição, do perfil
    /// de gestor de acessos, sem contar a concessão <paramref name="exceto"/>.
    /// </summary>
    Task<int> ContarGestoresAtivosAsync(Guid perfilId, Guid exceto, DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>
    /// Os titulares com concessão ativa do perfil, no escopo da instituição, que têm e-mail na concessão: são
    /// quem recebe o aviso das filas de aprovação. A concessão sem e-mail fica de fora.
    /// </summary>
    Task<IReadOnlyList<TitularComEmail>> ListarTitularesAtivosComEmailAsync(
        Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default);
}
