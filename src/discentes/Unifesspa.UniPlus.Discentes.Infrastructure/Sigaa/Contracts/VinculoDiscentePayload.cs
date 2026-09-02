namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Contracts;

using System.Text.Json.Serialization;

/// <summary>
/// Uma linha de vínculo discente como o SIGAA a entrega. É modelo de transporte, não
/// de domínio: reflete o formato da origem, incluindo o que ela permite deixar em
/// branco, e não impõe nenhuma regra do Uni+.
/// </summary>
/// <remarks>
/// A tradução para o domínio — e a decisão sobre o que fazer com os campos ausentes —
/// pertence à camada de decodificação, não a este tipo. O contrato completo está em
/// <c>contracts/sigaa-vinculo-discentes.schema.json</c>.
/// </remarks>
public sealed record VinculoDiscentePayload
{
    /// <summary>
    /// Chave natural do vínculo na origem. É por ela que a réplica localiza o registro —
    /// a matrícula não serve, porque se repete entre pessoas distintas.
    /// </summary>
    [JsonPropertyName("idDiscente")]
    public long IdDiscente { get; init; }

    /// <summary>
    /// Número de matrícula. Vem como texto para preservar zeros à esquerda e não perder
    /// precisão em valores longos.
    /// </summary>
    [JsonPropertyName("matricula")]
    public string? Matricula { get; init; }

    /// <summary>
    /// CPF em onze dígitos, sem máscara. Dado pessoal: trafega apenas sob TLS, é cifrado
    /// em repouso pelo repositório e nunca aparece em log.
    /// </summary>
    [JsonPropertyName("cpf")]
    public string? Cpf { get; init; }

    [JsonPropertyName("nome")]
    public string? Nome { get; init; }

    /// <summary>
    /// Nível de ensino do vínculo. A sincronização pede graduação; o valor esperado é <c>G</c>.
    /// </summary>
    [JsonPropertyName("nivel")]
    public string? Nivel { get; init; }

    [JsonPropertyName("curso")]
    public CursoPayload? Curso { get; init; }

    [JsonPropertyName("situacao")]
    public SituacaoPayload? Situacao { get; init; }

    /// <summary>
    /// Ano de ingresso. A origem pode não tê-lo registrado.
    /// </summary>
    [JsonPropertyName("anoIngresso")]
    public int? AnoIngresso { get; init; }

    /// <summary>
    /// Período letivo de ingresso. A origem pode não tê-lo registrado.
    /// </summary>
    [JsonPropertyName("periodoIngresso")]
    public int? PeriodoIngresso { get; init; }

    /// <summary>
    /// Momento em que a origem gerou a linha da resposta — carimbo da resposta, não do
    /// dado. Não serve para detectar mudança no vínculo.
    /// </summary>
    [JsonPropertyName("dateRequest")]
    public string? DateRequest { get; init; }

    /// <summary>
    /// Sobrescreve o <c>ToString()</c> sintetizado do record, que enumeraria nome, CPF e
    /// matrícula em texto claro em qualquer interpolação ou log deste objeto.
    /// </summary>
    public override string ToString() => $"[VinculoDiscentePayload IdDiscente={IdDiscente}]";
}
