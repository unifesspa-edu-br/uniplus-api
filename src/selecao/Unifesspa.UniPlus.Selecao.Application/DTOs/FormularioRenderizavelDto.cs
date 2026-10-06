namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O formulário de uma finalidade do processo pronto para renderização (UNI-REQ-0144, ADR-0139): o
/// renderizável comum ao certame, ao rascunho e ao modelo, mais a comprovação documental, que só o
/// processo tem.
/// </summary>
public sealed record FormularioRenderizavelDto : FormularioRenderizavel
{
    public FormularioRenderizavelDto(FormularioRenderizavel formulario, IReadOnlyList<ExigenciaDocumentalCertameDto>? comprovacaoDocumental)
        : base(formulario)
    {
        ComprovacaoDocumental = comprovacaoDocumental;
    }

    /// <summary>
    /// As exigências que o bloco de comprovação documental lista: as da fase do formulário, na forma
    /// que o certame publica. Nula quando o formulário não tem o bloco, e no rascunho, em que a lista
    /// sai da publicação.
    /// </summary>
    public IReadOnlyList<ExigenciaDocumentalCertameDto>? ComprovacaoDocumental { get; }
}
