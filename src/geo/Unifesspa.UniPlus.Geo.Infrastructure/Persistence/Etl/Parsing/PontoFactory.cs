namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Parsing;

using NetTopologySuite.Geometries;

/// <summary>
/// Materializa a coordenada textual da DNE (latitude/longitude) num
/// <see cref="Point"/> SRID 4326 — <c>geography(Point,4326)</c> (ADR-0091).
/// Parse tolerante: se qualquer componente faltar ou não parsear, retorna
/// <see langword="null"/> (a coordenada é opcional). Ordem PostGIS/NTS:
/// <strong>X = longitude, Y = latitude</strong>.
/// </summary>
internal static class PontoFactory
{
    private const int Srid4326 = 4326;

    public static Point? Criar(string? latitude, string? longitude)
    {
        decimal? lat = ParseTolerante.ParaDecimal(latitude);
        decimal? lon = ParseTolerante.ParaDecimal(longitude);

        if (lat is null || lon is null)
        {
            return null;
        }

        // Coordenada fora do domínio geográfico (lat ∈ [-90,90], lon ∈ [-180,180])
        // degrada para null: um dado opcional sujo não pode estourar no
        // geography(Point,4326) e derrubar a carga inteira (parse tolerante, ADR-0092).
        if (lat.Value is < -90m or > 90m || lon.Value is < -180m or > 180m)
        {
            return null;
        }

        return new Point((double)lon.Value, (double)lat.Value) { SRID = Srid4326 };
    }
}
