namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O formulário de uma finalidade do processo pronto para renderização (UNI-REQ-0144, ADR-0139): o
/// renderizável comum ao certame, ao rascunho e ao modelo, mais a comprovação documental, que só o
/// processo tem. Posicional, como a base, para o contrato declarar todas as propriedades.
/// </summary>
/// <param name="ComprovacaoDocumental">
/// As exigências que o bloco de comprovação documental lista: as da fase do formulário, na forma que o
/// certame publica. Nula quando o formulário não tem o bloco; no rascunho, em que a lista sai da
/// publicação; e no formulário da inscrição do candidato, que recebe da avaliação da inscrição só os
/// documentos que se aplicam a ele.
/// </param>
/// <param name="DependenciasDaAvaliacao">
/// No formulário de inscrição publicado, os campos e os grupos de que a avaliação da inscrição depende
/// (ADR-0143), só pelo código: quando um deles muda, o front consulta a avaliação. Nulo nas demais
/// finalidades e no rascunho.
/// </param>
/// <param name="DependenciasDoFator">
/// No formulário de inscrição publicado, os campos e os grupos de que o Fator de Atenuação depende
/// (ADR-0142), só pelo código, no desenho de <paramref name="DependenciasDaAvaliacao"/>: o fato que a
/// configuração do bônus cita e os campos com os municípios do bônus. Vazio quando o processo não tem
/// Fator; nulo nas demais finalidades e no rascunho.
/// </param>
/// <param name="FaseId">
/// A fase do cronograma em que o formulário é respondido; nula quando o formulário ainda não tem fase.
/// É ela que diz quais documentos da avaliação da inscrição são desta fase, porque o processo pode ter
/// mais de uma fase que coleta inscrição.
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
    DependenciasDaAvaliacaoDto? DependenciasDaAvaliacao,
    DependenciasDaAvaliacaoDto? DependenciasDoFator,
    Guid? FaseId)
    : FormularioRenderizavel(Finalidade, Titulo, Etapas, Termos, FatosColetados, Grupos, Regras, Pressupostos, DataReferenciaFatos, FusoDoEnvio)
{
    /// <summary>O formulário do processo a partir do renderizável comum e da comprovação documental.</summary>
    public static FormularioRenderizavelDto De(
        FormularioRenderizavel formulario,
        IReadOnlyList<ExigenciaDocumentalCertameDto>? comprovacaoDocumental,
        DependenciasDaAvaliacaoDto? dependenciasDaAvaliacao,
        DependenciasDaAvaliacaoDto? dependenciasDoFator,
        Guid? faseId)
    {
        ArgumentNullException.ThrowIfNull(formulario);
        return new(
            formulario.Finalidade, formulario.Titulo, formulario.Etapas, formulario.Termos, formulario.FatosColetados, formulario.Grupos,
            formulario.Regras, formulario.Pressupostos, formulario.DataReferenciaFatos, formulario.FusoDoEnvio, comprovacaoDocumental,
            dependenciasDaAvaliacao, dependenciasDoFator, faseId);
    }
}

/// <summary>Os códigos dos campos — o subitem de grupo como <c>GRUPO.FATO</c> — e dos grupos de que a avaliação da inscrição depende.</summary>
public sealed record DependenciasDaAvaliacaoDto(IReadOnlyList<string> Fatos, IReadOnlyList<string> Grupos);
