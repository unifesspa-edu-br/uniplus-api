namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Fonte;

/// <summary>
/// Cria a <see cref="IGeoFonteDados"/> de uma versão (AAAAMM) sob demanda. Desacopla o
/// orquestrador (#674) de como a fonte é construída: em produção/dev resolve a
/// <see cref="DneStagingFonte"/> a partir do schema de staging; em teste, uma fonte em
/// memória.
/// </summary>
internal interface IGeoFonteDadosFactory
{
    /// <summary>Cria a fonte do dataset DNE da <paramref name="versao"/> informada.</summary>
    IGeoFonteDados Criar(string versao);
}
