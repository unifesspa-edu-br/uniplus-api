namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A gravação de cada desfecho da confirmação da instalação. Com Idempotency-Key a violação é capturada e o rastreamento
/// descartado (ADR-0119): nada desta confirmação fica gravado, e o código sem a marca de gravação não prende a chave.
/// </summary>
internal static class GravacaoDaConfirmacao
{
    /// <summary>Grava; nula quando gravou, ou a recusa da violação reconhecida, com o rastreamento descartado.</summary>
    public static async Task<DomainError?> GravarAsync(IAutorizacaoUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (ConcessaoViolacoes.EhConcessaoDuplicada(ex) || ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();

            // A concessão duplicada diz que os gestores já estão instalados, ou que a origem já foi processada.
            return ConcessaoViolacoes.EhConcessaoDuplicada(ex)
                ? InstalacaoHandlerErros.JaHaGestorAoIniciar()
                : InstalacaoHandlerErros.ConflitoDeConcorrencia();
        }
    }
}
