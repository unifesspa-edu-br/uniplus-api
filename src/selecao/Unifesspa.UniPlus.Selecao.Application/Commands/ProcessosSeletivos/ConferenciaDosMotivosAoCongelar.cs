namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.MotivosDeDecisao;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Os últimos portões da publicação, da retificação de uma vez e do fechamento da sessão, depois de
/// todos os outros: a definição avaliável do processo e, por fim, os motivos escolhidos. Os motivos
/// ainda não congelados recopiam a descrição vigente e recusam o que veio de motivo desativado
/// (UNI-REQ-0122); a cobertura que as decisões do processo exigem é conferida
/// (<see cref="CoberturaDosMotivos"/>, UNI-REQ-0123); só então a escolha congela.
/// </summary>
/// <remarks>
/// O congelamento acontece antes de a transição do agregado aceitar a versão; se algo depois dele
/// recusar, o descarte do rastreamento que envolve as três operações desfaz também este.
/// </remarks>
internal static class ConferenciaDosMotivosAoCongelar
{
    /// <summary>A definição avaliável do processo, ou a primeira recusa: a da definição precede a dos motivos.</summary>
    internal static async Task<Result<DefinicaoAvaliavel>> UltimosPortoesAsync(
        IProcessoSeletivoRepository repository, ProcessoSeletivo processo, IReadOnlyCollection<FatoCandidatoView> catalogoDeFatos, CancellationToken cancellationToken)
    {
        Result<DefinicaoAvaliavel> avaliavel = DefinicaoAvaliavelDoProcesso.DaConfiguracaoViva(processo, catalogoDeFatos);
        if (avaliavel.IsFailure)
        {
            return avaliavel;
        }

        Result motivos = await ConferirAsync(repository, processo, cancellationToken).ConfigureAwait(false);
        return motivos.IsSuccess ? avaliavel : Result<DefinicaoAvaliavel>.ValidationFailure(motivos.Errors);
    }

    internal static async Task<Result> ConferirAsync(
        IProcessoSeletivoRepository repository, ProcessoSeletivo processo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(processo);

        IReadOnlyList<MotivoEscolhidoComOrigem> escolhidos = await repository.ListarMotivosEscolhidosAsync(processo.Id, cancellationToken).ConfigureAwait(false);
        MotivoDoProcesso[] motivos = [.. escolhidos.Select(static e => e.Escolhido)];

        Result conferencia = MotivosAoCongelar.Recopiar(escolhidos);
        conferencia = conferencia.IsSuccess ? CoberturaDosMotivos.Conferir(processo, motivos) : conferencia;
        if (conferencia.IsSuccess)
        {
            MotivosAoCongelar.Congelar(motivos);
        }

        return conferencia;
    }
}
