namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Handler de <see cref="RemoverArquivoDaInscricaoCommand"/>. Só o rascunho remove: depois do envio da
/// inscrição o arquivo é retido. Apaga o registro e as duas chaves do armazenamento, a do envio e a selada.
/// </summary>
public static class RemoverArquivoDaInscricaoCommandHandler
{
    public static async Task<Result> Handle(
        RemoverArquivoDaInscricaoCommand command,
        IInscricaoRepository inscricaoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IResolvedorFusoInstitucional resolvedorFuso,
        IDocumentoDaInscricaoRepository documentoRepository,
        IArquivoArmazenadoStorage storage,
        ISelecaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(documentoRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<Inscricao> acesso = await AcessoAoDocumentoDaInscricao
            .ResolverInscricaoAsync(
                command.ProcessoSeletivoId, userContext, inscricaoRepository, processoSeletivoRepository,
                certameDivulgadoRepository, registroCodecs, resolvedorFuso, timeProvider, cancellationToken)
            .ConfigureAwait(false);
        if (acesso.IsFailure)
        {
            return Result.Failure(acesso.Error!);
        }

        Inscricao inscricao = acesso.Value!;
        Result<ArquivoDaApresentacao> encontrado = await AcessoAoDocumentoDaInscricao
            .ResolverArquivoAsync(command.ArquivoId, inscricao, documentoRepository, cancellationToken)
            .ConfigureAwait(false);
        if (encontrado.IsFailure)
        {
            return Result.Failure(encontrado.Error!);
        }

        if (!inscricao.EhRascunho)
        {
            return Result.Failure(new DomainError(
                DocumentoDaInscricaoErrorCodes.RemocaoSoNoRascunho,
                "Sua inscrição já foi enviada: o arquivo fica retido e não pode ser removido."));
        }

        if (!await inscricaoRepository.TentarAvancarRevisaoAsync(inscricao.Id, inscricao.Revisao, apenasRascunho: true, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(AcessoAoDocumentoDaInscricao.InscricaoAlterada);
        }

        ArquivoDaApresentacao arquivo = encontrado.Value!;
        await documentoRepository.RemoverArquivoAsync(arquivo, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        // As duas chaves saem: a do envio e a selada. A remoção do objeto ausente conclui.
        await storage.RemoverAsync(arquivo.ObjectKey, cancellationToken).ConfigureAwait(false);
        await storage.RemoverAsync(arquivo.ChaveSelada, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
