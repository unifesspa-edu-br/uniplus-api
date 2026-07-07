namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosEdital;

using System.Security.Cryptography;

using Abstractions;
using DTOs;
using Domain.Entities;
using Domain.Interfaces;
using Kernel.Results;

/// <summary>
/// Handler convention-based de <see cref="ConfirmarUploadDocumentoEditalCommand"/>.
/// </summary>
public static class ConfirmarUploadDocumentoEditalCommandHandler
{
    public static async Task<Result<DocumentoEditalDto>> Handle(
        ConfirmarUploadDocumentoEditalCommand command,
        IDocumentoEditalRepository documentoEditalRepository,
        IDocumentoEditalStorage storage,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(documentoEditalRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        DocumentoEdital? documento = await documentoEditalRepository
            .ObterPorIdAsync(command.DocumentoEditalId, cancellationToken)
            .ConfigureAwait(false);
        if (documento is null || documento.ProcessoSeletivoId != command.ProcessoSeletivoId)
        {
            return Result<DocumentoEditalDto>.Failure(new DomainError(
                "DocumentoEdital.NaoEncontrado", "Documento do Edital não encontrado."));
        }

        InfoObjetoArmazenado? info = await storage
            .ObterInfoAsync(documento.ObjectKey, cancellationToken)
            .ConfigureAwait(false);
        if (info is null)
        {
            return Result<DocumentoEditalDto>.Failure(new DomainError(
                "DocumentoEdital.ObjetoNaoEncontrado",
                "O objeto ainda não foi enviado ao storage ou expirou antes da confirmação."));
        }

        // Corta aqui, antes de baixar: o stat (HEAD) não traz o conteúdo, então
        // barrar pelo tamanho reportado evita carregar um objeto arbitrariamente
        // grande inteiro em memória só para descobrir que ele será recusado.
        if (info.TamanhoBytes > DocumentoEdital.TamanhoMaximoBytes)
        {
            return Result<DocumentoEditalDto>.Failure(new DomainError(
                "DocumentoEdital.TamanhoExcedido",
                $"O documento excede o tamanho máximo permitido de {DocumentoEdital.TamanhoMaximoBytes / (1024 * 1024)} MB."));
        }

        byte[] conteudo;
        Stream stream = await storage.AbrirLeituraAsync(documento.ObjectKey, cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using MemoryStream buffer = new();
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            conteudo = buffer.ToArray();
        }

        Result validacao = DocumentoEdital.ValidarConteudo(conteudo.LongLength, info.ContentType, conteudo);
        if (validacao.IsFailure)
        {
            return Result<DocumentoEditalDto>.Failure(validacao.Error!);
        }

        string hashSha256 = Convert.ToHexStringLower(SHA256.HashData(conteudo));

        Result confirmacao = documento.Confirmar(conteudo.LongLength, hashSha256, clock);
        if (confirmacao.IsFailure)
        {
            return Result<DocumentoEditalDto>.Failure(confirmacao.Error!);
        }

        documentoEditalRepository.Atualizar(documento);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<DocumentoEditalDto>.Success(new DocumentoEditalDto(
            documento.Id,
            documento.ProcessoSeletivoId,
            documento.Status.ToString(),
            documento.TamanhoBytes!.Value,
            documento.HashSha256!,
            documento.ConfirmadoEm!.Value));
    }
}
