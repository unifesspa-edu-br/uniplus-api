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
    TipoProcessoSnapshotDto TipoProcesso,
    string Status,
    string OrigemCandidatos,
    UnidadeAdministradoraSnapshotDto UnidadeAdministradora,
    // Localidade que rege a contagem dos prazos (UNI-REQ-0111): read-back administrativo,
    // sem o qual a tela de edição não consegue reler o que foi declarado na criação e
    // poderia reenviar valor divergente do salvo. Sempre presente — é NOT NULL desde a criação.
    LocalidadeRegenteDto Localidade,
    IReadOnlyList<EtapaProcessoDto> Etapas,
    OfertaAtendimentoEspecializadoDto? OfertaAtendimento,
    IReadOnlyList<ConfiguracaoDistribuicaoVagasDto> DistribuicaoVagas,
    ConfiguracaoBonusRegionalDto? BonusRegional,
    ConfiguracaoCascataRemanejamentoDto? Cascata,
    IReadOnlyList<CriterioDesempateDto> CriteriosDesempate,
    ConfiguracaoClassificacaoDto? Classificacao,
    IReadOnlyList<FaseCronogramaDto> CronogramaFases,
    IReadOnlyList<DocumentoExigidoDto> DocumentosExigidos,
    IReadOnlyList<NoExigenciaDto> RaizesExigencia,
    ReferenciaTemporalFatosDto? ReferenciaTemporalFatos,
    IReadOnlyList<FatoColetadoDto> FatosColetados,
    IReadOnlyList<ConfiguracaoDerivacaoDto> RegrasDerivacao,
    // Formulário de inscrição (Story #559): read-back administrativo do que foi salvo antes da
    // publicação — o GET público (FormularioInscricaoController) projeta só da VersaoConfiguracao
    // vigente e devolve 422 para processo em rascunho, então não serve para reler a tela de edição.
    string? FormularioTitulo,
    string? FormularioTermoAceiteTexto,
    // Divulgação pública (UNI-REQ-0050, issue #563): read-back administrativo pelo mesmo motivo
    // do formulário acima — sem ele, a tela de edição (uniplus-web#504) não conseguiria reler a
    // configuração salva e poderia sobrescrevê-la com o default ao reenviar. A regra de
    // abreviação não entra aqui: não é configuração viva, só existe congelada no envelope.
    ConfiguracaoDivulgacaoDto? ConfiguracaoDivulgacao,
    // Taxa de inscrição e isenção (issue #1112): read-back administrativo. null distingue
    // "ainda não declarado" (bloqueia publicação) de uma declaração explícita — diferente de
    // ConfiguracaoDivulgacao, aqui null NÃO é um estado publicável.
    ConfiguracaoTaxaInscricaoDto? ConfiguracaoTaxaInscricao,
    // Convenção de contagem dos prazos (UNI-REQ-0112): read-back administrativo, pelo mesmo
    // motivo das dimensões acima — sem ele, a tela não distingue "ainda não declarei" de
    // "declarei e não sei qual", e reenviaria às cegas. null é estado publicável quando
    // nenhuma contagem do certame distingue dia útil.
    ReferenciaRegraDto? AlgoritmoContagemPrazo,
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

/// <summary>
/// DTO de leitura de <see cref="Domain.ValueObjects.ReferenciaTemporalFatos"/>
/// (Story #554, issue #892, PR #896). Mesmo formato flat aceito por
/// <c>DefinirReferenciaTemporalFatosRequest</c> — round-trip GET→PUT direto.
/// <see langword="null"/> é o estado válido "nenhuma política configurada".
/// </summary>
public sealed record ReferenciaTemporalFatosDto(string Tipo, DateOnly? Data, Guid? FaseId);

/// <summary>
/// DTO de leitura de <see cref="Domain.ValueObjects.UnidadeAdministradoraSnapshot"/>
/// (issue #849, CA-04 da Feature #40). Sempre presente — a Unidade administradora
/// é NOT NULL desde a criação do processo.
/// </summary>
public sealed record UnidadeAdministradoraSnapshotDto(
    Guid OrigemId, string Sigla, string Slug, string Nome, string Tipo,
    string? CidadeCodigoIbge, string? CidadeNome, string? CidadeUf);

/// <summary>
/// DTO de leitura da localidade que rege a contagem dos prazos (UNI-REQ-0111). Sempre
/// presente — declarada desde a criação. <c>Nome</c> e <c>Uf</c> são cache de exibição:
/// quem calcula prazo deriva município e UF do <c>CodigoIbge</c>, nunca destes.
/// </summary>
public sealed record LocalidadeRegenteDto(string CodigoIbge, string Nome, string Uf);

/// <summary>Identidade e rótulo do tipo congelados na criação do processo (UNI-REQ-0098).</summary>
public sealed record TipoProcessoSnapshotDto(Guid OrigemId, string Codigo, string Nome);

/// <summary>
/// Projeção de leitura da divulgação pública (UNI-REQ-0050, issue #563). <see langword="null"/>
/// é o estado válido "sem configuração explícita" (default minimizado — só o número de
/// inscrição). Não carrega a regra de abreviação: ela não é configuração viva, e só existe
/// congelada no envelope de publicação.
/// </summary>
public sealed record ConfiguracaoDivulgacaoDto(IReadOnlyList<string> CamposPublicos, string? Justificativa);

/// <summary>
/// Projeção de leitura da taxa de inscrição e isenção (issue #1112). <see langword="null"/> é
/// "ainda não declarado" (bloqueia publicação, CA-01) — diferente de
/// <see cref="ConfiguracaoDivulgacaoDto"/>, aqui ausência não é estado publicável.
/// </summary>
public sealed record ConfiguracaoTaxaInscricaoDto(
    bool Cobra, decimal? Valor, IReadOnlyList<string> Fundamentos, bool ConfirmacaoFundamentos);
