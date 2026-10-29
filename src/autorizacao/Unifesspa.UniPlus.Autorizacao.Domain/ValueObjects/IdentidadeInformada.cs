namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Pessoa como a requisição a informou, antes de conferida. Diferente de <see cref="Identidade"/>,
/// aceita campo ausente, para que o formato incorreto volte como recusa e não como exceção. É a pessoa que a instalação
/// designa gestor: o e-mail institucional é obrigatório, para que a concessão possa ser revogada, e o nome é opcional.
/// </summary>
public sealed record IdentidadeInformada(string? Emissor, string? Sub, string? Email = null, string? Nome = null);
