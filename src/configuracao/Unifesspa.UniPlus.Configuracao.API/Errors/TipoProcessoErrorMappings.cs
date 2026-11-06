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
                "uniplus.configuracao.tipo_processo.codigo_reservado", "Código reservado para obrigatoriedade legal universal")),
        new(TipoProcessoErrorCodes.CodigoTamanho,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_processo.codigo_tamanho", "Tamanho do código do tipo de processo seletivo inválido")),
        new(TipoProcessoErrorCodes.CodigoComCaractereNulo,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_processo.codigo_com_caractere_nulo", "Código do tipo de processo seletivo contém U+0000")),
        new(TipoProcessoErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(StatusCodes.Status409Conflict,
                "uniplus.configuracao.tipo_processo.codigo_ja_existe", "Código do tipo de processo seletivo já está reservado")),
        new(TipoProcessoErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_processo.nome_obrigatorio", "Nome do tipo de processo seletivo é obrigatório")),
        new(TipoProcessoErrorCodes.NomeTamanho,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_processo.nome_tamanho", "Tamanho do nome do tipo de processo seletivo inválido")),
        new(TipoProcessoErrorCodes.NomeComCaractereNulo,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_processo.nome_com_caractere_nulo", "Nome do tipo de processo seletivo contém U+0000")),
        new(TipoProcessoErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_processo.descricao_tamanho", "Tamanho da descrição do tipo de processo seletivo inválido")),
        new(TipoProcessoErrorCodes.DescricaoComCaractereNulo,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_processo.descricao_com_caractere_nulo", "Descrição do tipo de processo seletivo contém U+0000")),
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
