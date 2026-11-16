namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Entrega fases e etapas ao processo como a hidratação do banco faz, sem passar pela gravação: o estado que a gravação
/// recusa só existe assim, e é o que o portão de publicação e o checklist precisam conferir.
/// </summary>
internal static class ProcessoCarregadoDoBanco
{
    internal static void Fase(ProcessoSeletivo processo, FaseCronograma fase) => Lista<FaseCronograma>(processo, "_cronogramaFases").Add(fase);

    internal static void Etapa(ProcessoSeletivo processo, EtapaProcesso etapa) => Lista<EtapaProcesso>(processo, "_etapas").Add(etapa);

    /// <summary>O preliminar em que o prazo de recurso ancora some, e a âncora fica sem produto que a sustente.</summary>
    internal static void SemOsProdutos(FaseCronograma fase) => Lista<ProdutoDaFase>(fase, "_produtos").Clear();

    private static List<T> Lista<T>(object agregado, string campo) =>
        agregado.GetType().GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(agregado).Should().BeAssignableTo<List<T>>().Subject;
}
