namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Concessoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Providencias;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>
/// A trilha da concessão, montada na leitura a partir das fontes que a contam, sem tabela de eventos: a própria concessão,
/// com o vencimento derivado da vigência; a origem, pela porta <see cref="IEventosDaOrigem"/>; as decisões das revisões
/// periódicas e da janela de migração; e as providências solicitadas sobre ela, com o desfecho de cada uma.
/// </summary>
public sealed class TrilhaDaConcessao
{
    private readonly IRevisaoDeAcessosRepository _revisoes;
    private readonly IJanelaDeMigracaoRepository _janelas;
    private readonly IProvidenciaRepository _providencias;
    private readonly IEnumerable<IEventosDaOrigem> _origens;
    private readonly TimeProvider _clock;

    public TrilhaDaConcessao(
        IRevisaoDeAcessosRepository revisoes,
        IJanelaDeMigracaoRepository janelas,
        IProvidenciaRepository providencias,
        IEnumerable<IEventosDaOrigem> origens,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(revisoes);
        ArgumentNullException.ThrowIfNull(janelas);
        ArgumentNullException.ThrowIfNull(providencias);
        ArgumentNullException.ThrowIfNull(origens);
        ArgumentNullException.ThrowIfNull(clock);

        _revisoes = revisoes;
        _janelas = janelas;
        _providencias = providencias;
        _origens = origens;
        _clock = clock;
    }

    /// <summary>
    /// Os eventos da concessão em ordem cronológica. Cada providência exposta, com fundamento, motivo e identidades, é
    /// registrada antes, pela permissão que dá a leitura das providências (ADR-0153); quem não tem nenhuma delas não as vê.
    /// </summary>
    /// <param name="verificador">O ponto de decisão da requisição, que avalia e registra a leitura das providências.</param>
    public async Task<IReadOnlyList<EventoDaTrilhaDto>> MontarAsync(Concessao concessao, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(concessao);
        ArgumentNullException.ThrowIfNull(verificador);

        List<EventoDaTrilhaDto> eventos = [.. ConcessaoMapping.EventosDaConcessao(concessao, _clock.GetUtcNow())];
        eventos.AddRange(await DaOrigemAsync(concessao, cancellationToken).ConfigureAwait(false));

        // A decisão da revisão é lida dos itens, e as da janela de migração, das decisões dela: o decisor humano da recusa
        // aparece com sua identidade, e o sistema como ator.
        eventos.AddRange((await _revisoes.ListarDecisoesDaConcessaoAsync(concessao.Id, cancellationToken).ConfigureAwait(false)).Select(RevisaoMapping.ToEvento));
        eventos.AddRange((await _janelas.ListarDecisoesDaConcessaoAsync(concessao.Id, cancellationToken).ConfigureAwait(false)).Select(MigracaoMapping.ToEvento));
        eventos.AddRange(await DasProvidenciasAsync(concessao.Id, verificador, cancellationToken).ConfigureAwait(false));

        return [.. eventos.OrderBy(static e => e.Instante)];
    }

    private async Task<IEnumerable<EventoDaTrilhaDto>> DasProvidenciasAsync(Guid concessaoId, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        IReadOnlyList<Providencia> providencias = await _providencias.ListarDaConcessaoAsync(concessaoId, cancellationToken).ConfigureAwait(false);

        return (await AcessoAProvidencia.LiberarAsync(verificador, providencias, cancellationToken).ConfigureAwait(false))
            .SelectMany(ProvidenciaMapping.EventosDaTrilha);
    }

    private async Task<IReadOnlyList<EventoDaTrilhaDto>> DaOrigemAsync(Concessao concessao, CancellationToken cancellationToken)
    {
        IEventosDaOrigem? origem = _origens.FirstOrDefault(o => o.Tipo == concessao.OrigemTipo);

        return origem is null ? [] : await origem.ObterAsync(concessao.OrigemId, cancellationToken).ConfigureAwait(false) ?? [];
    }
}
