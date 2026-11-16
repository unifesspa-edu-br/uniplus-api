namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O fechamento do ciclo recursal do processo: o das fases, que podem declarar a concluinte, e o das etapas, que fecham dentro delas.</summary>
public static class CicloRecursalDoProcesso
{
    public static IReadOnlyList<FieldError> Conferir(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapas);

        return [.. cronograma.ConferirConclusaoDoCiclo(), .. etapas.ConferirFechamentoDoCiclo()];
    }

    public static ItemConformidade Item(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas)
    {
        ArgumentNullException.ThrowIfNull(cronograma);

        IReadOnlyList<FaseDeclarada> indeterminadas = cronograma.ConclusoesIndeterminadas();

        return new ItemConformidade(
            "cronograma_conclusao_do_ciclo_recursal",
            DimensaoConformidade.Cronograma,
            "Cronograma: a fase ou a etapa que publica resultado preliminar tem o definitivo do mesmo tipo de ato, na própria fase ou etapa ou na fase concluinte declarada"
                + Eco(indeterminadas),
            indeterminadas.Count == 0 && Conferir(cronograma, etapas).Count == 0);
    }

    private static string Eco(IReadOnlyList<FaseDeclarada> fases) =>
        fases.Count == 0
            ? string.Empty
            : $" (fases com concluinte indeterminada: {string.Join("; ", fases.OrderBy(static fase => fase.Ordem).ThenBy(static fase => fase.Codigo, StringComparer.Ordinal).Select(static fase => fase.Codigo))})";
}
