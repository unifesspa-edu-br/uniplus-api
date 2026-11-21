namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.MotivosDeDecisao;
using Domain.ValueObjects;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Define o cronograma de fases de um processo já travado para edição: monta as fases (ver
/// <see cref="MontagemDasFases"/>), entrega ao domínio o grafo de precedências vigente como parâmetro, e não como
/// navegação (ADR-0042), e delega a validação final ao agregado. O instante vem de quem chama, lido uma vez por
/// operação (ADR-0068), e vale para o tipo de ato vigente. A fase que sai leva junto os motivos dela e das etapas dela
/// (<see cref="MotivosDosLugaresQueSaem"/>).
/// </summary>
public sealed class DefinicaoDoCronogramaDeFases(
    MontagemDasFases montagem,
    LeituraDasPrecedenciasDosCadastros precedencias,
    ITipoAtoPublicadoReader tipoAtoPublicadoReader,
    IProcessoSeletivoRepository processoSeletivoRepository,
    IRegistroCodecsEnvelope registroCodecs,
    MotivosDosLugaresQueSaem motivosDosLugaresQueSaem)
{
    internal async Task<Result> DefinirAsync(
        ProcessoSeletivo processo, DefinirCronogramaFasesCommand command, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        LugaresDaDecisao lugaresAntes = LugaresDaDecisao.Das(processo.CronogramaFases, processo.Etapas);
        Result definido = await MontarEDefinirAsync(processo, command, agora, cancellationToken).ConfigureAwait(false);
        if (definido.IsSuccess)
        {
            await motivosDosLugaresQueSaem.RetirarAsync(processo, lugaresAntes, cancellationToken).ConfigureAwait(false);
        }

        return definido;
    }

    // O grafo de precedências é lido depois de as fases serem montadas, e só se elas vieram sem recusa.
    private async Task<Result> MontarEDefinirAsync(
        ProcessoSeletivo processo, DefinirCronogramaFasesCommand command, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        AtosConsultadosNaEdicao atos = new(tipoAtoPublicadoReader, processoSeletivoRepository, registroCodecs, command.ProcessoSeletivoId, agora);
        Result<List<FaseCronograma>> fases = await montagem.MontarAsync(processo, command.Fases, atos, cancellationToken).ConfigureAwait(false);
        if (fases.IsFailure)
        {
            return Result.ValidationFailure(fases.Errors);
        }

        IReadOnlyList<ArestaPrecedencia> vivas = await precedencias.ListarVivasAsync(cancellationToken).ConfigureAwait(false);

        return processo.DefinirCronogramaFases(fases.Value!, vivas);
    }
}
