namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// Registra o resultado da fila de resultados da Autorização (ADR-0146). A fila entrega ao menos uma vez, então o
/// resultado de instalação que já saiu de "documento em verificação" — repetido, ou depois do fim dela — é ignorado
/// com log. Todo veredicto tem transição: verificado grava o SHA-256; recusado e reenvio necessário encerram o
/// documento, e o executor recomeça. O veredicto indefinido é defeito do contrato.
/// </summary>
public static partial class RegistrarResultadoDaVerificacaoDoDocumentoDaInstalacaoHandler
{
    public static async Task Handle(
        ResultadoDaVerificacaoDeArquivo resultado,
        IInstalacaoRepository repository,
        IAutorizacaoUnitOfWork unitOfWork,
        TimeProvider clock,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        Validar(resultado);

        Instalacao? instalacao = await repository.ObterPorIdAsync(resultado.ArquivoId, cancellationToken).ConfigureAwait(false);
        if (instalacao is null)
        {
            LogInstalacaoInexistente(logger, resultado.ArquivoId);
            return;
        }

        SituacaoDaInstalacao situacao = instalacao.Situacao;
        bool alterou = resultado.Veredicto == VeredictoDaVerificacao.Verificado
            ? instalacao.RegistrarDocumentoVerificado(resultado.HashSha256!, clock.GetUtcNow())
            : instalacao.RegistrarDocumentoRecusado(clock.GetUtcNow());
        if (!alterou)
        {
            LogResultadoIgnorado(logger, resultado.ArquivoId, situacao);
            return;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Resultado da verificação do documento da instalação {InstalacaoId} descartado: a instalação não existe")]
    private static partial void LogInstalacaoInexistente(ILogger logger, Guid instalacaoId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resultado da verificação do documento da instalação {InstalacaoId} ignorado: a instalação está {Situacao}")]
    private static partial void LogResultadoIgnorado(ILogger logger, Guid instalacaoId, SituacaoDaInstalacao situacao);
}
