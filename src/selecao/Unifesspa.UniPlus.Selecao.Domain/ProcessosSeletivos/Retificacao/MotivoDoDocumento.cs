namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// O motivo de decisão que o documento escolhe num lugar: a origem no cadastro e o lugar, que o identificam, o tipo e o
/// código. A justificativa é a da inclusão depois da publicação; é nula no motivo anterior a ela.
/// </summary>
public sealed record MotivoDoDocumento(Guid OrigemId, LugarDaDecisao Lugar, TipoMotivoDeDecisao Tipo, string Codigo, string? Justificativa);
