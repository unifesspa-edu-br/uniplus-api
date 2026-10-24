namespace Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;

using Microsoft.Extensions.Logging;

using Services;

using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// Registra o resultado da fila de resultados (ADR-0146). A fila entrega ao menos uma vez, então o arquivo que já
/// saiu de "em verificação" não é tocado. O veredicto "não definido" é erro. O "verificado" que não confere com o
/// declarado é recusado por falha fechada, porque deixá-lo em verificação o republicaria para sempre.
/// </summary>
public static partial class RegistrarResultadoDaVerificacaoDoArquivoHandler
{
    public static async Task Handle(
        ResultadoDaVerificacaoDeArquivo resultado,
        IDocumentoDaInscricaoRepository documentoRepository,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider clock,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(documentoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        Validar(resultado);

        // Perder a transição significa que outro resultado acabou de gravar o arquivo. Reaplicar sobre o que ficou
        // gravado mantém a limpeza do objeto promovido quando o vencedor foi uma recusa ou um pedido de reenvio.
        if (!await TentarRegistrarAsync(resultado, documentoRepository, unitOfWork, clock, logger, cancellationToken).ConfigureAwait(false))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            await TentarRegistrarAsync(resultado, documentoRepository, unitOfWork, clock, logger, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<bool> TentarRegistrarAsync(
        ResultadoDaVerificacaoDeArquivo resultado,
        IDocumentoDaInscricaoRepository documentoRepository,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider clock,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArquivoDaApresentacao? arquivo = await documentoRepository.ObterArquivoAsync(resultado.ArquivoId, cancellationToken).ConfigureAwait(false);
        if (arquivo is null)
        {
            LogArquivoInexistente(logger, resultado.ArquivoId);
        }

        (bool alterou, bool promovidoSemDono) = arquivo is null
            ? (false, resultado.Veredicto == VeredictoDaVerificacao.Verificado)
            : Aplicar(resultado, arquivo, logger);
        if (promovidoSemDono)
        {
            await documentoRepository
                .AgendarLimpezaAsync(ArquivoDaApresentacao.ChaveDefinitivaDe(resultado.ArquivoId), clock.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false);
        }

        if (!alterou && !promovidoSemDono)
        {
            if (arquivo is not null)
            {
                LogResultadoRepetido(logger, resultado.ArquivoId, arquivo.Situacao);
            }

            return true;
        }

        // A situação já mudou em memória; a transição condicional decide quem a grava. O arquivo é rastreado, então
        // o SaveChanges grava só as colunas que o domínio alterou, sem sobrescrever as que outra transação mudou.
        if (alterou
            && !await documentoRepository
                .TentarConcluirVerificacaoAsync(resultado.ArquivoId, arquivo!.Situacao, cancellationToken)
                .ConfigureAwait(false))
        {
            return false;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    // O verificador promoveu o arquivo ao bucket definitivo, mas ele não é mais do candidato — foi removido, ou já
    // terminou recusado ou a reenviar —, ou o resultado dele não confere: sem remover a chave, o objeto com dado do
    // candidato ficaria órfão no bucket.
    private static (bool Alterou, bool PromovidoSemDono) Aplicar(
        ResultadoDaVerificacaoDeArquivo resultado, ArquivoDaApresentacao arquivo, ILogger logger)
    {
        switch (resultado.Veredicto)
        {
            case VeredictoDaVerificacao.Verificado:
                if (arquivo.Situacao is SituacaoDoArquivoDoCandidato.Recusado or SituacaoDoArquivoDoCandidato.ReenvioNecessario)
                {
                    return (false, true);
                }

                long tamanho = resultado.TamanhoBytes!.Value;
                FormatoPermitido formato = FormatosDoArquivoDoCandidato.DoContrato(resultado.Formato)!.Value;
                if (arquivo.Situacao == SituacaoDoArquivoDoCandidato.EmVerificacao
                    && arquivo.DivergenciaDoVerificado(tamanho, resultado.HashSha256!, formato) is { } divergencia)
                {
                    LogResultadoDivergente(logger, resultado.ArquivoId, divergencia);
                    return (arquivo.RegistrarRecusa(MotivoDeRecusaDoArquivo.NaoPassouNaVerificacaoDeSeguranca), true);
                }

                return (arquivo.RegistrarVerificado(tamanho, resultado.HashSha256!, formato), false);
            case VeredictoDaVerificacao.Recusado:
                return (arquivo.RegistrarRecusa(MotivoDeRecusaDe(resultado)!.Value), false);
            default:
                return (arquivo.PedirReenvio(), false);
        }
    }

    // Antes de buscar o arquivo: o resultado malformado é defeito do contrato, com ou sem registro.
    private static void Validar(ResultadoDaVerificacaoDeArquivo resultado)
    {
        switch (resultado.Veredicto)
        {
            case VeredictoDaVerificacao.Verificado:
                if (resultado.TamanhoBytes is null
                    || string.IsNullOrWhiteSpace(resultado.HashSha256)
                    || FormatosDoArquivoDoCandidato.DoContrato(resultado.Formato) is null)
                {
                    throw new ResultadoDaVerificacaoInvalidoException(
                        $"O resultado \"verificado\" do arquivo {resultado.ArquivoId} não traz o tamanho, o SHA-256 e o formato.");
                }

                break;
            case VeredictoDaVerificacao.Recusado:
                if (MotivoDeRecusaDe(resultado) is null)
                {
                    throw new ResultadoDaVerificacaoInvalidoException(
                        $"O resultado \"recusado\" do arquivo {resultado.ArquivoId} não traz um motivo ({resultado.Motivo}).");
                }

                break;
            case VeredictoDaVerificacao.ReenvioNecessario:
                break;
            default:
                throw new ResultadoDaVerificacaoInvalidoException(
                    $"O resultado do arquivo {resultado.ArquivoId} traz o veredicto {resultado.Veredicto}, que não é um veredicto.");
        }
    }

    private static MotivoDeRecusaDoArquivo? MotivoDeRecusaDe(ResultadoDaVerificacaoDeArquivo resultado) => resultado.Motivo switch
    {
        MotivoDaRecusa.TamanhoExcedido => MotivoDeRecusaDoArquivo.TamanhoExcedido,
        MotivoDaRecusa.FormatoNaoCorresponde => MotivoDeRecusaDoArquivo.FormatoNaoCorresponde,
        MotivoDaRecusa.PdfComSenha => MotivoDeRecusaDoArquivo.PdfComSenha,
        MotivoDaRecusa.NaoPassouNaVerificacaoDeSeguranca => MotivoDeRecusaDoArquivo.NaoPassouNaVerificacaoDeSeguranca,
        _ => null,
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Resultado da verificação do arquivo {ArquivoId} descartado: o arquivo não existe mais")]
    private static partial void LogArquivoInexistente(ILogger logger, Guid arquivoId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resultado da verificação do arquivo {ArquivoId} ignorado: o arquivo já está {Situacao}")]
    private static partial void LogResultadoRepetido(ILogger logger, Guid arquivoId, SituacaoDoArquivoDoCandidato situacao);

    [LoggerMessage(Level = LogLevel.Error, Message = "O resultado \"verificado\" do arquivo {ArquivoId} diverge do declarado ({Divergencia}); recusado por falha fechada")]
    private static partial void LogResultadoDivergente(ILogger logger, Guid arquivoId, string divergencia);
}
