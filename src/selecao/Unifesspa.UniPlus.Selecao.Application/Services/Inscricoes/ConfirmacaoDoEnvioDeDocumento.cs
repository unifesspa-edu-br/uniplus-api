namespace Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;

using Abstractions;

using Commands.DocumentosDaInscricao;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Microsoft.Extensions.Options;

/// <summary>Prepara o arquivo para verificação, disputando a revisão da inscrição antes de confirmar o envio.</summary>
public sealed class ConfirmacaoDoEnvioDeDocumento(
    IInscricaoRepository inscricoes,
    IDocumentoDaInscricaoRepository documentos,
    DocumentosEnviadosDaInscricao enviados,
    IOptions<ReconciliacaoDaVerificacaoOptions> reconciliacao,
    TimeProvider timeProvider)
{
    public async Task<Result<ArquivoDaApresentacao>> ConfirmarAsync(
        Inscricao inscricao, ArquivoDaApresentacao arquivo, ConfirmarEnvioDeDocumentoDaInscricaoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(arquivo);
        ArgumentNullException.ThrowIfNull(command);
        _ = await documentos.ObterApresentacaoAsync(arquivo.ApresentacaoId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"O arquivo {arquivo.Id} não tem apresentação.");
        DateTimeOffset agora = timeProvider.GetUtcNow();
        return ConferirArquivo(arquivo, command.ETag, agora) is { } recusa
            ? Result<ArquivoDaApresentacao>.Failure(recusa)
            : await ReivindicarAsync(inscricao, arquivo, command.ETag, agora, cancellationToken).ConfigureAwait(false);
    }

    private static DomainError? ConferirArquivo(ArquivoDaApresentacao arquivo, string etag, DateTimeOffset agora)
    {
        Result etagValido = ArquivoDaApresentacao.ValidarETag(etag);
        if (etagValido.IsFailure)
        {
            return etagValido.Error;
        }
        if (arquivo.Situacao != SituacaoDoArquivoDoCandidato.Pendente)
        {
            return ArquivoDaApresentacao.StatusInvalidoParaConfirmacao;
        }
        return arquivo.ExpiraEm + RemocaoDeArquivosPendentesVencidos.Tolerancia <= agora ? ArquivoDaApresentacao.ObjetoNaoEncontrado : null;
    }

    private async Task<Result<ArquivoDaApresentacao>> ReivindicarAsync(
        Inscricao inscricao, ArquivoDaApresentacao arquivo, string etag, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        PoliticaDeRepublicacao politica = reconciliacao.Value.Politica;
        if (!await documentos.TentarReivindicarConfirmacaoAsync(arquivo.Id, etag, agora, agora + politica.PrazoApos(1), cancellationToken).ConfigureAwait(false))
        {
            return Result<ArquivoDaApresentacao>.Failure(ArquivoDaApresentacao.StatusInvalidoParaConfirmacao);
        }
        if (!await inscricoes.TentarAvancarRevisaoAsync(inscricao.Id, inscricao.Revisao, apenasRascunho: false, cancellationToken).ConfigureAwait(false))
        {
            await documentos.DesfazerReivindicacaoAsync(arquivo.Id, cancellationToken).ConfigureAwait(false);
            return Result<ArquivoDaApresentacao>.Failure(AcessoAoDocumentoDaInscricao.InscricaoAlterada);
        }
        return ConfirmarReivindicado(arquivo, etag, politica);
    }

    private Result<ArquivoDaApresentacao> ConfirmarReivindicado(ArquivoDaApresentacao arquivo, string etag, PoliticaDeRepublicacao politica)
    {
        Result confirmacao = arquivo.Confirmar(etag, timeProvider, politica);
        if (confirmacao.IsFailure)
        {
            throw new InvalidOperationException(
                $"A confirmação do arquivo {arquivo.Id} falhou após a reivindicação atômica ({confirmacao.Error!.Code}); revertendo a transação.");
        }
        documentos.Atualizar(arquivo);
        return Result<ArquivoDaApresentacao>.Success(arquivo);
    }

    /// <summary>Relê os arquivos já confirmados para compor a resposta e o pedido de verificação no outbox.</summary>
    public async Task<(Result<ArquivoDaApresentacaoDto> Resposta, IEnumerable<object> Mensagens)> ResponderAsync(
        ArquivoDaApresentacao arquivo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        ArquivosDaInscricao daInscricao = await enviados.CarregarAsync(arquivo.InscricaoId, cancellationToken).ConfigureAwait(false);
        return (Result<ArquivoDaApresentacaoDto>.Success(daInscricao.ParaOCandidato().Single(a => a.ArquivoId == arquivo.Id)), [PedidoDeVerificacao.De(arquivo)]);
    }
}
