namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosEdital;

using System.Security.Cryptography;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Kernel.Results;

using Mappings;

using Services;

/// <summary>
/// Handler convention-based de <see cref="ConfirmarUploadDocumentoEditalCommand"/>.
/// </summary>
public static class ConfirmarUploadDocumentoEditalCommandHandler
{
    public static async Task<Result<DocumentoEditalDto>> Handle(
        ConfirmarUploadDocumentoEditalCommand command,
        IDocumentoEditalRepository documentoEditalRepository,
        IArquivoArmazenadoStorage storage,
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

        ArquivoEnviadoLido lido = await LeituraDoArquivoEnviado
            .LerAsync(storage, documento.ObjectKey, DocumentoEdital.TamanhoMaximoBytes, cancellationToken)
            .ConfigureAwait(false);
        if (lido.Situacao == SituacaoDoArquivoEnviado.Ausente)
        {
            return Result<DocumentoEditalDto>.Failure(new DomainError(
                "DocumentoEdital.ObjetoNaoEncontrado",
                "O objeto ainda não foi enviado ao storage ou expirou antes da confirmação."));
        }

        if (lido.Situacao == SituacaoDoArquivoEnviado.Excedido)
        {
            return Result<DocumentoEditalDto>.Failure(new DomainError(
                "DocumentoEdital.TamanhoExcedido",
                $"O documento excede o tamanho máximo permitido de {DocumentoEdital.TamanhoMaximoBytes / (1024 * 1024)} MB."));
        }

        byte[] conteudo = lido.Conteudo;

        Result validacao = DocumentoEdital.ValidarConteudo(conteudo.LongLength, lido.ContentType, conteudo);
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

        // Quem garante a exclusão mútua é a reivindicação condicional no banco, não
        // a guarda de Confirmar(...). Hoje documento.Status continua Pendente em
        // memória — o ExecuteUpdate da reivindicação não sincroniza o rastreamento —,
        // então a guarda não dispara. Se a entidade passar a ser recarregada depois
        // da reivindicação, a guarda recusará; nesse ponto o UPDATE já foi emitido e
        // não há resposta de negócio segura: lançar força a reversão da transação em
        // vez de confirmar Status=Confirmado sem hash, tamanho e cópia selada.
        Result confirmacao = documento.Confirmar(conteudo.LongLength, hashSha256, clock);
        if (confirmacao.IsFailure)
        {
            throw new InvalidOperationException(
                $"A confirmação do documento {documento.Id} falhou após a reivindicação atômica já ter "
                + $"avançado o status ({confirmacao.Error!.Code}) — estado inconsistente após a reivindicação, "
                + "revertendo a transação.");
        }

        // A cópia selada é o que torna o documento confirmado imutável de
        // fato: ObjectKey (o alvo da URL de upload original) segue
        // sobrescrevível até o TTL expirar, mas ObjectKeyConfirmado nunca foi
        // exposto por nenhuma URL pre-assinada — só o handler grava nele.
        await storage.SalvarConteudoSeladoAsync(documento.ObjectKeyConfirmado!, conteudo, DocumentoEdital.ContentTypeEsperado, cancellationToken).ConfigureAwait(false);

        documentoEditalRepository.Atualizar(documento);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<DocumentoEditalDto>.Success(DocumentoEditalMapping.ToDto(documento));
    }
}
