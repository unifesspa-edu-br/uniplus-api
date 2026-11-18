namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// A repetição por entidade de uma exigência de documento vista pela conferência: o formulário que entrega
/// a exigência, a fase dela e o código do grupo repetível que ela nomeia (UNI-REQ-0069, UNI-REQ-0146).
/// </summary>
public sealed record RepeticaoDaExigencia(CodigoDoFormulario CodigoDoFormulario, Guid FaseId, string CodigoDoGrupo);
