namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Tipo de deficiência (UNI-REQ-0012, UNI-REQ-0061).</summary>
internal static class TipoDeficienciaErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(TipoDeficienciaErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_deficiencia.codigo_obrigatorio",
                "Código do tipo de deficiência é obrigatório")),

        new(TipoDeficienciaErrorCodes.CodigoFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_deficiencia.codigo_formato_invalido",
                "Código do tipo de deficiência em formato inválido")),

        new(TipoDeficienciaErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.tipo_deficiencia.codigo_ja_existe",
                "Já existe um tipo de deficiência ativo com este código")),

        new(TipoDeficienciaErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_deficiencia.nome_obrigatorio",
                "Nome do tipo de deficiência é obrigatório")),

        new(TipoDeficienciaErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_deficiencia.nome_tamanho",
                "Tamanho do nome do tipo de deficiência inválido")),

        new(TipoDeficienciaErrorCodes.NomeJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.tipo_deficiencia.nome_ja_existe",
                "Já existe um tipo de deficiência ativo com este nome")),

        new(TipoDeficienciaErrorCodes.DescricaoObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_deficiencia.descricao_obrigatoria",
                "Descrição do tipo de deficiência é obrigatória")),

        new(TipoDeficienciaErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_deficiencia.descricao_tamanho",
                "Tamanho da descrição do tipo de deficiência inválido")),

        new(TipoDeficienciaErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.tipo_deficiencia.nao_encontrado",
                "Tipo de deficiência não encontrado")),
    ];
}
