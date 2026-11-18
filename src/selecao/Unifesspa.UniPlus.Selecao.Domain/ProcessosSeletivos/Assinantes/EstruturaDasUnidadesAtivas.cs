namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Assinantes;

/// <summary>
/// As unidades ativas e a superior de cada uma, no instante da conferência. Unidade fora do conjunto
/// está inativa ou não existe; para quem confere a autoridade, as duas coisas negam do mesmo jeito.
/// </summary>
internal sealed record EstruturaDasUnidadesAtivas
{
    private readonly Dictionary<Guid, Guid?> superiorPorUnidade;

    internal EstruturaDasUnidadesAtivas(IEnumerable<UnidadeAtiva> unidades)
    {
        ArgumentNullException.ThrowIfNull(unidades);
        superiorPorUnidade = unidades.ToDictionary(static u => u.Id, static u => u.SuperiorId);
    }

    internal bool TentaObterSuperior(Guid unidadeId, out Guid? superiorId) =>
        superiorPorUnidade.TryGetValue(unidadeId, out superiorId);
}
