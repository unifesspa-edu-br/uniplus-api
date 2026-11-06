namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Categoria de documento (UNI-REQ-0013).</summary>
internal static class CategoriaDocumentoErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(CategoriaDocumentoErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.categoria_documento.codigo_obrigatorio",
                "Código da categoria de documento é obrigatório")),

        new(CategoriaDocumentoErrorCodes.CodigoFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.categoria_documento.codigo_formato_invalido",
                "Código da categoria de documento em formato inválido")),

        new(CategoriaDocumentoErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.categoria_documento.codigo_ja_existe",
                "Já existe uma categoria de documento ativa com este código")),

        new(CategoriaDocumentoErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.categoria_documento.nome_obrigatorio",
                "Nome da categoria de documento é obrigatório")),

        new(CategoriaDocumentoErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.categoria_documento.nome_tamanho",
                "Tamanho do nome da categoria de documento inválido")),

        new(CategoriaDocumentoErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.categoria_documento.descricao_tamanho",
                "Tamanho da descrição da categoria de documento inválido")),

        new(CategoriaDocumentoErrorCodes.OrdemInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.categoria_documento.ordem_invalida",
                "Ordem de exibição da categoria de documento inválida")),

        new(CategoriaDocumentoErrorCodes.NaoEncontrada,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.categoria_documento.nao_encontrada",
                "Categoria de documento não encontrada")),
    ];
}
