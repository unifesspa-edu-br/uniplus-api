namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>Uma base legal que sustenta o documento exigido do modelo; copiada como está para a exigência do processo.</summary>
public sealed record BaseLegalDoDocumentoDoModelo(string Referencia, string Abrangencia, string Situacao, string? Observacao);
