namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

/// <summary>As fases e as etapas que um teste da relação entre elas declara, e como lê o resultado das conferências.</summary>
internal static class CenarioDeProcesso
{
    internal static CronogramaDoProcesso Fases(params ModeloDeFase[] fases) => new([.. fases.Select(static fase => fase.Declarar())]);

    internal static EtapasDoProcesso Etapas(params ModeloDeEtapa[] etapas) => new([.. etapas.Select(static etapa => etapa.Declarar())]);

    internal static (string? Campo, string Codigo)[] Causas(IEnumerable<FieldError> recusas) =>
        [.. recusas.Select(static recusa => (recusa.Field, recusa.Error.Code))];

    internal static ModeloDeEtapa EtapaNaFase(string codigoDaFase, string nome = "Prova") => new ModeloDeEtapa(nome) with { FaseCodigo = codigoDaFase };
}
