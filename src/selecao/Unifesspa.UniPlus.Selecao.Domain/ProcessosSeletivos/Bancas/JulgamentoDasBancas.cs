namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>Quem julga o quê: o vínculo entre as bancas declaradas, as fases e as etapas do processo.</summary>
public static class JulgamentoDasBancas
{
    public static bool EhDeRecursos(string codigoDoTipo) =>
        string.Equals(codigoDoTipo, TipoBancaCodigo.AnaliseDeRecursos, StringComparison.Ordinal);

    internal static bool TemBancaDeRecursos(FaseCronograma fase) =>
        fase.BancasRequeridas.Any(static banca => EhDeRecursos(banca.Codigo));

    internal static bool TemBancaDeRecursos(EtapaProcesso etapa) =>
        etapa.Bancas.Any(static banca => EhDeRecursos(banca.Codigo));

    internal static IEnumerable<EtapaProcesso> EtapasDaFase(EstadoDaConfiguracao estado, FaseCronograma fase) =>
        estado.Etapas.Where(etapa => string.Equals(etapa.FaseCodigo, fase.Codigo, StringComparison.Ordinal));

    /// <summary>
    /// As etapas da fase que têm recurso e não têm banca de recursos própria: o recurso delas é
    /// julgado pela banca de recursos da fase.
    /// </summary>
    internal static IEnumerable<EtapaProcesso> EtapasRecorridasSemBancaPropria(EstadoDaConfiguracao estado, FaseCronograma fase) =>
        EtapasDaFase(estado, fase).Where(static etapa => etapa.Recursos.Count > 0 && !TemBancaDeRecursos(etapa));

    /// <summary>A etapa que a banca da fase julga, além da própria fase: só a banca de recursos julga etapas.</summary>
    internal static IEnumerable<EtapaProcesso> EtapasJulgadasPelaBancaDaFase(
        EstadoDaConfiguracao estado, FaseCronograma fase, BancaRequerida banca) =>
        EhDeRecursos(banca.Codigo) ? EtapasRecorridasSemBancaPropria(estado, fase) : [];

    /// <summary>A etapa que a banca da própria etapa julga: só a banca de recursos julga a etapa em que está declarada.</summary>
    internal static IEnumerable<EtapaProcesso> EtapasJulgadasPelaBancaDaEtapa(EtapaProcesso etapa, BancaDaEtapa banca) =>
        EhDeRecursos(banca.Codigo) ? [etapa] : [];
}
