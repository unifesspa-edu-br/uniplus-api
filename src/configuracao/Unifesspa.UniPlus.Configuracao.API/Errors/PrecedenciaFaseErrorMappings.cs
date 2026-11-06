namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Precedência de fase (UNI-REQ-0139).</summary>
internal static class PrecedenciaFaseErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(PrecedenciaFaseErrorCodes.AntecessoraCodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.precedencia_fase.antecessora_codigo_obrigatorio",
                "Código da fase antecessora é obrigatório")),

        new(PrecedenciaFaseErrorCodes.AntecessoraCodigoFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.precedencia_fase.antecessora_codigo_formato_invalido",
                "Código da fase antecessora em formato inválido")),

        new(PrecedenciaFaseErrorCodes.AntecessoraForaDoConjuntoCanonico,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.precedencia_fase.antecessora_fora_do_conjunto_canonico",
                "Código da fase antecessora fora do conjunto canônico")),

        new(PrecedenciaFaseErrorCodes.SucessoraCodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.precedencia_fase.sucessora_codigo_obrigatorio",
                "Código da fase sucessora é obrigatório")),

        new(PrecedenciaFaseErrorCodes.SucessoraCodigoFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.precedencia_fase.sucessora_codigo_formato_invalido",
                "Código da fase sucessora em formato inválido")),

        new(PrecedenciaFaseErrorCodes.SucessoraForaDoConjuntoCanonico,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.precedencia_fase.sucessora_fora_do_conjunto_canonico",
                "Código da fase sucessora fora do conjunto canônico")),

        new(PrecedenciaFaseErrorCodes.SelfLoop,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.precedencia_fase.self_loop",
                "A fase antecessora não pode ser igual à fase sucessora")),

        new(PrecedenciaFaseErrorCodes.ArestaDuplicada,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.precedencia_fase.aresta_duplicada",
                "Já existe uma aresta de precedência viva com este par de fases")),

        new(PrecedenciaFaseErrorCodes.CicloDetectado,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.precedencia_fase.ciclo_detectado",
                "A aresta fecharia um ciclo no grafo de precedências")),

        new(PrecedenciaFaseErrorCodes.NaoEncontrada,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.precedencia_fase.nao_encontrada",
                "Aresta de precedência não encontrada")),
    ];
}
