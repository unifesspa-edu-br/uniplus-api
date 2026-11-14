namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>O que "Minhas inscrições" precisa saber de um processo: o nome divulgado e se as inscrições estão abertas agora.</summary>
/// <param name="Nome">O nome do processo como está divulgado; nulo se ele deixou de ser público.</param>
/// <param name="InscricoesAbertas">Se o instante da consulta está no período de inscrição divulgado.</param>
public sealed record SituacaoDoProcessoDaInscricao(string? Nome, bool InscricoesAbertas);

/// <summary>
/// Os processos das inscrições de um candidato, lidos uma vez por processo e no mesmo instante: o período vem do processo, e
/// as ofertas de curso, da versão em que cada inscrição está, nunca da vigente. Processo sem versão divulgada legível, ou sem
/// janela, conta como fechado: nada a oferecer.
/// </summary>
public sealed class ProcessosDasInscricoes
{
    private readonly ICertameDivulgadoRepository _certameDivulgadoRepository;
    private readonly IProcessoSeletivoRepository _processoSeletivoRepository;
    private readonly IRegistroCodecsEnvelope _registroCodecs;
    private readonly IResolvedorFusoInstitucional _resolvedorFuso;
    private readonly TimeProvider _timeProvider;

    public ProcessosDasInscricoes(
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IResolvedorFusoInstitucional resolvedorFuso,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(resolvedorFuso);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _certameDivulgadoRepository = certameDivulgadoRepository;
        _processoSeletivoRepository = processoSeletivoRepository;
        _registroCodecs = registroCodecs;
        _resolvedorFuso = resolvedorFuso;
        _timeProvider = timeProvider;
    }

    /// <summary>A situação de cada processo das inscrições, lida num só instante.</summary>
    public async Task<Dictionary<Guid, SituacaoDoProcessoDaInscricao>> SituacoesAsync(
        IEnumerable<Inscricao> inscricoes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricoes);

        DateTimeOffset agora = _timeProvider.GetUtcNow();
        Dictionary<Guid, SituacaoDoProcessoDaInscricao> situacoes = [];
        foreach (Guid processo in inscricoes.Select(static i => i.ProcessoSeletivoId).Distinct())
        {
            situacoes[processo] = await SituacaoAsync(processo, agora, cancellationToken).ConfigureAwait(false);
        }

        return situacoes;
    }

    /// <summary>As ofertas de curso da versão em que cada inscrição está, com o nome congelado na publicação.</summary>
    public Task<Dictionary<Guid, IReadOnlyList<OfertaDeCursoCertameDto>>> OfertasDasVersoesAsync(
        IEnumerable<Inscricao> inscricoes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricoes);

        return ContextoDaVersaoFixadaResolver.OfertasDasVersoesAsync(
            inscricoes.Select(static i => (i.ProcessoSeletivoId, i.VersaoFormularioId)),
            _processoSeletivoRepository,
            _registroCodecs,
            cancellationToken);
    }

    private async Task<SituacaoDoProcessoDaInscricao> SituacaoAsync(Guid processo, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        CertameDivulgado? divulgado = await _certameDivulgadoRepository
            .ObterParaLeituraAsync(processo, cancellationToken).ConfigureAwait(false);
        bool abertas = await PeriodoDaInscricaoResolver.ConferirAsync(
            processo, _certameDivulgadoRepository, _processoSeletivoRepository, _registroCodecs, _resolvedorFuso, agora, cancellationToken)
            .ConfigureAwait(false) is null;

        return new SituacaoDoProcessoDaInscricao(divulgado?.Nome, abertas);
    }
}
