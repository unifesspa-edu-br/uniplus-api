namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using DTOs;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Handler de <see cref="ConfirmarEnvioDeDocumentoDaInscricaoCommand"/>. O antivírus olha o conteúdo ainda
/// em claro na memória, antes de qualquer gravação: o infectado e o não verificado recusam a confirmação
/// e o arquivo continua pendente. O que se guarda é o conteúdo cifrado, e o objeto do envio, em claro,
/// sai logo depois.
/// </summary>
public static class ConfirmarEnvioDeDocumentoDaInscricaoCommandHandler
{
    private static ConfirmacaoDoArquivoEnviado.Recusas RecusasDe(ArquivoDaApresentacao arquivo) => new(
        ObjetoNaoEncontrado: new DomainError(
            DocumentoDaInscricaoErrorCodes.ObjetoNaoEncontrado,
            "O arquivo ainda não foi enviado ao armazenamento ou expirou antes da confirmação."),
        TamanhoExcedido: ArquivoDaApresentacao.TamanhoExcedidoAte(arquivo.TamanhoMaximoBytes),
        StatusInvalido: ArquivoDaApresentacao.StatusInvalidoParaConfirmacao);

    public static async Task<Result<ArquivoDaApresentacaoDto>> Handle(
        ConfirmarEnvioDeDocumentoDaInscricaoCommand command,
        IInscricaoRepository inscricaoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IResolvedorFusoInstitucional resolvedorFuso,
        IDocumentoDaInscricaoRepository documentoRepository,
        IArquivoArmazenadoStorage storage,
        IProtecaoDoConteudoSelado protecao,
        ISelecaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(documentoRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(protecao);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<Inscricao> acesso = await AcessoAoDocumentoDaInscricao
            .ResolverInscricaoAsync(
                command.ProcessoSeletivoId, userContext, inscricaoRepository, processoSeletivoRepository,
                certameDivulgadoRepository, registroCodecs, resolvedorFuso, timeProvider, cancellationToken)
            .ConfigureAwait(false);
        if (acesso.IsFailure)
        {
            return Result<ArquivoDaApresentacaoDto>.Failure(acesso.Error!);
        }

        Inscricao inscricao = acesso.Value!;
        Result<ArquivoDaApresentacao> encontrado = await AcessoAoDocumentoDaInscricao
            .ResolverArquivoAsync(command.ArquivoId, inscricao, documentoRepository, cancellationToken)
            .ConfigureAwait(false);
        if (encontrado.IsFailure)
        {
            return Result<ArquivoDaApresentacaoDto>.Failure(encontrado.Error!);
        }

        ArquivoDaApresentacao arquivo = encontrado.Value!;
        ApresentacaoDeDocumento apresentacao = await documentoRepository
            .ObterApresentacaoAsync(arquivo.ApresentacaoId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"O arquivo {arquivo.Id} não tem apresentação.");

        // O arquivo só vale se a inscrição ainda é a que foi lida: o envio e o cancelamento disputam a
        // mesma revisão, na transação desta confirmação.
        Result confirmacao = await ConfirmacaoDoArquivoEnviado
            .ConfirmarAsync(
                arquivo, arquivo.TamanhoMaximoBytes, RecusasDe(arquivo), documentoRepository, storage, timeProvider, cancellationToken,
                protecao,
                async ct => await inscricaoRepository.TentarAvancarRevisaoAsync(inscricao.Id, inscricao.Revisao, apenasRascunho: false, ct).ConfigureAwait(false)
                    ? Result.Success()
                    : Result.Failure(AcessoAoDocumentoDaInscricao.InscricaoAlterada))
            .ConfigureAwait(false);
        if (confirmacao.IsFailure)
        {
            return Result<ArquivoDaApresentacaoDto>.Failure(confirmacao.Error!);
        }

        documentoRepository.Atualizar(arquivo);

        // A URL de envio vale até ExpiraEm: um PUT nela recria o conteúdo em claro, que a rotina apaga depois.
        await AgendaDeLimpezaDoArquivoDoCandidato.AgendarAsync(documentoRepository, arquivo, comSelada: false, timeProvider.GetUtcNow(), cancellationToken, envioTambemAgora: true).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<ArquivoDaApresentacaoDto>.Success(new ArquivoDaApresentacaoDto(
            arquivo.Id, arquivo.ApresentacaoId, apresentacao.ExigenciaId, apresentacao.OcorrenciaId,
            arquivo.TipoDeMidia, arquivo.TamanhoBytes!.Value, arquivo.HashSha256!, arquivo.ConfirmadoEm!.Value));
    }
}
