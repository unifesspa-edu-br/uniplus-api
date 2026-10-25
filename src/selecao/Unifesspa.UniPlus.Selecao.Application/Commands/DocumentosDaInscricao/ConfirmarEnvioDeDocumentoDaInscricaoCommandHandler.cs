namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Microsoft.Extensions.Options;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Grava o arquivo como em verificação e devolve o pedido como mensagem de saída, que o Wolverine instala no
/// outbox na mesma transação: não há arquivo em verificação sem pedido, nem pedido sem arquivo (ADR-0146).
/// </summary>
public static class ConfirmarEnvioDeDocumentoDaInscricaoCommandHandler
{
    public static async Task<(Result<ArquivoDaApresentacaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        ConfirmarEnvioDeDocumentoDaInscricaoCommand command,
        IInscricaoRepository inscricaoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IResolvedorFusoInstitucional resolvedorFuso,
        IDocumentoDaInscricaoRepository documentoRepository,
        ISelecaoUnitOfWork unitOfWork,
        IOptions<ReconciliacaoDaVerificacaoOptions> reconciliacao,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(documentoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(reconciliacao);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<Inscricao> acesso = await AcessoAoDocumentoDaInscricao
            .ResolverInscricaoAsync(
                command.ProcessoSeletivoId, userContext, inscricaoRepository, processoSeletivoRepository,
                certameDivulgadoRepository, registroCodecs, resolvedorFuso, timeProvider, cancellationToken)
            .ConfigureAwait(false);
        if (acesso.IsFailure)
        {
            return Recusa(acesso.Error!);
        }

        Inscricao inscricao = acesso.Value!;
        Result<ArquivoDaApresentacao> encontrado = await AcessoAoDocumentoDaInscricao
            .ResolverArquivoAsync(command.ArquivoId, inscricao, documentoRepository, cancellationToken)
            .ConfigureAwait(false);
        if (encontrado.IsFailure)
        {
            return Recusa(encontrado.Error!);
        }

        ArquivoDaApresentacao arquivo = encontrado.Value!;
        ApresentacaoDeDocumento apresentacao = await documentoRepository
            .ObterApresentacaoAsync(arquivo.ApresentacaoId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"O arquivo {arquivo.Id} não tem apresentação.");

        // As recusas por estado e por prazo vêm antes de disputar a revisão: a transação é confirmada mesmo na
        // recusa, e a revisão avançada à toa derrubaria os outros passos do envio.
        DateTimeOffset agora = timeProvider.GetUtcNow();
        Result etagValido = ArquivoDaApresentacao.ValidarETag(command.ETag);
        if (etagValido.IsFailure)
        {
            return Recusa(etagValido.Error!);
        }

        if (arquivo.Situacao != SituacaoDoArquivoDoCandidato.Pendente)
        {
            return Recusa(ArquivoDaApresentacao.StatusInvalidoParaConfirmacao);
        }

        if (arquivo.ExpiraEm + RemocaoDeArquivosPendentesVencidos.Tolerancia <= agora)
        {
            return Recusa(ArquivoDaApresentacao.ObjetoNaoEncontrado);
        }

        // A reivindicação vem antes de avançar a revisão: perdida para a remoção do pendente vencido, a recusa não
        // deixa uma revisão avançada à toa. Ela não sincroniza o rastreamento: o arquivo segue pendente em memória e
        // Confirmar não recusa.
        PoliticaDeRepublicacao politica = reconciliacao.Value.Politica;
        if (!await documentoRepository
            .TentarReivindicarConfirmacaoAsync(arquivo.Id, command.ETag, agora, agora + politica.PrazoApos(1), cancellationToken)
            .ConfigureAwait(false))
        {
            return Recusa(ArquivoDaApresentacao.StatusInvalidoParaConfirmacao);
        }

        // O arquivo só vale se a inscrição ainda é a que foi lida: o envio e o cancelamento disputam a mesma revisão.
        if (!await inscricaoRepository.TentarAvancarRevisaoAsync(inscricao.Id, inscricao.Revisao, apenasRascunho: false, cancellationToken).ConfigureAwait(false))
        {
            await documentoRepository.DesfazerReivindicacaoAsync(arquivo.Id, cancellationToken).ConfigureAwait(false);
            return Recusa(AcessoAoDocumentoDaInscricao.InscricaoAlterada);
        }

        Result confirmacao = arquivo.Confirmar(command.ETag, timeProvider, politica);
        if (confirmacao.IsFailure)
        {
            throw new InvalidOperationException(
                $"A confirmação do arquivo {arquivo.Id} falhou após a reivindicação atômica ({confirmacao.Error!.Code}); revertendo a transação.");
        }

        documentoRepository.Atualizar(arquivo);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        ArquivosDaInscricao daInscricao = await ArquivosDaInscricao
            .CarregarAsync(documentoRepository, arquivo.InscricaoId, cancellationToken).ConfigureAwait(false);
        return (
            Result<ArquivoDaApresentacaoDto>.Success(daInscricao.ParaOCandidato().Single(a => a.ArquivoId == arquivo.Id)),
            [PedidoDeVerificacao.De(arquivo)]);
    }

    private static (Result<ArquivoDaApresentacaoDto> Resposta, IEnumerable<object> Mensagens) Recusa(DomainError erro) =>
        (Result<ArquivoDaApresentacaoDto>.Failure(erro), []);
}
