namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>Uma base legal que sustenta uma exigência do modelo; copiada como está para a exigência do processo.</summary>
public sealed record BaseLegalDoModelo(string Referencia, string Abrangencia, string Situacao, string? Observacao);
