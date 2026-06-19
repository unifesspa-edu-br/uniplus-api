namespace Unifesspa.UniPlus.Geo.API.Formatting;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Validação no boundary HTTP (ADR-0031) dos parâmetros da consulta de proximidade:
/// <c>lat</c>/<c>long</c>/<c>raioKm</c> obrigatórios e em faixa, <c>limit</c> com
/// default e teto. O handler recebe a consulta já validada (nunca a entrada crua);
/// qualquer parâmetro ausente, não finito ou fora de faixa vira 400 (<c>ProblemDetails</c>).
/// </summary>
internal sealed class ConsultaProximidade
{
    private ConsultaProximidade(double latitude, double longitude, double raioKm, int limit)
    {
        Latitude = latitude;
        Longitude = longitude;
        RaioKm = raioKm;
        Limit = limit;
    }

    public double Latitude { get; }

    public double Longitude { get; }

    public double RaioKm { get; }

    public int Limit { get; }

    /// <summary>
    /// Valida os parâmetros contra <paramref name="opcoes"/> (teto de raio, default/teto
    /// de limit) e devolve <see langword="true"/> + <paramref name="consulta"/> quando
    /// válidos; caso contrário <see langword="false"/> + <paramref name="erro"/> (400).
    /// </summary>
    public static bool TentarCriar(
        double? lat,
        double? @long,
        double? raioKm,
        int? limit,
        GeoProximidadeOptions opcoes,
        [NotNullWhen(true)] out ConsultaProximidade? consulta,
        [NotNullWhen(false)] out ProblemDetails? erro)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        consulta = null;
        erro = null;

        if (lat is null || @long is null || raioKm is null)
        {
            erro = Problema(
                "Parâmetros obrigatórios ausentes",
                "Informe os parâmetros 'lat', 'long' e 'raioKm'.");
            return false;
        }

        double latitude = lat.Value;
        double longitude = @long.Value;
        double raio = raioKm.Value;

        if (!double.IsFinite(latitude) || latitude is < -90 or > 90)
        {
            erro = Problema("Latitude inválida", "A latitude deve estar entre -90 e 90 graus.");
            return false;
        }

        if (!double.IsFinite(longitude) || longitude is < -180 or > 180)
        {
            erro = Problema("Longitude inválida", "A longitude deve estar entre -180 e 180 graus.");
            return false;
        }

        if (!double.IsFinite(raio) || raio <= 0 || raio > opcoes.RaioMaxKm)
        {
            erro = Problema(
                "Raio inválido",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"O raio (raioKm) deve ser maior que 0 e no máximo {opcoes.RaioMaxKm} km."));
            return false;
        }

        // limit negativo/zero é entrada inválida → 400 (não silenciar com clamp);
        // ausente → default; acima do teto → trunca para o teto (top-N defensivo).
        if (limit is <= 0)
        {
            erro = Problema("Limite inválido", "O parâmetro 'limit' deve ser maior que 0.");
            return false;
        }

        int limitEfetivo = Math.Min(limit ?? opcoes.LimitPadrao, opcoes.LimitMax);

        consulta = new ConsultaProximidade(latitude, longitude, raio, limitEfetivo);
        return true;
    }

    private static ProblemDetails Problema(string titulo, string detalhe) => new()
    {
        Title = titulo,
        Detail = detalhe,
        Status = StatusCodes.Status400BadRequest,
    };
}
