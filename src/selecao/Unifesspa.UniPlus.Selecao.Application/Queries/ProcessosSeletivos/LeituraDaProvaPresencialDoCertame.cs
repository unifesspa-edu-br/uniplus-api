namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

internal static class LeituraDaProvaPresencialDoCertame
{
    internal static Result<ProvaPresencialCertameDto> Ler(JsonObject envelope)
    {
        try
        {
            ProvaPresencialCertameDto? prova = envelope[ProjecaoDoCertamePublicado.BlocoPublico("provaPresencial")]
                ?.Deserialize<ProvaPresencialCertameDto>(ProjecaoDoCertamePublicado.OpcoesDoDocumento);
            return prova is null ? ProjecaoDoCertamePublicado.Recusar<ProvaPresencialCertameDto>("prova presencial") : Result<ProvaPresencialCertameDto>.Success(prova);
        }
        catch (JsonException)
        {
            return ProjecaoDoCertamePublicado.Recusar<ProvaPresencialCertameDto>("prova presencial");
        }
    }
}
