namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Define o cronograma de fases de um processo já travado para edição: monta as fases (ver
/// <see cref="MontagemDasFases"/>), entrega ao domínio o grafo de precedências vigente e as fases já iniciadas como
/// parâmetros, e não como navegação (ADR-0042), e delega a validação final ao agregado. O instante vem de quem
/// chama, lido uma vez por operação (ADR-0068), e vale para o tipo de ato vigente e para as fases iniciadas.
/// </summary>
public sealed class DefinicaoDoCronogramaDeFases(
    MontagemDasFases montagem,
    IPrecedenciaFaseReader precedenciaFaseReader,
    IFasesIniciadasDoProcesso fasesIniciadasDoProcesso,
    ITipoAtoPublicadoReader tipoAtoPublicadoReader,
    IProcessoSeletivoRepository processoSeletivoRepository,
    IRegistroCodecsEnvelope registroCodecs)
{
    // O grafo de precedências é lido depois de as fases serem montadas, e só se elas vieram sem recusa.
    internal async Task<Result> DefinirAsync(
        ProcessoSeletivo processo, DefinirCronogramaFasesCommand command, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        FasesIniciadas iniciadas = await IniciadasAsync(processo, agora, cancellationToken).ConfigureAwait(false);
        AtosConsultadosNaEdicao atos = new(tipoAtoPublicadoReader, processoSeletivoRepository, registroCodecs, command.ProcessoSeletivoId, agora);
        Result<List<FaseCronograma>> fases = await montagem.MontarAsync(processo, command.Fases, atos, cancellationToken).ConfigureAwait(false);
        if (fases.IsFailure)
        {
            return Result.ValidationFailure(fases.Errors);
        }

        List<ArestaPrecedencia> precedencias = await PrecedenciasVivasAsync(cancellationToken).ConfigureAwait(false);
        return processo.DefinirCronogramaFases(fases.Value!, precedencias, command.Precondicao, iniciadas);
    }

    private async Task<FasesIniciadas> IniciadasAsync(ProcessoSeletivo processo, DateTimeOffset agora, CancellationToken cancellationToken) =>
        processo.Rascunho is null
            ? FasesIniciadas.Nenhuma
            : await fasesIniciadasDoProcesso.ObterAsync(processo.Id, agora, cancellationToken).ConfigureAwait(false);

    private async Task<List<ArestaPrecedencia>> PrecedenciasVivasAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<PrecedenciaFaseView> arestasVivas = await precedenciaFaseReader
            .ListarVivasAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. arestasVivas.Select(static a => new ArestaPrecedencia(a.AntecessoraCodigo, a.SucessoraCodigo, a.PermiteSobreposicao))];
    }
}
