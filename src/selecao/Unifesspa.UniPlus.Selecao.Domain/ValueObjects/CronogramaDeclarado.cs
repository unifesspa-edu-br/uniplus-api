namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O eixo temporal do processo: a localidade e a convenção que regem a contagem dos prazos, e as fases do cronograma. A localidade
/// ausente é a do documento que ainda não a declara, e a pendência dela recusa a publicação.
/// </summary>
public sealed class CronogramaDeclarado
{
    internal CronogramaDeclarado(LocalidadeRegente? localidade, ReferenciaRegra? algoritmoContagemPrazo, IReadOnlyList<FaseCronograma> cronogramaFases)
    {
        Localidade = localidade;
        AlgoritmoContagemPrazo = algoritmoContagemPrazo;
        CronogramaFases = cronogramaFases;
    }

    public LocalidadeRegente? Localidade { get; }

    public ReferenciaRegra? AlgoritmoContagemPrazo { get; }

    public IReadOnlyList<FaseCronograma> CronogramaFases { get; }
}
