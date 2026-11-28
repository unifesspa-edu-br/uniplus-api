namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// A exigência de documento vista pelas regras de formulário: a fase em que é exigida e o formulário
/// que a entrega, citado pelo código (ADR-0171). A fase é nula quando a exigência cita fase fora do cronograma:
/// a recusa dela é a do vínculo com a fase, e o formulário só é conferido contra a fase quando ela existe.
/// </summary>
public sealed record ExigenciaDeDocumentoDoFormulario(CodigoDoFormulario CodigoDoFormulario, Guid? FaseId)
{
    public static ExigenciaDeDocumentoDoFormulario De(DocumentoExigido documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        return new(documento.FormularioCodigo, documento.ExigidoNaFaseId);
    }
}
