namespace Unifesspa.UniPlus.Infrastructure.Core.Errors.Regras;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Regras.Errors;

/// <summary>Códigos do predicado, das cláusulas e das condições sobre fatos do candidato, e do descritor do fato.</summary>
internal static class ErrosDoPredicado
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Mapeamentos =
    [
        new("ClausulaDnf.ClausulaVazia", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.clausula_dnf.clausula_vazia", "Uma cláusula do predicado deve ter ao menos uma condição")),
        new(PredicadoDnfErrorCodes.CondicaoNula, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.predicado_dnf.condicao_nula", "O predicado contém uma condição nula")),
        new("CondicaoDnf.FatoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.condicao_dnf.fato_obrigatorio", "O fato da condição é obrigatório")),
        new("CondicaoDnf.OperadorInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.condicao_dnf.operador_invalido", "O operador da condição não é reconhecido")),
        new("CondicaoDnf.FormaIncoerenteComOperador", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.condicao_dnf.forma_incoerente_com_operador", "A forma do valor não é coerente com o operador da condição")),
        new("PredicadoDnf.FatoDesconhecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.predicado_dnf.fato_desconhecido", "O fato da condição não pertence ao vocabulário fechado")),
        new(PredicadoDnfErrorCodes.OperadorIncompativelComDominio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.predicado_dnf.operador_incompativel_com_dominio", "O operador da condição não é compatível com o domínio do fato")),
        new(PredicadoDnfErrorCodes.ValorIncompativelComTipo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.predicado_dnf.valor_incompativel_com_tipo", "O valor da condição não é compatível com o tipo do fato")),
        new(PredicadoDnfErrorCodes.ValorForaDoDominio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.predicado_dnf.valor_fora_do_dominio", "O valor da condição não pertence ao domínio declarado do fato")),
        new("PredicadoDnf.FatoNaoColetadoPeloProcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.predicado_dnf.fato_nao_coletado_pelo_processo", "O fato da condição não é coletado por este processo")),
    ];
}
