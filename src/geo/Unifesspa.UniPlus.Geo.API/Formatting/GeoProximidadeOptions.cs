namespace Unifesspa.UniPlus.Geo.API.Formatting;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Limites configuráveis da consulta de proximidade (ADR-0091): teto de raio (defesa
/// contra varredura nacional) e limites do top-N. Bindável da seção
/// <c>Geo:Proximidade</c>; os defaults cobrem o caso comum sem configuração.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Bindável via Configure<T> e injetada como IOptions<T> no ctor público do ProximidadeController — internal quebraria a acessibilidade do ctor (CS0051).")]
public sealed class GeoProximidadeOptions
{
    public const string SectionName = "Geo:Proximidade";

    /// <summary>Raio máximo aceito, em km (acima → 400). Evita varredura de cobertura nacional.</summary>
    public double RaioMaxKm { get; set; } = 500.0;

    /// <summary>Quantidade de resultados quando o cliente não informa <c>limit</c>.</summary>
    public int LimitPadrao { get; set; } = 50;

    /// <summary>Teto do top-N. <c>limit</c> acima deste valor é truncado (não é erro).</summary>
    public int LimitMax { get; set; } = 200;
}
