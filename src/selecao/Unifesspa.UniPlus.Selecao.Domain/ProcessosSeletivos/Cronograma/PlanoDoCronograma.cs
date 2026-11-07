namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>
/// O que trocar o cronograma faz: as fases que continuam recebem o conteúdo das novas, e a mudança nas
/// coleções leva as fases resultantes e a poda do que existia só por causa das que saem. Tudo foi
/// calculado antes de qualquer efeito.
/// </summary>
internal sealed record PlanoDoCronograma(
    IReadOnlyList<(FaseCronograma Existente, FaseCronograma Nova)> Retidas,
    MudancaDaConfiguracao Mudanca) : IPlanoDaEdicao
{
    public void AtualizarPartes()
    {
        foreach ((FaseCronograma existente, FaseCronograma nova) in Retidas)
        {
            existente.AtualizarDe(nova, [.. nova.Produtos]);
        }
    }
}
