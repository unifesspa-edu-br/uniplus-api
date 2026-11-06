namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

internal static class LocalOfertaErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(LocalOfertaErrorCodes.TipoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.local_oferta.tipo_invalido",
                "Tipo de local de oferta inválido")),

        new(LocalOfertaErrorCodes.CampusResponsavelNaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.local_oferta.campus_responsavel_nao_encontrado",
                "Campus responsável informado não encontrado")),

        new(LocalOfertaErrorCodes.CodigoEmecTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.local_oferta.codigo_emec_tamanho",
                "Tamanho do código e-MEC do local de oferta inválido")),

        new(LocalOfertaErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.local_oferta.nao_encontrado",
                "Local de oferta não encontrado")),

        new(LocalOfertaErrorCodes.RemocaoBloqueadaPorOfertaCurso,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.local_oferta.remocao_bloqueada_por_oferta_curso",
                "Não é possível remover um local de oferta referenciado por oferta de curso ativa")),
    ];
}
