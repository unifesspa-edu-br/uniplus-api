namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>Um instante em que algo foi decidido ou avaliado no lugar.</summary>
internal sealed record InicioNoLugar(LugarDaDecisao Lugar, DateTimeOffset Instante);
