namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Quem produz o fato de que a avaliação começou num lugar do processo: cada módulo que registra decisões ou
/// avaliações registra a sua fonte. A fonte que falha propaga a falha, porque a leitura nunca vale como "não iniciado".
/// </summary>
internal interface IFonteDoInicioNoLugar
{
    /// <summary>
    /// O instante de cada registro da fonte no processo, com o lugar dele: todos, e não só o primeiro de cada lugar, e
    /// por isso pode haver vários por lugar. A fase que cada um inicia é a que continha o lugar no instante dele, e uma
    /// etapa que mudou de fase só inicia a segunda fase com um registro posterior à mudança.
    /// </summary>
    Task<IReadOnlyList<InicioNoLugar>> ListarAsync(Guid processoId, CancellationToken cancellationToken = default);
}
