namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Recurso de acessibilidade (UNI-REQ-0012, UNI-REQ-0061).</summary>
internal static class RecursoAcessibilidadeErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(RecursoAcessibilidadeErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.recurso_acessibilidade.codigo_obrigatorio",
                "Código do recurso de acessibilidade é obrigatório")),

        new(RecursoAcessibilidadeErrorCodes.CodigoFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.recurso_acessibilidade.codigo_formato_invalido",
                "Código do recurso de acessibilidade em formato inválido")),

        new(RecursoAcessibilidadeErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.recurso_acessibilidade.codigo_ja_existe",
                "Já existe um recurso de acessibilidade ativo com este código")),

        new(RecursoAcessibilidadeErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.recurso_acessibilidade.nome_obrigatorio",
                "Nome do recurso de acessibilidade é obrigatório")),

        new(RecursoAcessibilidadeErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.recurso_acessibilidade.nome_tamanho",
                "Tamanho do nome do recurso de acessibilidade inválido")),

        new(RecursoAcessibilidadeErrorCodes.NomeJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.recurso_acessibilidade.nome_ja_existe",
                "Já existe um recurso de acessibilidade ativo com este nome")),

        new(RecursoAcessibilidadeErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.recurso_acessibilidade.descricao_tamanho",
                "Tamanho da descrição do recurso de acessibilidade inválido")),

        new(RecursoAcessibilidadeErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.recurso_acessibilidade.nao_encontrado",
                "Recurso de acessibilidade não encontrado")),
    ];
}
