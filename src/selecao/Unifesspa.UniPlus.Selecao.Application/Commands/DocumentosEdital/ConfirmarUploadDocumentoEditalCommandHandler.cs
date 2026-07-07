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

        // Atalho: o stat (HEAD) não traz o conteúdo, então barrar cedo pelo
        // tamanho já reportado evita abrir o stream no caso comum de um
        // envio já obviamente grande demais. Não é a proteção definitiva —
        // ObjectKey segue sobrescrevível até o TTL expirar (ver
        // ObjectKeyConfirmado), então o tamanho pode mudar entre este stat e
        // a leitura abaixo; quem garante o limite de fato é a leitura
        // limitada, não este atalho.
        if (info.TamanhoBytes > DocumentoEdital.TamanhoMaximoBytes)
        {
            return Result<DocumentoEditalDto>.Failure(new DomainError(
                "DocumentoEdital.TamanhoExcedido",
                $"O documento excede o tamanho máximo permitido de {DocumentoEdital.TamanhoMaximoBytes / (1024 * 1024)} MB."));
        }

        // Lê no máximo TamanhoMaximoBytes+1 — nunca mais que isso, não importa
        // o quanto o objeto real cresça entre o stat acima e esta leitura.
        // Sem o limite, um objeto substituído por algo muito maior depois do
        // stat faria o CopyToAsync bufferizar o arquivo inteiro em memória
        // antes de ValidarConteudo rejeitar pelo tamanho.
        byte[] bufferLimitado = new byte[DocumentoEdital.TamanhoMaximoBytes + 1];
        int totalLido = 0;
        Stream stream = await storage.AbrirLeituraAsync(documento.ObjectKey, cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            int lidos;
            while (totalLido < bufferLimitado.Length &&
                   (lidos = await stream.ReadAsync(bufferLimitado.AsMemory(totalLido), cancellationToken).ConfigureAwait(false)) > 0)
            {
                totalLido += lidos;
            }
        }

        if (totalLido > DocumentoEdital.TamanhoMaximoBytes)
        {
            return Result<DocumentoEditalDto>.Failure(new DomainError(
                "DocumentoEdital.TamanhoExcedido",
                $"O documento excede o tamanho máximo permitido de {DocumentoEdital.TamanhoMaximoBytes / (1024 * 1024)} MB."));
        }

        byte[] conteudo = bufferLimitado[..totalLido];

        Result validacao = DocumentoEdital.ValidarConteudo(conteudo.LongLength, info.ContentType, conteudo);
        if (validacao.IsFailure)
        {
            return Result<DocumentoEditalDto>.Failure(validacao.Error!);
        }

        string hashSha256 = Convert.ToHexStringLower(SHA256.HashData(conteudo));

        // Reivindica a confirmação só agora — depois de validar o conteúdo,
        // nunca antes: reivindicar cedo demais travaria o registro como
        // Confirmado para sempre se a validação recusasse em seguida. Duas
        // confirmações concorrentes do mesmo documento (Idempotency-Keys
        // diferentes) nunca ganham as duas — só a vencedora chega a escrever
        // a cópia selada, evitando hash/objeto de origens diferentes.
        bool reivindicou = await documentoEditalRepository
            .TentarReivindicarConfirmacaoAsync(documento.Id, cancellationToken)
            .ConfigureAwait(false);
        if (!reivindicou)
        {
            return Result<DocumentoEditalDto>.Failure(new DomainError(
                "DocumentoEdital.StatusInvalidoParaConfirmacao",
                "Somente um documento pendente pode ser confirmado."));
        }

        Result confirmacao = documento.Confirmar(conteudo.LongLength, hashSha256, clock);
        if (confirmacao.IsFailure)
        {
            return Result<DocumentoEditalDto>.Failure(confirmacao.Error!);
        }

        // A cópia selada é o que torna o documento confirmado imutável de
        // fato: ObjectKey (o alvo da URL de upload original) segue
        // sobrescrevível até o TTL expirar, mas ObjectKeyConfirmado nunca foi
        // exposto por nenhuma URL pre-assinada — só o handler grava nele.
        await storage.SalvarConteudoSeladoAsync(documento.ObjectKeyConfirmado!, conteudo, cancellationToken).ConfigureAwait(false);

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
