namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// Projeção de leitura do agregado <c>ProcessoSeletivo</c> (Story #758,
/// UNI-REQ-0014/0015). Cresce à medida que novas dimensões de configuração
/// ganham commands nas fatias seguintes.
/// </summary>
public sealed record ProcessoSeletivoDto(
    Guid Id,
    string Nome,
    string Tipo,
    string Status,
    IReadOnlyList<EtapaProcessoDto> Etapas,
    OfertaAtendimentoEspecializadoDto? OfertaAtendimento,
    IReadOnlyList<ConfiguracaoDistribuicaoVagasDto> DistribuicaoVagas,
    ConfiguracaoBonusRegionalDto? BonusRegional,
    IReadOnlyList<CriterioDesempateDto> CriteriosDesempate,
    ConfiguracaoClassificacaoDto? Classificacao,
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
