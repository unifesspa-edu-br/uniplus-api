namespace Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Repositório das revisões das concessões. Quem altera chama depois de bloquear o perfil: a leitura enxerga o que as
/// transações anteriores confirmaram e os bloqueios das concessões seguem a ordem única do perfil.
/// </summary>
public interface IRevisaoDasConcessoesRepository
{
    Task AdicionarAsync(RevisaoDasConcessoesDoPerfil revisao, CancellationToken cancellationToken = default);

    /// <summary>A revisão não encerrada do perfil, com envios de decisões, situações e decisões, rastreada; nula quando não há.</summary>
    Task<RevisaoDasConcessoesDoPerfil?> ObterNaoEncerradaParaAlterarAsync(Guid perfilId, CancellationToken cancellationToken = default);

    /// <summary>A revisão mais recente do perfil, encerrada ou não, com envios de decisões, situações e decisões, sem rastrear.</summary>
    Task<RevisaoDasConcessoesDoPerfil?> ObterUltimaParaLeituraAsync(Guid perfilId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Se o perfil tem revisão efetivamente aberta: não encerrada, com o prazo por vencer e ao menos uma concessão elegível.
    /// É o predicado de quem bloqueia pedido, versão pendente e concessão direta, lido sob o bloqueio que eles já obtêm.
    /// </summary>
    Task<bool> TemRevisaoAbertaAsync(Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>Se o perfil tem revisão não encerrada que já deixou de estar efetivamente aberta e ainda não foi materializada.</summary>
    Task<bool> TemRevisaoPorEncerrarAsync(Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>
    /// As concessões do perfil que a revisão alcança: as não revogadas e as revogadas depois de <paramref name="desde"/>,
    /// rastreadas e bloqueadas com <c>FOR UPDATE</c> em ordem de id até o fim da transação.
    /// </summary>
    Task<IReadOnlyList<Concessao>> ObterConcessoesDaRevisaoAsync(Guid perfilId, DateTimeOffset desde, CancellationToken cancellationToken = default);

    /// <summary>As concessões do perfil que a revisão alcança, sem rastrear nem bloquear: a leitura da tela.</summary>
    Task<IReadOnlyList<Concessao>> ListarConcessoesDaRevisaoAsync(Guid perfilId, DateTimeOffset desde, CancellationToken cancellationToken = default);

    /// <summary>Os códigos que cada concessão dá no instante, pela regra única da decisão de acesso; a sem permissão volta vazia.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> ObterComposicoesAsync(
        IReadOnlyCollection<Guid> concessaoIds, DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>As revisões não encerradas com o prazo por vencer em <paramref name="agora"/>, com envios de decisões, situações e decisões, sem rastrear.</summary>
    Task<IReadOnlyList<RevisaoDasConcessoesDoPerfil>> ListarAbertasParaLeituraAsync(DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>As concessões não revogadas dos perfis, sem rastrear nem bloquear: a leitura da fila de revisões.</summary>
    Task<IReadOnlyList<Concessao>> ListarConcessoesAtivasDosPerfisAsync(
        IReadOnlyCollection<Guid> perfilIds, CancellationToken cancellationToken = default);

    /// <summary>Os perfis com revisão não encerrada, lidos sem bloqueio: quem os processa bloqueia e confere cada um de novo.</summary>
    Task<IReadOnlyList<Guid>> ListarPerfisComRevisaoNaoEncerradaAsync(CancellationToken cancellationToken = default);

    /// <summary>As decisões da revisão sobre a concessão, em ordem cronológica, sem rastrear.</summary>
    Task<IReadOnlyList<DecisaoDaMigracao>> ListarDecisoesDaConcessaoAsync(Guid concessaoId, CancellationToken cancellationToken = default);
}
