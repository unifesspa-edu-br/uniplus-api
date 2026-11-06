namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Condição de atendimento especializado (UNI-REQ-0012).</summary>
internal static class CondicaoAtendimentoErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(CondicaoAtendimentoErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.condicao_atendimento.codigo_obrigatorio",
                "Código da condição de atendimento é obrigatório")),

        new(CondicaoAtendimentoErrorCodes.CodigoFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.condicao_atendimento.codigo_formato_invalido",
                "Código da condição de atendimento em formato inválido")),

        new(CondicaoAtendimentoErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.condicao_atendimento.codigo_ja_existe",
                "Já existe uma condição de atendimento ativa com este código")),

        new(CondicaoAtendimentoErrorCodes.CodigoProtegidoNaoEditavel,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.condicao_atendimento.codigo_protegido_nao_editavel",
                "O código da condição reservada não pode ser alterado")),

        new(CondicaoAtendimentoErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.condicao_atendimento.nome_obrigatorio",
                "Nome da condição de atendimento é obrigatório")),

        new(CondicaoAtendimentoErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.condicao_atendimento.nome_tamanho",
                "Tamanho do nome da condição de atendimento inválido")),

        new(CondicaoAtendimentoErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.condicao_atendimento.descricao_tamanho",
                "Tamanho da descrição da condição de atendimento inválido")),

        new(CondicaoAtendimentoErrorCodes.NaoEncontrada,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.condicao_atendimento.nao_encontrada",
                "Condição de atendimento não encontrada")),

        new(CondicaoAtendimentoErrorCodes.RemocaoBloqueadaCodigoProtegido,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.condicao_atendimento.remocao_bloqueada_codigo_protegido",
                "A condição reservada não pode ser removida")),
    ];
}
