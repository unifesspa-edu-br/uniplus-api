namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Recusas da definição de uma regra do catálogo e da referência tipada que a configuração embute.
/// </summary>
internal static class ErrosDoCatalogoDeRegras
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        new("RegraCatalogo.CodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_catalogo.codigo_obrigatorio", "Código da regra obrigatório")),
        new("RegraCatalogo.VersaoObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_catalogo.versao_obrigatoria", "Versão da regra obrigatória")),
        new("RegraCatalogo.TipoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_catalogo.tipo_obrigatorio", "Tipo da regra obrigatório")),
        new("RegraCatalogo.EsquemaArgsInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_catalogo.esquema_args_invalido", "esquema_args deve ser um objeto JSON")),
        new("RegraCatalogo.InvariantesInvalidas", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_catalogo.invariantes_invalidas", "invariantes deve ser um array JSON")),
        new("RegraCatalogo.BaseLegalObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_catalogo.base_legal_obrigatoria", "Base legal da regra obrigatória")),
        new("ReferenciaRegra.CodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_regra.codigo_obrigatorio", "Código da regra referenciada obrigatório")),
        new("ReferenciaRegra.VersaoObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_regra.versao_obrigatoria", "Versão da regra referenciada obrigatória")),
        new("ReferenciaRegra.HashInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_regra.hash_invalido", "Hash da regra referenciada inválido")),
    ];
}
