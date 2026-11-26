namespace Unifesspa.UniPlus.Infrastructure.Core.Errors.Regras;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Regras.Errors;

/// <summary>Códigos do vínculo do campo com o catálogo de fatos do candidato.</summary>
internal static class ErrosDoVinculoComOCatalogo
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Mapeamentos =
    [
        new(VinculoCatalogoErrorCodes.FatoDesativado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.vinculo_catalogo.fato_desativado", "O dado do candidato está desativado no catálogo e não aceita vínculo novo")),
        new(VinculoCatalogoErrorCodes.FatoComProtecaoPendente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.vinculo_catalogo.fato_com_protecao_pendente", "O dado do candidato aguarda a aprovação do Representante do controlador e não aceita vínculo novo")),
        new(VinculoCatalogoErrorCodes.ValorDesativado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.vinculo_catalogo.valor_desativado", "O valor de um dado do candidato está desativado no catálogo e não aceita vínculo novo")),
    ];
}
