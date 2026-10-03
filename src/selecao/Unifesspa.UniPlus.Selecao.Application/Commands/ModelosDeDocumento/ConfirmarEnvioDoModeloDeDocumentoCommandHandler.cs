namespace Unifesspa.UniPlus.Selecao.Application.Commands.ModelosDeDocumento;

using System.Security.Cryptography;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using DTOs;

using Kernel.Results;

using Mappings;

using Services;

/// <summary>Handler de <see cref="ConfirmarEnvioDoModeloDeDocumentoCommand"/>.</summary>
public static class ConfirmarEnvioDoModeloDeDocumentoCommandHandler
{
    public static async Task<Result<ModeloDeDocumentoDto>> Handle(
        ConfirmarEnvioDoModeloDeDocumentoCommand command,
        IModeloDeDocumentoRepository modeloDeDocumentoRepository,
        IArquivoArmazenadoStorage storage,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(modeloDeDocumentoRepository);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        ModeloDeDocumento? modelo = await modeloDeDocumentoRepository
            .ObterPorIdAsync(command.ModeloDeDocumentoId, cancellationToken)
            .ConfigureAwait(false);
        if (modelo is null || modelo.ProcessoSeletivoId != command.ProcessoSeletivoId)
        {
            return Result<ModeloDeDocumentoDto>.Failure(new DomainError(
                ModeloDeDocumentoErrorCodes.NaoEncontrado, "Modelo de documento não encontrado neste Processo Seletivo."));
        }

        ArquivoEnviadoLido lido = await LeituraDoArquivoEnviado
            .LerAsync(storage, modelo.ObjectKey, ModeloDeDocumento.TamanhoMaximoBytes, cancellationToken)
            .ConfigureAwait(false);
        if (lido.Situacao == SituacaoDoArquivoEnviado.Ausente)
        {
            return Result<ModeloDeDocumentoDto>.Failure(new DomainError(
                ModeloDeDocumentoErrorCodes.ObjetoNaoEncontrado,
                "O arquivo ainda não foi enviado ao storage ou expirou antes da confirmação."));
        }

        if (lido.Situacao == SituacaoDoArquivoEnviado.Excedido)
        {
            return Result<ModeloDeDocumentoDto>.Failure(ModeloDeDocumento.TamanhoExcedido);
        }

        Result validacao = modelo.ValidarConteudo(lido.Conteudo.LongLength, lido.ContentType, lido.Conteudo);
        if (validacao.IsFailure)
        {
            return Result<ModeloDeDocumentoDto>.Failure(validacao.Error!);
        }

        string hashSha256 = Convert.ToHexStringLower(SHA256.HashData(lido.Conteudo));

        // A reivindicação vem depois de validar — reivindicar antes travaria o modelo como
        // confirmado se a validação recusasse — e garante que, entre confirmações concorrentes, só
        // a vencedora escreve a cópia selada.
        if (!await modeloDeDocumentoRepository.TentarReivindicarConfirmacaoAsync(modelo.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result<ModeloDeDocumentoDto>.Failure(new DomainError(
                ModeloDeDocumentoErrorCodes.StatusInvalidoParaConfirmacao, "Somente um modelo pendente pode ser confirmado."));
        }

        // A reivindicação não sincroniza o rastreamento, então o modelo segue pendente em memória e
        // a guarda de Confirmar não dispara. Se disparar, o UPDATE já saiu e não há resposta de
        // negócio segura: lançar reverte a transação.
        Result confirmacao = modelo.Confirmar(lido.Conteudo.LongLength, hashSha256, clock);
        if (confirmacao.IsFailure)
        {
            throw new InvalidOperationException(
                $"A confirmação do modelo {modelo.Id} falhou após a reivindicação atômica ({confirmacao.Error!.Code}); revertendo a transação.");
        }

        await storage.SalvarConteudoSeladoAsync(modelo.ObjectKeyConfirmado!, lido.Conteudo, modelo.ContentType, cancellationToken).ConfigureAwait(false);

        modeloDeDocumentoRepository.Atualizar(modelo);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<ModeloDeDocumentoDto>.Success(ModeloDeDocumentoMapping.ToDto(modelo));
    }
}
