namespace Unifesspa.UniPlus.Geo.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// Endereço estruturado resolvido a partir de um CEP (<c>GET /api/cep/{cep}</c>):
/// logradouro → bairro/distrito → cidade → UF, com coordenada. Reference data
/// público (read-only); suporta HATEOAS Level 1 via <c>_links</c> (ADR-0029). A
/// <c>Coordenada</c> PostGIS não vaza no contrato — só <c>Latitude</c>/<c>Longitude</c>.
/// </summary>
/// <remarks>
/// <para><see cref="NivelResolucao"/> (<c>logradouro|bairro|distrito|cidade</c>)
/// indica até onde a resolução chegou; <see cref="Origem"/>
/// (<c>logradouro|faixa-cidade|faixa-bairro|faixa-distrito|grande-usuario</c>)
/// distingue a estratégia. Ver <see cref="CepResolucao"/>.</para>
/// <para>Na resolução por <strong>grande usuário</strong>, o nome do órgão/empresa
/// é exposto em <see cref="Logradouro"/> (o DTO não tem campo dedicado) e
/// <see cref="Cidade"/>/<see cref="CodigoIbge"/>/<see cref="Uf"/> vêm da faixa CEP,
/// não do próprio grande usuário (que só guarda <c>cep</c>+<c>nome</c>).</para>
/// </remarks>
public sealed record CepResolvidoDto(
    string Cep,
    string? Tipo,
    string? Logradouro,
    string? Complemento,
    string? Bairro,
    string? Distrito,
    string Cidade,
    string CodigoIbge,
    string Uf,
    decimal? Latitude,
    decimal? Longitude,
    string NivelResolucao,
    string Origem)
{
    /// <summary>
    /// Candidatos alternativos quando o CEP casa <strong>vários</strong> logradouros
    /// (CEP de logradouro/geral compartilhado). Vazia quando há um único logradouro
    /// ou quando a resolução é por faixa/grande usuário. O primário fica nos campos
    /// de topo; os demais aqui, na mesma ordem de desempate estável.
    /// </summary>
    public IReadOnlyList<CandidatoLogradouroDto> Alternativos { get; init; } = [];

    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
