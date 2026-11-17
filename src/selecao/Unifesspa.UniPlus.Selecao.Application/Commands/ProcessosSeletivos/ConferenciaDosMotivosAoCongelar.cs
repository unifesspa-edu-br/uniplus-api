namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.MotivosDeDecisao;

using Kernel.Results;

/// <summary>
/// Os motivos escolhidos, que a publicação, a retificação de uma vez e o fechamento da sessão conferem por último. Os
/// motivos que entram na versão que nasce recopiam a descrição vigente e recusam o que veio de motivo desativado
/// (UNI-REQ-0122); a cobertura que as decisões do processo exigem é conferida sobre a escolha atual
/// (<see cref="CoberturaDosMotivos"/>, UNI-REQ-0123).
/// </summary>
/// <remarks>
/// Congelar não grava nada nos motivos: cada escolha já nasce com a versão do edital em que entra. A recópia da
/// descrição é rastreada e se grava com a transação que aceita a versão; se algo depois dela recusar, o descarte do
/// rastreamento que envolve as três operações a desfaz.
/// </remarks>
public sealed class ConferenciaDosMotivosAoCongelar(IMotivoDoProcessoRepository repository)
{
    internal async Task<Result> ConferirAsync(ProcessoSeletivo processo, int numeroDaVersaoQueNasce, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processo);

        IReadOnlyList<MotivoEscolhidoComOrigem> escolhidos = await repository.ListarEscolhaAtualComOrigemAsync(processo.Id, cancellationToken).ConfigureAwait(false);
        Result recopia = MotivosAoCongelar.Recopiar(escolhidos, numeroDaVersaoQueNasce);

        return recopia.IsSuccess ? CoberturaDosMotivos.Conferir(processo, [.. escolhidos.Select(static e => e.Escolhido)]) : recopia;
    }
}
