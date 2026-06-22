namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// Referência de cidade do Geo aninhada no contrato HTTP (ADR-0090/ADR-0096):
/// o trio canônico <c>codigoIbge</c>/<c>nome</c>/<c>uf</c>. No nível raiz da
/// entidade carrega também a proveniência/frescura do display cache
/// (<c>origem</c>/<c>displayAtualizadoEm</c>); aninhada dentro de
/// <see cref="EnderecoGeoDto"/> esses metadados são omitidos (vivem no endereço).
/// Mantida byte-equivalente à cópia do módulo Configuração (ADR-0035).
/// </summary>
public sealed record CidadeReferenciaDto(string CodigoIbge, string Nome, string Uf)
{
    /// <summary>Proveniência do display cache (ex.: <c>geo-api</c>). Só no nível raiz.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Origem { get; init; }

    /// <summary>Instante do carimbo server-side do display cache. Só no nível raiz.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? DisplayAtualizadoEm { get; init; }
}
