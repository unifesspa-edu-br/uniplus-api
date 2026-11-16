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
        new("ReferenciaRegra.CodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_regra.codigo_obrigatorio", "Código da regra referenciada obrigatório")),
        new("ReferenciaRegra.VersaoObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_regra.versao_obrigatoria", "Versão da regra referenciada obrigatória")),
        new("ReferenciaRegra.HashInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_regra.hash_invalido", "Hash da regra referenciada inválido")),
    ];
}
