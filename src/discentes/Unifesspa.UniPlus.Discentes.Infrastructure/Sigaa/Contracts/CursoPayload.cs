namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Contracts;

using System.Text.Json.Serialization;

/// <summary>
/// Curso do vínculo, como o SIGAA o entrega.
/// </summary>
public sealed record CursoPayload
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("nome")]
    public string? Nome { get; init; }

    /// <summary>
    /// Código do curso no registro do MEC, quando o curso tem um. É o identificador que
    /// permite cruzar o curso com dados externos à universidade.
    /// </summary>
    [JsonPropertyName("codigoEmec")]
    public string? CodigoEmec { get; init; }

    /// <summary>
    /// Unidade acadêmica responsável pelo curso. Vem ausente inteira para curso sem
    /// unidade registrada na origem — situação frequente, não excepcional.
    /// </summary>
    [JsonPropertyName("unidade")]
    public UnidadePayload? Unidade { get; init; }
}

/// <summary>
/// Unidade acadêmica responsável por um curso. Quando presente, traz identificador e
/// nome juntos; a origem nunca envia um sem o outro.
/// </summary>
public sealed record UnidadePayload
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("nome")]
    public string? Nome { get; init; }
}
