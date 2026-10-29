namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Pessoa como a requisição a informou, antes de conferida. Diferente de <see cref="Identidade"/>,
/// aceita campo ausente, para que o formato incorreto volte como recusa e não como exceção.
/// </summary>
public sealed record IdentidadeInformada(string? Emissor, string? Sub);
