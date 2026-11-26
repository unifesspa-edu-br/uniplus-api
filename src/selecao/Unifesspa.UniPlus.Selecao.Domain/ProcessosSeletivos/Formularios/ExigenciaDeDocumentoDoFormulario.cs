namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// A exigência de documento vista pelas regras de formulário: a fase em que é exigida e o formulário
/// que a entrega, citado pelo código (ADR-0171).
/// </summary>
public sealed record ExigenciaDeDocumentoDoFormulario(CodigoDoFormulario CodigoDoFormulario, Guid FaseId)
{
    public static ExigenciaDeDocumentoDoFormulario De(DocumentoExigido documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        return new(documento.FormularioCodigo, documento.ExigidoNaFaseId);
    }
}
