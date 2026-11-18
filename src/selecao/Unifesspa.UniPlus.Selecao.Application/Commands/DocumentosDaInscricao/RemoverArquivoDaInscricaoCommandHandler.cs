namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;

using Domain.Entities;

using Kernel.Results;

using Services;
using Services.Inscricoes;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>Confere o acesso do titular e confirma a remoção do arquivo com a agenda de limpeza.</summary>
public static class RemoverArquivoDaInscricaoCommandHandler
{
    public static async Task<Result> Handle(
        RemoverArquivoDaInscricaoCommand command, AcessoAoDocumentoDaInscricao acesso,
        RemocaoDoArquivoDaInscricao remocao, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(acesso);
        ArgumentNullException.ThrowIfNull(remocao);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        Result<Inscricao> inscricao = await acesso.ResolverInscricaoAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return inscricao.IsFailure
            ? Result.Failure(inscricao.Error!)
            : await ResolverArquivoAsync(inscricao.Value!, command.ArquivoId, acesso, remocao, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> ResolverArquivoAsync(
        Inscricao inscricao, Guid arquivoId, AcessoAoDocumentoDaInscricao acesso,
        RemocaoDoArquivoDaInscricao remocao, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result<ArquivoDaApresentacao> arquivo = await acesso.ResolverArquivoAsync(arquivoId, inscricao, cancellationToken).ConfigureAwait(false);
        return arquivo.IsFailure ? Result.Failure(arquivo.Error!)
            : await RemoverEConfirmarAsync(inscricao, arquivo.Value!, remocao, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> RemoverEConfirmarAsync(
        Inscricao inscricao, ArquivoDaApresentacao arquivo, RemocaoDoArquivoDaInscricao remocao,
        IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result resultado = await remocao.RemoverAsync(inscricao, arquivo, cancellationToken).ConfigureAwait(false);
        if (resultado.IsSuccess)
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        return resultado;
    }
}
