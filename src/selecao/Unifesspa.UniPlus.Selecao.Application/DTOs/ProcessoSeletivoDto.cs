namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// Projeção de leitura do agregado <c>ProcessoSeletivo</c> (Story #758,
/// UNI-REQ-0014/0015). Cresce à medida que novas dimensões de configuração
/// (distribuição de vagas, bônus, desempate, classificação) ganham commands
/// nas fatias seguintes — cada dimensão entra aqui quando o command
/// correspondente é implementado.
/// </summary>
public sealed record ProcessoSeletivoDto(
    Guid Id,
    string Nome,
    string Tipo,
    string Status,
    IReadOnlyList<EtapaProcessoDto> Etapas,
    OfertaAtendimentoEspecializadoDto? OfertaAtendimento,
    DateTimeOffset CriadoEm)
{
    /// <summary>
    /// Hypermedia links (HATEOAS Level 1) — ver <see cref="EditalDto.Links"/>
    /// para a justificativa completa do padrão.
    /// </summary>
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
