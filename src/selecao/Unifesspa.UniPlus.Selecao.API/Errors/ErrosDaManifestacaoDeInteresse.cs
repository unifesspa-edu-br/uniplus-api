namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;

internal static class ErrosDaManifestacaoDeInteresse
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        new(ManifestacaoDeInteresseErrorCodes.CodigoDesconhecido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.manifestacao_de_interesse.codigo_desconhecido", "Manifestação de interesse desconhecida")),
    ];
}
