namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O formulário do processo pronto para apresentação (UNI-REQ-0144, ADR-0139): o
/// apresentado comum ao certame, ao rascunho e ao modelo, mais o código, a fase, a obrigatoriedade e a
/// comprovação documental, que só o processo tem. Posicional, como a base, para o contrato declarar todas as propriedades.
/// </summary>
/// <param name="Codigo">O código que identifica o formulário no processo.</param>
/// <param name="ComprovacaoDocumental">
/// As exigências que o bloco de comprovação documental lista: as da fase do formulário, na forma que o
/// certame publica. Nula quando o formulário não tem o bloco; no rascunho, em que a lista sai da
/// publicação; e no formulário da inscrição do candidato, que recebe da avaliação da inscrição só os
/// documentos que se aplicam a ele.
/// </param>
/// <param name="DependenciasDaAvaliacao">
/// No formulário que a inscrição responde, publicado, os campos e os grupos de que a avaliação da inscrição depende
/// (ADR-0143), só pelo código: quando um deles muda, o front consulta a avaliação. Nulo nos demais
/// formulários e no rascunho.
/// </param>
/// <param name="DependenciasDoFator">
/// No formulário que a inscrição responde, publicado, os campos e os grupos de que o Fator de Atenuação depende
/// (ADR-0142), só pelo código, no desenho de <paramref name="DependenciasDaAvaliacao"/>: o dado do candidato
/// que a configuração do FADA cita e os campos com os municípios do FADA. Vazio quando o processo não tem
/// Fator; nulo nos demais formulários e no rascunho.
/// </param>
/// <param name="FaseId">
/// A fase do cronograma em que o formulário é respondido; nula quando o formulário ainda não tem fase.
/// É ela que diz quais documentos da avaliação da inscrição são desta fase, porque o processo pode ter
/// mais de uma fase que coleta inscrição.
/// </param>
/// <param name="Obrigatorio">Se o formulário é obrigatório para o candidato.</param>
public sealed record FormularioApresentadoDoProcessoDto(
    FormularioApresentado Formulario,
    string Codigo,
    Guid? FaseId,
    bool Obrigatorio,
    IReadOnlyList<ExigenciaDocumentalCertameDto>? ComprovacaoDocumental,
    DependenciasDaAvaliacaoDto? DependenciasDaAvaliacao,
    DependenciasDaAvaliacaoDto? DependenciasDoFator);

/// <summary>Os códigos dos campos — o subitem de grupo como <c>GRUPO.FATO</c> — e dos grupos de que a avaliação da inscrição depende.</summary>
public sealed record DependenciasDaAvaliacaoDto(IReadOnlyList<string> Fatos, IReadOnlyList<string> Grupos);
