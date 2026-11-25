namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Tipo de processo seletivo (UNI-REQ-0098).</summary>
internal static class TipoProcessoErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(TipoProcessoErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_processo.codigo_obrigatorio", "Código do tipo de processo seletivo é obrigatório")),
        new(TipoProcessoErrorCodes.CodigoReservado,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_processo.codigo_reservado", "Código reservado para obrigatoriedade legal que vale para todos os tipos")),
        new(TipoProcessoErrorCodes.CodigoFormatoInvalido,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_processo.codigo_formato_invalido", "Código do tipo de processo seletivo fora do formato")),
        new(TipoProcessoErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(StatusCodes.Status409Conflict,
                "uniplus.configuracao.tipo_processo.codigo_ja_existe", "Código do tipo de processo seletivo já está reservado")),
        new(TipoProcessoErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_processo.nome_obrigatorio", "Nome do tipo de processo seletivo é obrigatório")),
        new(TipoProcessoErrorCodes.NomeTamanho,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_processo.nome_tamanho", "Tamanho do nome do tipo de processo seletivo inválido")),
        new(TipoProcessoErrorCodes.NomeCaractereInvalido,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_processo.nome_caractere_invalido", "Nome do tipo de processo seletivo contém o caractere nulo ou caractere que não é texto")),
        new(TipoProcessoErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_processo.descricao_tamanho", "Tamanho da descrição do tipo de processo seletivo inválido")),
        new(TipoProcessoErrorCodes.DescricaoCaractereInvalido,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_processo.descricao_caractere_invalido", "A descrição do tipo de processo seletivo contém o caractere nulo, de controle ou de formatação")),
        new(TipoProcessoErrorCodes.NaoEncontrado,
            new DomainErrorMapping(StatusCodes.Status404NotFound,
                "uniplus.configuracao.tipo_processo.nao_encontrado", "Tipo de processo seletivo não encontrado")),
        new(TipoProcessoErrorCodes.JaDesativado,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_processo.ja_desativado", "Tipo de processo seletivo já está desativado")),
        new(TipoProcessoErrorCodes.JaAtivo,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_processo.ja_ativo", "Tipo de processo seletivo já está ativo")),
        new(TipoProcessoErrorCodes.ConflitoDeConcorrencia,
            new DomainErrorMapping(StatusCodes.Status409Conflict,
                "uniplus.configuracao.tipo_processo.conflito_de_concorrencia",
                "Tipo de processo seletivo alterado concorrentemente", RetryableConflict: true)),
    ];
}
