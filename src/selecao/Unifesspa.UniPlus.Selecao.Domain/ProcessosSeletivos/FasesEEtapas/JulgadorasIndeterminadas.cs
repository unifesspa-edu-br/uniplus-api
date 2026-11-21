namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Quando não se sabe quem julga o recurso: a leitura do documento recusou parte do que decide isso, e a regra que o decidisse com
/// o que sobrou aprovaria ou recusaria por omissão. O item fica pendente, sem causa. Só entra o que o item lê: a fase não depende
/// das etapas quando o item aponta área do ENEM ou não tem tipo de decisão, e o alvo em item de avaliação lê só a etapa que o
/// contém.
/// </summary>
internal static class JulgadorasIndeterminadas
{
    /// <summary>O recurso da fase: o que a própria fase deixou recusado e, se o item tem tipo de decisão, as bancas das etapas que o avaliaram.</summary>
    internal static bool DoRecursoDaFase(FaseDeclarada fase, EtapasDoProcesso etapas, ProcedimentoDeRecursoDeclarado? procedimento) =>
        FaseComParteRecusada(fase) || (procedimento is not null && EtapasQueAvaliamComParteRecusada(fase, etapas, procedimento.Alvo));

    /// <summary>
    /// O recurso da etapa: a lista das bancas, a dos recursos ou os tipos de decisão da etapa, a lista das bancas da fase em que ela
    /// acontece, ou a fase recusada inteira na leitura, que leva consigo a banca de recursos que declarou.
    /// </summary>
    internal static bool DoRecursoDaEtapa(CronogramaDoProcesso cronograma, EtapaDeclarada etapa)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapa);

        return etapa.ParteRecusadaEm(EtapaDeclarada.ListaDasBancas)
            || etapa.ParteRecusadaEm(EtapaDeclarada.ListaDosRecursos)
            || etapa.TiposDeDecisaoRecusados
            || FaseDaEtapaPodeTerSidoRecusada(cronograma, etapa)
            || cronograma.Fases.Any(fase => TemOCodigoDaFase(fase, etapa) && fase.ParteRecusadaEm(FaseDeclarada.ListaDasBancasRequeridas));
    }

    private static bool FaseComParteRecusada(FaseDeclarada fase) =>
        fase.ParteRecusadaEm(FaseDeclarada.ListaDasBancasRequeridas)
        || fase.ParteRecusadaEm(FaseDeclarada.CampoDaRegraDeRecurso)
        || fase.ParteRecusadaEm(FaseDeclarada.ListaDosProcedimentosDeRecurso)
        || fase.TiposDeDecisaoRecusados;

    private static bool EtapasQueAvaliamComParteRecusada(FaseDeclarada fase, EtapasDoProcesso etapas, AlvoDoProcedimentoDeRecurso? alvo) => alvo switch
    {
        null => etapas.LeituraRecusouEtapa || etapas.Etapas.Any(etapa => DaFase(etapa, fase) && BancasRecusadas(etapa)),
        { Tipo: TipoDeAlvoDoProcedimentoDeRecurso.ItemDeAvaliacao } => EtapaDoAlvoComBancasRecusadas(etapas, alvo),
        _ => false,
    };

    // A etapa que contém o alvo é a única procurada: sem ela entre as declaradas, só a etapa recusada inteira na leitura pode ser a dele.
    private static bool EtapaDoAlvoComBancasRecusadas(EtapasDoProcesso etapas, AlvoDoProcedimentoDeRecurso alvo) =>
        etapas.Etapas.FirstOrDefault(etapa => etapa.ContemOAlvo(alvo)) is { } doAlvo
            ? BancasRecusadas(doAlvo)
            : etapas.LeituraRecusouEtapa;

    private static bool BancasRecusadas(EtapaDeclarada etapa) => etapa.ParteRecusadaEm(EtapaDeclarada.ListaDasBancas);

    private static bool DaFase(EtapaDeclarada etapa, FaseDeclarada fase) =>
        string.Equals(etapa.FaseCodigo, fase.Codigo, StringComparison.Ordinal);

    private static bool FaseDaEtapaPodeTerSidoRecusada(CronogramaDoProcesso cronograma, EtapaDeclarada etapa) =>
        cronograma.TemParteRecusadaNaListaDeFases
        && etapa.FaseCodigo is not null
        && !cronograma.Fases.Any(fase => TemOCodigoDaFase(fase, etapa));

    private static bool TemOCodigoDaFase(FaseDeclarada fase, EtapaDeclarada etapa) =>
        string.Equals(fase.Codigo, etapa.FaseCodigo, StringComparison.Ordinal);
}
