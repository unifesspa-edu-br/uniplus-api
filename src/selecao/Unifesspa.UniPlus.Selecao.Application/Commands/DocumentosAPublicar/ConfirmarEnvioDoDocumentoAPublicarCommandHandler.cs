namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosAPublicar;

using System.Security.Cryptography;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using DTOs;

using Kernel.Results;

using Mappings;

using Services;

/// <summary>Handler de <see cref="ConfirmarEnvioDoDocumentoAPublicarCommand"/>.</summary>
public static class ConfirmarEnvioDoDocumentoAPublicarCommandHandler
{
    public static async Task<Result<DocumentoAPublicarDto>> Handle(
        ConfirmarEnvioDoDocumentoAPublicarCommand command,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IArquivoArmazenadoStorage storage,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        DocumentoAPublicar? documento = await documentoAPublicarRepository
            .ObterPorIdAsync(command.DocumentoAPublicarId, cancellationToken)
            .ConfigureAwait(false);
        if (documento is null || documento.ProcessoSeletivoId != command.ProcessoSeletivoId)
        {
            return Result<DocumentoAPublicarDto>.Failure(new DomainError(
                DocumentoAPublicarErrorCodes.NaoEncontrado, "Documento não encontrado neste Processo Seletivo."));
        }

        ArquivoEnviadoLido lido = await LeituraDoArquivoEnviado
            .LerAsync(storage, documento.ObjectKey, DocumentoAPublicar.TamanhoMaximoBytes, cancellationToken)
            .ConfigureAwait(false);
        if (lido.Situacao == SituacaoDoArquivoEnviado.Ausente)
        {
            return Result<DocumentoAPublicarDto>.Failure(new DomainError(
                DocumentoAPublicarErrorCodes.ObjetoNaoEncontrado,
                "O arquivo ainda não foi enviado ao armazenamento ou expirou antes da confirmação."));
        }

        if (lido.Situacao == SituacaoDoArquivoEnviado.Excedido)
        {
            return Result<DocumentoAPublicarDto>.Failure(DocumentoAPublicar.TamanhoExcedido);
        }

        Result validacao = documento.ValidarConteudo(lido.Conteudo.LongLength, lido.ContentType, lido.Conteudo);
        if (validacao.IsFailure)
        {
            return Result<DocumentoAPublicarDto>.Failure(validacao.Error!);
        }

        string hashSha256 = Convert.ToHexStringLower(SHA256.HashData(lido.Conteudo));

        // A reivindicação vem depois de validar — reivindicar antes travaria o documento como
        // confirmado se a validação recusasse — e garante que, entre confirmações concorrentes
        // (Idempotency-Keys diferentes), só a vencedora escreve a cópia selada.
        if (!await documentoAPublicarRepository.TentarReivindicarConfirmacaoAsync(documento.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result<DocumentoAPublicarDto>.Failure(new DomainError(
                DocumentoAPublicarErrorCodes.StatusInvalidoParaConfirmacao, "Somente um documento pendente pode ser confirmado."));
        }

        // Quem garante a exclusão mútua é a reivindicação condicional no banco, não a guarda de
        // Confirmar. A reivindicação não sincroniza o rastreamento, então o documento segue
        // pendente em memória e a guarda não dispara. Se disparar, o UPDATE já saiu e não há
        // resposta de negócio segura: lançar reverte a transação em vez de confirmar sem hash,
        // tamanho e cópia selada.
        Result confirmacao = documento.Confirmar(lido.Conteudo.LongLength, hashSha256, clock);
        if (confirmacao.IsFailure)
        {
            throw new InvalidOperationException(
                $"A confirmação do documento {documento.Id} falhou após a reivindicação atômica ({confirmacao.Error!.Code}); revertendo a transação.");
        }

        // A cópia selada é o que torna o documento confirmado imutável de fato: ObjectKey (alvo da
        // URL de envio) segue sobrescrevível até o TTL expirar, mas ObjectKeyConfirmado nunca foi
        // exposto por URL pré-assinada.
        await storage.SalvarConteudoSeladoAsync(documento.ObjectKeyConfirmado!, lido.Conteudo, documento.TipoDeMidia, cancellationToken).ConfigureAwait(false);

        documentoAPublicarRepository.Atualizar(documento);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<DocumentoAPublicarDto>.Success(DocumentoAPublicarMapping.ToDto(documento));
    }
}
