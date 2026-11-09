namespace Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Repositório das janelas de migração. Quem altera chama depois de travar o perfil: a leitura enxerga o que as
/// transações anteriores confirmaram e as travas das concessões seguem a ordem única do perfil.
/// </summary>
public interface IJanelaDeMigracaoRepository
{
    Task AdicionarAsync(JanelaDeMigracao janela, CancellationToken cancellationToken = default);

    /// <summary>A janela não encerrada do perfil, com lotes, situações e decisões, rastreada; nula quando não há.</summary>
    Task<JanelaDeMigracao?> ObterNaoEncerradaParaAlterarAsync(Guid perfilId, CancellationToken cancellationToken = default);

    /// <summary>A janela mais recente do perfil, encerrada ou não, com lotes, situações e decisões, sem rastrear.</summary>
    Task<JanelaDeMigracao?> ObterUltimaParaLeituraAsync(Guid perfilId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Se o perfil tem janela efetivamente aberta: não encerrada, com o prazo por vencer e ao menos uma concessão elegível.
    /// É o predicado de quem bloqueia pedido, versão pendente e concessão direta, lido sob a trava que eles já tomam.
    /// </summary>
    Task<bool> TemJanelaAbertaAsync(Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>Se o perfil tem janela não encerrada que já deixou de estar efetivamente aberta e ainda não foi materializada.</summary>
    Task<bool> TemJanelaPorMaterializarAsync(Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>
    /// As concessões do perfil que a janela alcança: as não revogadas e as revogadas depois de <paramref name="desde"/>,
    /// rastreadas e travadas com <c>FOR UPDATE</c> em ordem de id até o fim da transação.
    /// </summary>
    Task<IReadOnlyList<Concessao>> ObterConcessoesDaJanelaAsync(Guid perfilId, DateTimeOffset desde, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trava o perfil de governança da proteção de dados e todas as concessões ativas dele, em ordem de id, e devolve
    /// quem as tem: a dispensa da segunda etapa se lê delas, e a criação e a revogação concorrentes esperam a trava.
    /// </summary>
    Task<IReadOnlyCollection<Identidade>> TravarTitularesDaProtecaoDeDadosAsync(DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>Os códigos que cada concessão dá no instante, pela regra única da decisão de acesso; a sem permissão volta vazia.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> ObterComposicoesAsync(
        IReadOnlyCollection<Guid> concessaoIds, DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>Os perfis com janela não encerrada, lidos sem trava: quem os processa trava e confere cada um de novo.</summary>
    Task<IReadOnlyList<Guid>> ListarPerfisComJanelaNaoEncerradaAsync(CancellationToken cancellationToken = default);

    /// <summary>As decisões da janela sobre a concessão, em ordem cronológica, sem rastrear.</summary>
    Task<IReadOnlyList<DecisaoDaMigracao>> ListarDecisoesDaConcessaoAsync(Guid concessaoId, CancellationToken cancellationToken = default);
}
