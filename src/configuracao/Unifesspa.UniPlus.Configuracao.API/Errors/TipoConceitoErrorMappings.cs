namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Tipo de conceito da avaliação.</summary>
internal static class TipoConceitoErrorMappings
{
    private static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Identificacao =
    [
        new(TipoConceitoErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_conceito.codigo_obrigatorio", "Código do tipo de conceito é obrigatório")),
        new(TipoConceitoErrorCodes.CodigoTamanho,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_conceito.codigo_tamanho", "Tamanho do código do tipo de conceito inválido")),
        new(TipoConceitoErrorCodes.CodigoCaractereInvalido,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_conceito.codigo_caractere_invalido", "Código do tipo de conceito contém o caractere nulo ou caractere que não é texto")),
        new(TipoConceitoErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(StatusCodes.Status409Conflict,
                "uniplus.configuracao.tipo_conceito.codigo_ja_existe", "Código do tipo de conceito já está reservado")),
        new(TipoConceitoErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_conceito.nome_obrigatorio", "Nome do tipo de conceito é obrigatório")),
        new(TipoConceitoErrorCodes.NomeTamanho,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_conceito.nome_tamanho", "Tamanho do nome do tipo de conceito inválido")),
        new(TipoConceitoErrorCodes.NomeCaractereInvalido,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_conceito.nome_caractere_invalido", "Nome do tipo de conceito contém o caractere nulo ou caractere que não é texto")),
        new(TipoConceitoErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_conceito.descricao_tamanho", "Tamanho da descrição do tipo de conceito inválido")),
        new(TipoConceitoErrorCodes.DescricaoCaractereInvalido,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_conceito.descricao_caractere_invalido", "A descrição do tipo de conceito contém o caractere nulo, de controle ou de formatação")),
    ];

    private static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> NaturezaEParametros =
    [
        new(TipoConceitoErrorCodes.NaturezaInvalida,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_conceito.natureza_invalida", "Natureza do tipo de conceito inválida")),
        new(TipoConceitoErrorCodes.EscalaNumericaObrigatoria,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_conceito.escala_numerica_obrigatoria", "Escala do tipo de conceito numérico é obrigatória")),
        new(TipoConceitoErrorCodes.EscalaNumericaInvalida,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_conceito.escala_numerica_invalida", "Escala do tipo de conceito numérico inválida")),
        new(TipoConceitoErrorCodes.CasasDecimaisInvalidas,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_conceito.casas_decimais_invalidas", "Casas decimais do tipo de conceito inválidas")),
        new(TipoConceitoErrorCodes.ParametrosIncompativeisComANatureza,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_conceito.parametros_incompativeis_com_a_natureza",
                "Parâmetros incompatíveis com a natureza do tipo de conceito")),
    ];

    private static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Valores =
    [
        new(TipoConceitoErrorCodes.ValoresCategoricosInsuficientes,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_conceito.valores_categoricos_insuficientes",
                "Tipo de conceito categórico exige ao menos dois valores")),
        new(TipoConceitoErrorCodes.ValorCategoricoCodigoInvalido,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_conceito.valor_categorico_codigo_invalido", "Código do valor categórico inválido")),
        new(TipoConceitoErrorCodes.ValorCategoricoRotuloInvalido,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_conceito.valor_categorico_rotulo_invalido", "Rótulo do valor categórico inválido")),
        new(TipoConceitoErrorCodes.ValorCategoricoContaComoAprovacaoObrigatoria,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_conceito.valor_categorico_conta_como_aprovacao_obrigatoria",
                "É obrigatório informar se o valor categórico conta como aprovação")),
        new(TipoConceitoErrorCodes.ValorCategoricoDuplicado,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_conceito.valor_categorico_duplicado", "Valor categórico repetido no tipo de conceito")),
    ];

    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        .. Identificacao,
        .. NaturezaEParametros,
        .. Valores,
        new(TipoConceitoErrorCodes.NaoEncontrado,
            new DomainErrorMapping(StatusCodes.Status404NotFound,
                "uniplus.configuracao.tipo_conceito.nao_encontrado", "Tipo de conceito não encontrado")),
        new(TipoConceitoErrorCodes.ConflitoDeConcorrencia,
            new DomainErrorMapping(StatusCodes.Status409Conflict,
                "uniplus.configuracao.tipo_conceito.conflito_de_concorrencia",
                "Tipo de conceito alterado concorrentemente", RetryableConflict: true)),
    ];
}
