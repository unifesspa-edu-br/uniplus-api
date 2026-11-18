namespace Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using Kernel.Results;

/// <summary>Remove o arquivo do rascunho e agenda a limpeza posterior do armazenamento.</summary>
public sealed class RemocaoDoArquivoDaInscricao(
    IInscricaoRepository inscricoes, IDocumentoDaInscricaoRepository documentos, TimeProvider timeProvider)
{
    public async Task<Result> RemoverAsync(Inscricao inscricao, ArquivoDaApresentacao arquivo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(arquivo);
        if (!inscricao.EhRascunho)
        {
            return Result.Failure(new DomainError(DocumentoDaInscricaoErrorCodes.RemocaoSoNoRascunho,
                "Sua inscrição já foi enviada: o arquivo fica retido e não pode ser removido."));
        }
        return await inscricoes.TentarAvancarRevisaoAsync(inscricao.Id, inscricao.Revisao, apenasRascunho: true, cancellationToken).ConfigureAwait(false)
            ? await RemoverEAgendarAsync(arquivo, cancellationToken).ConfigureAwait(false)
            : Result.Failure(AcessoAoDocumentoDaInscricao.InscricaoAlterada);
    }

    private async Task<Result> RemoverEAgendarAsync(ArquivoDaApresentacao arquivo, CancellationToken cancellationToken)
    {
        await documentos.RemoverArquivoAsync(arquivo, cancellationToken).ConfigureAwait(false);
        await AgendaDeLimpezaDoArquivoDoCandidato.AgendarAsync(documentos, arquivo, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
