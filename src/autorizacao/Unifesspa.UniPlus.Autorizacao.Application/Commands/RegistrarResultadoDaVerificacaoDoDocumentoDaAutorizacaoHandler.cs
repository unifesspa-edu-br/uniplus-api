namespace Unifesspa.UniPlus.Autorizacao.Application.Commands;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// Registra o resultado da fila de resultados da Autorização (ADR-0146). A fila é uma só para os PDFs do módulo e o
/// resultado traz só o id do arquivo, então o handler despacha por consulta: procura a instalação por esse id e, não a
/// achando, o anexo de pedido; nenhum dos dois, log e descarte. Os ids são UUID v7 de tabelas distintas, e não colidem.
/// </summary>
/// <remarks>
/// A fila entrega ao menos uma vez, então o resultado de quem já saiu de "em verificação" — repetido, ou depois do fim
/// da instalação — é ignorado com log. Todo veredicto tem transição: verificado grava o SHA-256; recusado e reenvio
/// necessário encerram o documento, e quem enviou recomeça. O veredicto indefinido é defeito do contrato.
/// O pedido que o documento libera avisa a próxima pessoa pelo outbox, na mesma gravação.
/// </remarks>
public static partial class RegistrarResultadoDaVerificacaoDoDocumentoDaAutorizacaoHandler
{
    public static async Task<IEnumerable<EnviarEmail>> Handle(
        ResultadoDaVerificacaoDeArquivo resultado,
        IInstalacaoRepository instalacoes,
        IPedidoDeConcessaoRepository pedidos,
        IPerfilRepository perfis,
        AvisosDoPedido avisos,
        IAutorizacaoUnitOfWork unitOfWork,
        TimeProvider clock,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(instalacoes);
        ArgumentNullException.ThrowIfNull(pedidos);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        Validar(resultado);

        bool alterou;
        IReadOnlyList<EnviarEmail> mensagens = [];
        Instalacao? instalacao = await instalacoes.ObterPorIdAsync(resultado.ArquivoId, cancellationToken).ConfigureAwait(false);
        if (instalacao is not null)
        {
            SituacaoDaInstalacao situacao = instalacao.Situacao;
            alterou = resultado.Veredicto == VeredictoDaVerificacao.Verificado
                ? instalacao.RegistrarDocumentoVerificado(resultado.HashSha256!, clock.GetUtcNow())
                : instalacao.RegistrarDocumentoRecusado(clock.GetUtcNow());
            if (!alterou)
            {
                LogResultadoIgnorado(logger, resultado.ArquivoId, situacao);
                return [];
            }
        }
        else
        {
            AnexoDoPedido? anexo = await pedidos.ObterAnexoAsync(resultado.ArquivoId, cancellationToken).ConfigureAwait(false);
            if (anexo is null)
            {
                LogArquivoInexistente(logger, resultado.ArquivoId);
                return [];
            }

            SituacaoDoAnexo situacao = anexo.Situacao;
            alterou = resultado.Veredicto == VeredictoDaVerificacao.Verificado
                ? anexo.RegistrarVerificado(resultado.HashSha256!, clock.GetUtcNow())
                : anexo.RegistrarRecusado(clock.GetUtcNow());
            if (!alterou)
            {
                LogAnexoIgnorado(logger, resultado.ArquivoId, situacao);
                return [];
            }

            // O documento verificado libera o pedido que o esperava; o anexo e o pedido saem na mesma gravação.
            if (resultado.Veredicto == VeredictoDaVerificacao.Verificado
                && await pedidos.ObterParaAlterarAsync(anexo.PedidoId, cancellationToken).ConfigureAwait(false) is { } pedido
                && pedido.LiberarAoVerificarDocumento())
            {
                pedidos.Atualizar(pedido);
                Perfil? perfil = await perfis.ObterPorIdAsync(pedido.PerfilId, cancellationToken).ConfigureAwait(false);
                mensagens = await avisos
                    .AvisarAsync(pedido, perfil?.Nome ?? string.Empty, SituacaoDoPedido.AguardandoDocumento, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return mensagens;
    }

    private static void Validar(ResultadoDaVerificacaoDeArquivo resultado)
    {
        switch (resultado.Veredicto)
        {
            case VeredictoDaVerificacao.Verificado:
                if (string.IsNullOrWhiteSpace(resultado.HashSha256))
                {
                    throw new ResultadoDaVerificacaoInvalidoException(
                        $"O resultado \"verificado\" do documento {resultado.ArquivoId} não traz o SHA-256.");
                }

                break;
            case VeredictoDaVerificacao.Recusado:
            case VeredictoDaVerificacao.ReenvioNecessario:
                break;
            default:
                throw new ResultadoDaVerificacaoInvalidoException(
                    $"O resultado do documento {resultado.ArquivoId} traz o veredicto {resultado.Veredicto}, que não é um veredicto.");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Resultado da verificação do documento {ArquivoId} da Autorização descartado: nem a instalação nem o anexo de pedido existem")]
    private static partial void LogArquivoInexistente(ILogger logger, Guid arquivoId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resultado da verificação do documento da instalação {InstalacaoId} ignorado: a instalação está {Situacao}")]
    private static partial void LogResultadoIgnorado(ILogger logger, Guid instalacaoId, SituacaoDaInstalacao situacao);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resultado da verificação do anexo do pedido {AnexoId} ignorado: o anexo está {Situacao}")]
    private static partial void LogAnexoIgnorado(ILogger logger, Guid anexoId, SituacaoDoAnexo situacao);
}
