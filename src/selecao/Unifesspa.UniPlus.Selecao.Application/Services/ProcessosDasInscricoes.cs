namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O que "Minhas inscrições" precisa saber de um processo: o nome divulgado e o motivo de as inscrições não estarem abertas agora.</summary>
/// <param name="Nome">O nome do processo como está divulgado; nulo se ele deixou de ser público.</param>
/// <param name="MotivoDoFechamento">Por que as inscrições estão fechadas no instante da consulta; nulo com as inscrições abertas.</param>
public sealed record SituacaoDoProcessoDaInscricao(string? Nome, MotivoDasInscricoesFechadas? MotivoDoFechamento);

/// <summary>
/// Os processos das inscrições de um candidato, lidos uma vez por processo e no mesmo instante: o período vem do processo, e
/// as ofertas de curso, da versão em que cada inscrição está, nunca da vigente. Processo sem versão divulgada legível, ou sem
/// período, conta como fechado: nada a oferecer, e o motivo diz qual dos casos foi.
/// </summary>
public sealed partial class ProcessosDasInscricoes
{
    private readonly ICertameDivulgadoRepository _certameDivulgadoRepository;
    private readonly PeriodoDaInscricaoResolver _periodo;
    private readonly ContextoDaVersaoFixadaResolver _contexto;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProcessosDasInscricoes> _logger;

    public ProcessosDasInscricoes(
        ICertameDivulgadoRepository certameDivulgadoRepository,
        PeriodoDaInscricaoResolver periodo,
        ContextoDaVersaoFixadaResolver contexto,
        TimeProvider timeProvider,
        ILogger<ProcessosDasInscricoes> logger)
    {
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(periodo);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _certameDivulgadoRepository = certameDivulgadoRepository;
        _periodo = periodo;
        _contexto = contexto;
        _timeProvider = timeProvider;
        _logger = logger;
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

        return _contexto.OfertasDasVersoesAsync(inscricoes.Select(static i => (i.ProcessoSeletivoId, i.Formulario.VersaoFormularioId)), cancellationToken);
    }

    private async Task<SituacaoDoProcessoDaInscricao> SituacaoAsync(Guid processo, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        CertameDivulgado? divulgado = await _certameDivulgadoRepository
            .ObterParaLeituraAsync(processo, cancellationToken).ConfigureAwait(false);
        InscricoesFechadas? fechadas = await _periodo.ConferirAsync(processo, agora, cancellationToken).ConfigureAwait(false);
        if (fechadas?.Motivo == MotivoDasInscricoesFechadas.PeriodoNaoConfirmado)
        {
            LogPeriodoNaoConfirmado(_logger, processo, fechadas.Recusa.Code);
        }

        return new SituacaoDoProcessoDaInscricao(divulgado?.Nome, fechadas?.Motivo);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "O período de inscrição do processo {ProcessoSeletivoId} não pôde ser confirmado para a lista de inscrições do candidato. Código do erro: {CodigoDoErro}.")]
    private static partial void LogPeriodoNaoConfirmado(ILogger logger, Guid processoSeletivoId, string codigoDoErro);
}
