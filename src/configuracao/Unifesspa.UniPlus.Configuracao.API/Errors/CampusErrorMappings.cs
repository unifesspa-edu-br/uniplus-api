namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

internal static class CampusErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(CampusErrorCodes.SiglaObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.sigla_obrigatoria",
                "Sigla do campus é obrigatória")),

        new(CampusErrorCodes.SiglaTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.sigla_tamanho",
                "Tamanho da sigla do campus inválido")),

        new(CampusErrorCodes.SiglaAcentuacaoInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.sigla_acentuacao_invalida",
                "Sigla do campus contém acentuação gráfica")),

        new(CampusErrorCodes.SiglaJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.campus.sigla_ja_existe",
                "Já existe um campus ativo com esta sigla")),

        new(CampusErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.nome_obrigatorio",
                "Nome do campus é obrigatório")),

        new(CampusErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.nome_tamanho",
                "Tamanho do nome do campus inválido")),

        new(CampusErrorCodes.CodigoEmecTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.codigo_emec_tamanho",
                "Tamanho do código e-MEC do campus inválido")),

        new(CampusErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.campus.nao_encontrado",
                "Campus não encontrado")),

        new(CampusErrorCodes.RemocaoBloqueadaPorLocalOferta,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.campus.remocao_bloqueada_por_local_oferta",
                "Não é possível remover um campus responsável por locais de oferta ativos")),
    ];
}
