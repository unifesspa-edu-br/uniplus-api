namespace Unifesspa.UniPlus.Infrastructure.Core.Errors.Regras;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Regras.Errors;

/// <summary>Códigos da restrição de valor declarada num campo do formulário.</summary>
internal static class ErrosDaRestricaoDeValor
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Mapeamentos =
    [
        new(RestricaoValorErrorCodes.TipoDesconhecido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.restricao_valor.tipo_desconhecido", "O tipo da restrição de valor não é reconhecido")),
        new(RestricaoValorErrorCodes.LimitesIncoerentes, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.restricao_valor.limites_incoerentes", "Os limites da restrição de valor são incoerentes")),
        new(RestricaoValorErrorCodes.OpcoesVazias, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.restricao_valor.opcoes_vazias", "As opções permitidas precisam de valores")),
        new(RestricaoValorErrorCodes.FatosVazios, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.restricao_valor.fatos_vazios", "As opções formadas pelas respostas precisam citar fatos")),
        new(RestricaoValorErrorCodes.FormaJsonInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.restricao_valor.forma_json_invalida", "A restrição de valor não tem a forma esperada")),
        new(RestricaoValorErrorCodes.TipoRepetido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.restricao_valor.tipo_repetido", "O item declara no máximo uma restrição de cada tipo")),
        new(RestricaoValorErrorCodes.LimiteDeDataInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.restricao_valor.limite_de_data_invalido", "O limite da faixa de data é uma data fixa ou o envio deslocado em anos e dias")),
    ];
}
