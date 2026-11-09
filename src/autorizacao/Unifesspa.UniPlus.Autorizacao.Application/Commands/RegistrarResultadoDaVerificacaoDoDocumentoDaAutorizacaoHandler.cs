namespace Unifesspa.UniPlus.Autorizacao.Application.Commands;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.ResultadosDaVerificacao;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// Registra o resultado da fila de resultados da Autorização (ADR-0146). A fila é uma só para os PDFs do módulo e o
/// resultado traz só o id do arquivo, então o handler percorre os receptores, na ordem de registro, até um reconhecer o
/// arquivo (instalação, anexo de pedido e documento de versão de perfil); nenhum, log e descarte. Os ids são UUID v7 de
/// tabelas distintas, e não colidem.
/// </summary>
/// <remarks>
/// A fila entrega ao menos uma vez, então o resultado de quem já saiu de "em verificação" é ignorado com log pelo
/// receptor. Todo veredicto tem transição: verificado grava o SHA-256; recusado e reenvio necessário encerram o
/// documento, e quem enviou recomeça. O veredicto indefinido é defeito do contrato.
/// </remarks>
public static partial class RegistrarResultadoDaVerificacaoDoDocumentoDaAutorizacaoHandler
{
    public static async Task<IEnumerable<EnviarEmail>> Handle(
        ResultadoDaVerificacaoDeArquivo resultado,
        IEnumerable<IReceptorDoResultadoDaVerificacao> receptores,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(receptores);
        ArgumentNullException.ThrowIfNull(logger);

        Validar(resultado);

        foreach (IReceptorDoResultadoDaVerificacao receptor in receptores)
        {
            DesfechoDoRecebimento desfecho = await receptor.ReceberAsync(resultado, cancellationToken).ConfigureAwait(false);
            if (desfecho.Reconhecido)
            {
                return desfecho.Mensagens;
            }
        }

        LogArquivoInexistente(logger, resultado.ArquivoId);

        return [];
    }

    private static void Validar(ResultadoDaVerificacaoDeArquivo resultado)
    {
        string? defeito = resultado.Veredicto switch
        {
            VeredictoDaVerificacao.Verificado when string.IsNullOrWhiteSpace(resultado.HashSha256) =>
                $"O resultado \"verificado\" do documento {resultado.ArquivoId} não traz o SHA-256.",
            VeredictoDaVerificacao.Verificado or VeredictoDaVerificacao.Recusado or VeredictoDaVerificacao.ReenvioNecessario => null,
            _ => $"O resultado do documento {resultado.ArquivoId} traz o veredicto {resultado.Veredicto}, que não é um veredicto.",
        };

        if (defeito is not null)
        {
            throw new ResultadoDaVerificacaoInvalidoException(defeito);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Resultado da verificação do documento {ArquivoId} da Autorização descartado: nem a instalação, nem o anexo de pedido, nem o documento de versão de perfil existem")]
    private static partial void LogArquivoInexistente(ILogger logger, Guid arquivoId);
}
