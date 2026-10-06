namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O formulário de uma finalidade do processo pronto para renderização (UNI-REQ-0144, ADR-0139): o
/// renderizável comum ao certame, ao rascunho e ao modelo, mais a comprovação documental, que só o
/// processo tem. Posicional, como a base, para o contrato declarar todas as propriedades.
/// </summary>
/// <param name="ComprovacaoDocumental">
/// As exigências que o bloco de comprovação documental lista: as da fase do formulário, na forma que o
/// certame publica. Nula quando o formulário não tem o bloco, e no rascunho, em que a lista sai da
/// publicação.
/// </param>
public sealed record FormularioRenderizavelDto(
    string Finalidade,
    string? Titulo,
    IReadOnlyList<SecaoRenderizavel> Etapas,
    IReadOnlyList<TermoRenderizavel> Termos,
    IReadOnlyList<CampoRenderizavel> FatosColetados,
    IReadOnlyList<GrupoRenderizavel> Grupos,
    FormularioPortavel Regras,
    IReadOnlyList<PressupostoRenderizavel> Pressupostos,
    DateOnly? DataReferenciaFatos,
    IReadOnlyList<ExigenciaDocumentalCertameDto>? ComprovacaoDocumental)
    : FormularioRenderizavel(Finalidade, Titulo, Etapas, Termos, FatosColetados, Grupos, Regras, Pressupostos, DataReferenciaFatos)
{
    /// <summary>O formulário do processo a partir do renderizável comum e da comprovação documental.</summary>
    public static FormularioRenderizavelDto De(FormularioRenderizavel formulario, IReadOnlyList<ExigenciaDocumentalCertameDto>? comprovacaoDocumental)
    {
        ArgumentNullException.ThrowIfNull(formulario);
        return new(
            formulario.Finalidade, formulario.Titulo, formulario.Etapas, formulario.Termos, formulario.FatosColetados, formulario.Grupos,
            formulario.Regras, formulario.Pressupostos, formulario.DataReferenciaFatos, comprovacaoDocumental);
    }
}
