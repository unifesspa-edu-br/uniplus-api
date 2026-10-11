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
/// <param name="DependenciasDaAvaliacao">
/// No formulário de inscrição publicado, os campos e os grupos de que a avaliação da inscrição depende
/// (ADR-0143), só pelo código: quando um deles muda, o front consulta a avaliação. Nulo nas demais
/// finalidades e no rascunho.
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
    string? FusoDoEnvio,
    IReadOnlyList<ExigenciaDocumentalCertameDto>? ComprovacaoDocumental,
    DependenciasDaAvaliacaoDto? DependenciasDaAvaliacao)
    : FormularioRenderizavel(Finalidade, Titulo, Etapas, Termos, FatosColetados, Grupos, Regras, Pressupostos, DataReferenciaFatos, FusoDoEnvio)
{
    /// <summary>O formulário do processo a partir do renderizável comum e da comprovação documental.</summary>
    public static FormularioRenderizavelDto De(
        FormularioRenderizavel formulario,
        IReadOnlyList<ExigenciaDocumentalCertameDto>? comprovacaoDocumental,
        DependenciasDaAvaliacaoDto? dependenciasDaAvaliacao)
    {
        ArgumentNullException.ThrowIfNull(formulario);
        return new(
            formulario.Finalidade, formulario.Titulo, formulario.Etapas, formulario.Termos, formulario.FatosColetados, formulario.Grupos,
            formulario.Regras, formulario.Pressupostos, formulario.DataReferenciaFatos, formulario.FusoDoEnvio, comprovacaoDocumental,
            dependenciasDaAvaliacao);
    }
}

/// <summary>Os códigos dos campos — o subitem de grupo como <c>GRUPO.FATO</c> — e dos grupos de que a avaliação da inscrição depende.</summary>
public sealed record DependenciasDaAvaliacaoDto(IReadOnlyList<string> Fatos, IReadOnlyList<string> Grupos);
