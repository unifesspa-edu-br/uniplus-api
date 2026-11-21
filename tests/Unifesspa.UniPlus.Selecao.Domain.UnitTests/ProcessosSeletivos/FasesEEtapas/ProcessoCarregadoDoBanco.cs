namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Entrega fases e etapas ao processo como a hidratação do banco faz, sem passar pela gravação: o estado que a gravação
/// recusa só existe assim, e é o que a conferência de publicação e o checklist precisam conferir.
/// </summary>
internal static class ProcessoCarregadoDoBanco
{
    internal static void Fase(ProcessoSeletivo processo, FaseCronograma fase) => Lista<FaseCronograma>(processo, "_cronogramaFases").Add(fase);

    internal static void Etapa(ProcessoSeletivo processo, EtapaProcesso etapa) => Lista<EtapaProcesso>(processo, "_etapas").Add(etapa);

    /// <summary>A janela da etapa que o banco hidrata com o fim antes do início, que a gravação recusa.</summary>
    internal static void InverterJanela(EtapaProcesso etapa)
    {
        DateTimeOffset inicio = new(2027, 3, 10, 0, 0, 0, TimeSpan.Zero);
        typeof(EtapaProcesso).GetProperty(nameof(EtapaProcesso.Inicio))!.SetValue(etapa, inicio);
        typeof(EtapaProcesso).GetProperty(nameof(EtapaProcesso.Fim))!.SetValue(etapa, inicio.AddDays(-5));
    }

    /// <summary>A hidratação de um processo cujo banco não tem nenhuma etapa.</summary>
    internal static void EsvaziarEtapas(ProcessoSeletivo processo) => Lista<EtapaProcesso>(processo, "_etapas").Clear();

    /// <summary>O preliminar em que o prazo de recurso ancora some, e a âncora fica sem produto que a sustente.</summary>
    internal static void SemOsProdutos(FaseCronograma fase) => Lista<ProdutoDaFase>(fase, "_produtos").Clear();

    /// <summary>A fase que o banco hidrata declarando um tipo de decisão, sem que a gravação o tenha conferido.</summary>
    internal static void ComTipoDeDecisao(FaseCronograma fase, TipoMotivoDeDecisao tipo) => Lista<TipoMotivoDeDecisao>(fase, "_tiposDeDecisao").Add(tipo);

    /// <summary>A fase que o banco hidrata sem declarar nenhum tipo de decisão.</summary>
    internal static void SemTiposDeDecisao(FaseCronograma fase) => Lista<TipoMotivoDeDecisao>(fase, "_tiposDeDecisao").Clear();

    /// <summary>A fase que o banco hidrata sem nenhuma banca requerida.</summary>
    internal static void SemBancasRequeridas(FaseCronograma fase) => Lista<BancaRequerida>(fase, "_bancasRequeridas").Clear();

    private static List<T> Lista<T>(object agregado, string campo) =>
        agregado.GetType().GetField(campo, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(agregado).Should().BeAssignableTo<List<T>>().Subject;
}
