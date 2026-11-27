namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A recusa da fase que coleta inscrição sem início e fim definidos, a mesma na publicação e na consulta de conformidade.</summary>
public static class RecusasDaJanelaDaInscricao
{
    public static DomainError FaseQueColetaInscricaoSemJanela(CitacaoDaFase fase) => new(
        "ProcessoSeletivo.FaseQueColetaInscricaoSemJanela",
        $"A fase {fase} coleta inscrição e precisa de início e fim definidos para que o Edital declare o período.");
}
