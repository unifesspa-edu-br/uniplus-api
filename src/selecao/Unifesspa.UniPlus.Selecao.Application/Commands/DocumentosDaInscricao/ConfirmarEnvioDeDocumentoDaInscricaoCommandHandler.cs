namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;

using Domain.Entities;

using DTOs;

using Kernel.Results;

using Services;
using Services.Inscricoes;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>Confirma o envio do titular e devolve o pedido de verificação na mesma transação do arquivo.</summary>
public static class ConfirmarEnvioDeDocumentoDaInscricaoCommandHandler
{
    public static async Task<(Result<ArquivoDaApresentacaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        ConfirmarEnvioDeDocumentoDaInscricaoCommand command, AcessoAoDocumentoDaInscricao acesso,
        ConfirmacaoDoEnvioDeDocumento confirmacao, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(acesso);
        ArgumentNullException.ThrowIfNull(confirmacao);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        Result<Inscricao> inscricao = await acesso.ResolverInscricaoAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return inscricao.IsFailure
            ? Recusa(inscricao.Error!)
            : await ResolverArquivoAsync(inscricao.Value!, command, acesso, confirmacao, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(Result<ArquivoDaApresentacaoDto> Resposta, IEnumerable<object> Mensagens)> ResolverArquivoAsync(
        Inscricao inscricao, ConfirmarEnvioDeDocumentoDaInscricaoCommand command, AcessoAoDocumentoDaInscricao acesso,
        ConfirmacaoDoEnvioDeDocumento confirmacao, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result<ArquivoDaApresentacao> arquivo = await acesso.ResolverArquivoAsync(command.ArquivoId, inscricao, cancellationToken).ConfigureAwait(false);
        return arquivo.IsFailure
            ? Recusa(arquivo.Error!)
            : await ConfirmarEGravarAsync(inscricao, arquivo.Value!, command, confirmacao, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(Result<ArquivoDaApresentacaoDto> Resposta, IEnumerable<object> Mensagens)> ConfirmarEGravarAsync(
        Inscricao inscricao, ArquivoDaApresentacao arquivo, ConfirmarEnvioDeDocumentoDaInscricaoCommand command,
        ConfirmacaoDoEnvioDeDocumento confirmacao, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result<ArquivoDaApresentacao> resultado = await confirmacao.ConfirmarAsync(inscricao, arquivo, command, cancellationToken).ConfigureAwait(false);
        if (resultado.IsFailure)
        {
            return Recusa(resultado.Error!);
        }
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return await confirmacao.ResponderAsync(resultado.Value!, cancellationToken).ConfigureAwait(false);
    }

    private static (Result<ArquivoDaApresentacaoDto> Resposta, IEnumerable<object> Mensagens) Recusa(DomainError erro) =>
        (Result<ArquivoDaApresentacaoDto>.Failure(erro), []);
}
