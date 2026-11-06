namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Curso (UNI-REQ-0010).</summary>
internal static class CursoErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(CursoErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.curso.codigo_obrigatorio",
                "Código do curso é obrigatório")),

        new(CursoErrorCodes.CodigoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.curso.codigo_tamanho",
                "Tamanho do código do curso inválido")),

        new(CursoErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.curso.codigo_ja_existe",
                "Já existe um curso ativo com este código")),

        new(CursoErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.curso.nome_obrigatorio",
                "Nome do curso é obrigatório")),

        new(CursoErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.curso.nome_tamanho",
                "Tamanho do nome do curso inválido")),

        new(CursoErrorCodes.GrauObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.curso.grau_obrigatorio",
                "Grau do curso é obrigatório")),

        new(CursoErrorCodes.GrauTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.curso.grau_tamanho",
                "Tamanho do grau do curso inválido")),

        new(CursoErrorCodes.NivelEnsinoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.curso.nivel_ensino_obrigatorio",
                "Nível de ensino do curso é obrigatório")),

        new(CursoErrorCodes.NivelEnsinoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.curso.nivel_ensino_tamanho",
                "Tamanho do nível de ensino do curso inválido")),

        new(CursoErrorCodes.GrupoAreaEnemInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.curso.grupo_area_enem_invalido",
                "Grupo de área do ENEM fora do domínio fechado")),

        new(CursoErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.curso.nao_encontrado",
                "Curso não encontrado")),

        new(CursoErrorCodes.RemocaoBloqueadaPorOfertaCurso,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.curso.remocao_bloqueada_por_oferta_curso",
                "Não é possível remover um curso referenciado por uma oferta de curso ativa")),
    ];
}
