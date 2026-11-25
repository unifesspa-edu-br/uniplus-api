namespace Unifesspa.UniPlus.Infrastructure.Core.Errors.Regras;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Regras.Errors;

/// <summary>Códigos dos termos que o formulário exige do candidato.</summary>
internal static class ErrosDosTermosDoFormulario
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Mapeamentos =
    [
        new(TermoFormularioErrorCodes.CodigoObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.termo_formulario.codigo_obrigatorio", "O código do termo exigido é obrigatório")),
        new(TermoFormularioErrorCodes.CodigoTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.termo_formulario.codigo_tamanho", "O código do termo exigido excede o tamanho máximo")),
        new(TermoFormularioErrorCodes.CodigoFormatoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.termo_formulario.codigo_formato_invalido", "O código do termo exigido está fora do formato")),
        new(TermoFormularioErrorCodes.OrdemInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.termo_formulario.ordem_invalida", "A ordem do termo não pode ser negativa")),
        new(TermoFormularioErrorCodes.CodigoDuplicado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.termo_formulario.codigo_duplicado", "O código aparece em mais de um termo do formulário")),
        new(TermoFormularioErrorCodes.OrdemDuplicada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.termo_formulario.ordem_duplicada", "Dois termos do formulário têm a mesma ordem")),
    ];
}
