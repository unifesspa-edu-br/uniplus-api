namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A confirmação do envio do documento de designação: põe o PDF em verificação, com o ETag que o armazenamento devolveu, e
/// grava. Na recusa do passo nada fica gravado.
/// </summary>
public sealed class ConfirmacaoDoDocumentoDeDesignacao
{
    private readonly IInstalacaoRepository _instalacoes;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmacaoDoDocumentoDeDesignacao(IInstalacaoRepository instalacoes, IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(instalacoes);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _instalacoes = instalacoes;
        _unitOfWork = unitOfWork;
    }

    internal async Task<Result<Instalacao>> ConfirmarAsync(Guid instalacaoId, Identidade executor, string? etag, CancellationToken cancellationToken)
    {
        Instalacao? instalacao = await _instalacoes.ObterDoExecutorAsync(instalacaoId, executor, cancellationToken).ConfigureAwait(false);
        if (instalacao is null)
        {
            return Result<Instalacao>.Failure(InstalacaoHandlerErros.NaoEncontrada());
        }

        Result gravado = await GravacaoDaInstalacao.GravarPassoAsync(instalacao.ConfirmarDocumento(etag), _unitOfWork, cancellationToken).ConfigureAwait(false);

        return gravado.IsFailure ? Result<Instalacao>.ValidationFailure(gravado.Errors) : Result<Instalacao>.Success(instalacao);
    }
}
