namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Quem julga o recurso de cada fase que o admite e de cada etapa que o abre: a banca de análise de recursos da própria etapa,
/// ou a da fase em que a etapa acontece. O primeiro nível que tem banca decide, e o recurso que nenhum nível julga é recusado.
/// </summary>
internal static class QuemJulgaORecurso
{
    /// <summary>Em que nível se encontrou a banca que julga o recurso.</summary>
    internal enum Nivel
    {
        Ninguem,
        BancaDeRecursosDaEtapa,
        BancaDeRecursosDaFase,
    }

    internal static Nivel NivelDoRecursoDaFase(bool faseTemBanca) =>
        faseTemBanca ? Nivel.BancaDeRecursosDaFase : Nivel.Ninguem;

    internal static Nivel NivelDoRecursoDaEtapa(bool etapaTemBanca, bool faseTemBanca)
    {
        if (etapaTemBanca)
        {
            return Nivel.BancaDeRecursosDaEtapa;
        }

        return NivelDoRecursoDaFase(faseTemBanca);
    }

    /// <summary>
    /// Os recursos das fases que admitem recurso e quem os julga. A fase cuja lista de bancas teve parte recusada fica de fora:
    /// a banca que a leitura recusou pode ser a de recursos.
    /// </summary>
    internal static IReadOnlyList<RecursoJulgadoDaFase> DasFases(CronogramaDoProcesso cronograma)
    {
        ArgumentNullException.ThrowIfNull(cronograma);

        return
        [
            .. cronograma.Fases
                .Select(static (fase, posicao) => new RecursoJulgadoDaFase(fase, posicao, NivelDoRecursoDaFase(fase.Bancas.TemBancaDeRecursos)))
                .Where(static recurso => recurso.Fase.Atos.AdmiteRecurso && !recurso.Fase.ParteRecusadaEm(FaseDeclarada.ListaDasBancasRequeridas)),
        ];
    }

    /// <summary>
    /// Os recursos das etapas que abrem recurso e quem os julga. A etapa de julgadora indeterminada fica de fora: não se sabe
    /// qual banca a julga, e atribuí-la a uma só esconderia as outras.
    /// </summary>
    internal static IReadOnlyList<RecursoJulgadoDaEtapa> DasEtapas(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapas);

        return
        [
            .. etapas.Etapas
                .Select((etapa, posicao) => new RecursoJulgadoDaEtapa(
                    etapa, posicao, NivelDoRecursoDaEtapa(etapa.Bancas.TemBancaDeRecursos, FaseDaEtapaTemBancaDeRecursos(cronograma, etapa))))
                .Where(recurso => recurso.Etapa.Atos.TemRecursos && !JulgadoraIndeterminada(cronograma, recurso.Etapa)),
        ];
    }

    /// <summary>As recusas dos recursos que nenhuma banca julga: as das fases antes das das etapas.</summary>
    internal static IReadOnlyList<FieldError> SemBanca(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
    [
        .. DasFases(cronograma)
            .Where(static recurso => recurso.Nivel == Nivel.Ninguem)
            .Select(static recurso => CronogramaDoProcesso.NaFase(recurso.Posicao, "bancasRequeridas", FaseSemBancaDeRecursos(recurso.Fase.Codigo))),
        .. DasEtapas(cronograma, etapas)
            .Where(static recurso => recurso.Nivel == Nivel.Ninguem)
            .Select(static recurso => EtapasDoProcesso.NaEtapa(recurso.Posicao, "bancas", EtapaSemBancaDeRecursos(recurso.Etapa.Nome))),
    ];

    /// <summary>
    /// Se não se sabe qual banca julga o recurso da etapa: a lista das bancas ou a dos recursos da etapa teve parte recusada, ou a
    /// lista das bancas da fase em que ela acontece, ou a fase foi recusada inteira na leitura e leva consigo a banca de recursos
    /// que declarou.
    /// </summary>
    internal static bool JulgadoraIndeterminada(CronogramaDoProcesso cronograma, EtapaDeclarada etapa)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapa);

        return etapa.ParteRecusadaEm(EtapaDeclarada.ListaDasBancas)
            || etapa.ParteRecusadaEm(EtapaDeclarada.ListaDosRecursos)
            || FaseDaEtapaPodeTerSidoRecusada(cronograma, etapa)
            || cronograma.Fases.Any(fase => TemOCodigoDaFase(fase, etapa) && fase.ParteRecusadaEm(FaseDeclarada.ListaDasBancasRequeridas));
    }

    private static bool FaseDaEtapaPodeTerSidoRecusada(CronogramaDoProcesso cronograma, EtapaDeclarada etapa) =>
        cronograma.TemParteRecusadaNaListaDeFases
        && etapa.FaseCodigo is not null
        && !cronograma.Fases.Any(fase => TemOCodigoDaFase(fase, etapa));

    private static bool TemOCodigoDaFase(FaseDeclarada fase, EtapaDeclarada etapa) =>
        string.Equals(fase.Codigo, etapa.FaseCodigo, StringComparison.Ordinal);

    private static bool FaseDaEtapaTemBancaDeRecursos(CronogramaDoProcesso cronograma, EtapaDeclarada etapa) =>
        cronograma.Fases.Any(fase => TemOCodigoDaFase(fase, etapa) && fase.Bancas.TemBancaDeRecursos);

    private static DomainError FaseSemBancaDeRecursos(string codigoDaFase) =>
        new(BancaErrorCodes.RecursoSemBanca, $"A fase '{codigoDaFase}' admite recurso e não declara a banca de análise de recursos.");

    private static DomainError EtapaSemBancaDeRecursos(string nomeDaEtapa) =>
        new(
            BancaErrorCodes.RecursoSemBanca,
            $"A etapa '{Eco(nomeDaEtapa)}' abre recurso, e nem ela nem a fase em que acontece declaram a banca de análise de recursos.");

    private static string Eco(string nomeDaEtapa) =>
        CaracteresInvisiveis.ParaEco(nomeDaEtapa, TextoCongelado.TamanhoMaximoEcoado);

    /// <summary>O recurso de uma fase, a posição dela no cronograma e quem o julga.</summary>
    internal sealed record RecursoJulgadoDaFase(FaseDeclarada Fase, int Posicao, Nivel Nivel);

    /// <summary>O recurso de uma etapa, a posição dela na lista das etapas e quem o julga.</summary>
    internal sealed record RecursoJulgadoDaEtapa(EtapaDeclarada Etapa, int Posicao, Nivel Nivel);
}
