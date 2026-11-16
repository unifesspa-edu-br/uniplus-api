namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O motivo de decisão que o documento escolhe: a origem no cadastro, que o identifica, o tipo e o código. A
/// justificativa é a da inclusão depois da publicação; é nula no motivo anterior a ela.
/// </summary>
public sealed record MotivoDoDocumento(Guid OrigemId, TipoMotivoDeDecisao Tipo, string Codigo, string? Justificativa);
