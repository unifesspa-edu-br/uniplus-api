namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Valor de domínio de fato do candidato (ADR-0116).</summary>
internal static class FatoValorDominioErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(FatoValorDominioErrorCodes.NaoPermitidoForaDeCategorico,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_valor_dominio.nao_permitido_fora_de_categorico",
                "Só fato categórico tem valores de domínio próprios")),

        new(FatoValorDominioErrorCodes.NaoPermitidoForaDeFonteGlobal,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_valor_dominio.nao_permitido_fora_de_fonte_global",
                "Valores de domínio só são declarados no catálogo quando a fonte dos valores é global")),

        new(FatoValorDominioErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_valor_dominio.codigo_obrigatorio",
                "Código do valor de domínio é obrigatório")),

        new(FatoValorDominioErrorCodes.CodigoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_valor_dominio.codigo_tamanho",
                "Tamanho do código do valor de domínio inválido")),

        new(FatoValorDominioErrorCodes.CodigoDuplicado,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_valor_dominio.codigo_duplicado",
                "Já existe um valor de domínio com este código neste fato")),

        new(FatoValorDominioErrorCodes.DescricaoObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_valor_dominio.descricao_obrigatoria",
                "Descrição do valor de domínio é obrigatória quando a origem do fato é DECLARADO")),

        new(FatoValorDominioErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_valor_dominio.descricao_tamanho",
                "Tamanho da descrição do valor de domínio inválido")),

        new(FatoValorDominioErrorCodes.OrientacaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_valor_dominio.orientacao_tamanho",
                "Tamanho da orientação do valor de domínio inválido")),

        new(FatoValorDominioErrorCodes.OrdemInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_valor_dominio.ordem_invalida",
                "Ordem do valor de domínio não pode ser negativa")),

        new(FatoValorDominioErrorCodes.OrdemComCodigoDesconhecido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_valor_dominio.ordem_com_codigo_desconhecido",
                "A ordem cita um valor que o fato não tem")),

        new(FatoValorDominioErrorCodes.OrdemComCodigoRepetido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_valor_dominio.ordem_com_codigo_repetido",
                "A ordem repete um valor")),

        new(FatoValorDominioErrorCodes.OrdemSemTodosOsValores,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_valor_dominio.ordem_sem_todos_os_valores",
                "A ordem deve trazer todos os valores do fato")),

        new(FatoValorDominioErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.fato_valor_dominio.nao_encontrado",
                "Valor de domínio não encontrado neste fato")),

        new(FatoValorDominioErrorCodes.JaAtivo,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_valor_dominio.ja_ativo",
                "O valor já está ativo")),

        new(FatoValorDominioErrorCodes.JaDesativado,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_valor_dominio.ja_desativado",
                "O valor já está desativado")),
    ];
}
