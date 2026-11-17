namespace Unifesspa.UniPlus.Infrastructure.Core.Errors.Regras;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>Códigos do grafo de dependências entre os campos do formulário.</summary>
internal static class ErrosDoGrafoDoFormulario
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Mapeamentos =
    [
        new(GrafoFormularioErrorCodes.FatoDuplicado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grafo_formulario.fato_duplicado", "O fato aparece mais de uma vez no formulário")),
        new(GrafoFormularioErrorCodes.OrdemDuplicada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grafo_formulario.ordem_duplicada", "A ordem de coleta do formulário precisa ser total")),
        new(GrafoFormularioErrorCodes.GrafoComCiclo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grafo_formulario.grafo_com_ciclo", "As regras dos campos formam um ciclo")),
        new(GrafoFormularioErrorCodes.CitaFatoNaoConhecido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grafo_formulario.cita_fato_nao_conhecido", "Uma regra do formulário cita fato que ele não conhece")),
        new(GrafoFormularioErrorCodes.CitaFatoPosterior, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grafo_formulario.cita_fato_posterior", "Uma regra do formulário cita fato conhecido só depois dela")),
        new(GrafoFormularioErrorCodes.CitaAtributoDoCandidato, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grafo_formulario.cita_atributo_do_candidato", "A regra do formulário cita fato calculado de atributos do candidato")),
        new(GrafoFormularioErrorCodes.CitaFatoDeMembroForaDoGrupo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.grafo_formulario.cita_fato_de_membro_fora_do_grupo", "A regra cita campo de grupo repetível fora do grupo")),
    ];
}
