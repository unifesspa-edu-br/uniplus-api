namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A gravação da instalação iniciada. A URL de PUT sai antes de gravar: se o armazenamento falhar, nada é gravado, nem o
/// cancelamento da pendência anterior. Cancelamento e nova pendência vão numa única gravação: se a segunda falhar, a
/// anterior não fica cancelada sozinha, e o índice único parcial admite uma pendente por executor.
/// </summary>
public sealed class GravacaoDaNovaInstalacao
{
    private const string TipoDeMidiaDoPdf = "application/pdf";

    // Validade da URL de PUT; o PDF de até 10 MB sobe dentro dela.
    private static readonly TimeSpan PrazoDoEnvio = TimeSpan.FromMinutes(15);

    private readonly IInstalacaoRepository _instalacoes;
    private readonly IArmazenamentoDosDocumentosDaAutorizacao _armazenamento;
    private readonly IUnitOfWork _unitOfWork;

    public GravacaoDaNovaInstalacao(
        IInstalacaoRepository instalacoes, IArmazenamentoDosDocumentosDaAutorizacao armazenamento, IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(instalacoes);
        ArgumentNullException.ThrowIfNull(armazenamento);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _instalacoes = instalacoes;
        _armazenamento = armazenamento;
        _unitOfWork = unitOfWork;
    }

    internal async Task<Result<InstalacaoIniciadaDto>> GravarAsync(
        Instalacao nova, Identidade executor, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        string url = await _armazenamento.GerarUrlDeEnvioAsync(nova.ChaveDeEnvio, TipoDeMidiaDoPdf, PrazoDoEnvio, cancellationToken).ConfigureAwait(false);
        Instalacao? anterior = await _instalacoes.ObterPendenteDoExecutorAsync(executor, cancellationToken).ConfigureAwait(false);
        anterior?.Cancelar(agora);
        await _instalacoes.AdicionarAsync(nova, cancellationToken).ConfigureAwait(false);

        return await GravacaoDaInstalacao.GravarAsync(_unitOfWork, cancellationToken).ConfigureAwait(false) is { } falha
            ? Result<InstalacaoIniciadaDto>.Failure(falha)
            : Result<InstalacaoIniciadaDto>.Success(Iniciada(nova, url, agora));
    }

    private static InstalacaoIniciadaDto Iniciada(Instalacao instalacao, string url, DateTimeOffset agora) =>
        new(instalacao.Id, SituacaoDaInstalacaoCodigo.AguardandoDocumento, new Uri(url, UriKind.Absolute), TipoDeMidiaDoPdf, agora + PrazoDoEnvio);
}
