namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A gravação de cada passo da instalação e a recusa das violações que a corrida entre pedidos pode causar. Com
/// Idempotency-Key a violação é capturada e o rastreamento descartado (ADR-0119): nada do passo fica gravado, e o código sem
/// a marca de gravação não prende a chave.
/// </summary>
internal static class GravacaoDaInstalacao
{
    private const string PendenciaUnicaConstraint = "ux_instalacoes_executor_pendente";

    /// <summary>Grava; nula quando gravou, ou a recusa da violação reconhecida, com o rastreamento descartado.</summary>
    public static async Task<DomainError?> GravarAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (EhViolacaoReconhecida(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();

            // A concessão duplicada diz que os gestores já estão instalados, ou que a origem já foi processada; a pendência
            // duplicada, que duas iniciações do mesmo executor correram juntas e a que perdeu recomeça.
            return ConcessaoViolacoes.EhConcessaoDuplicada(ex)
                ? InstalacaoHandlerErros.JaHaGestorAoIniciar()
                : InstalacaoHandlerErros.ConflitoDeConcorrencia();
        }
    }

    private static bool EhViolacaoReconhecida(Exception ex) =>
        ConcessaoViolacoes.EhConcessaoDuplicada(ex)
        || ViolacoesDeGravacao.ViolaIndiceUnico(ex, PendenciaUnicaConstraint)
        || ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex);
}
