namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Acrescenta à identificação o nome e a UF do município da localidade, com uma só consulta ao Geo. O Geo fora do ar, ou sem o
/// município, não impede a exportação: os dois descritivos ficam de fora, pois a localidade já está no documento pelo código IBGE.
/// </summary>
public sealed class DescricaoDaLocalidade(IConferenciaEnderecoGeo geo)
{
    private const string CampoDoCodigo = "localidadeCodigoIbge";

    public async Task DescreverAsync(JsonObject documento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documento);
        if (documento["identificacao"] is not JsonObject identificacao || CodigoIbgeDa(identificacao) is not { } codigo)
        {
            return;
        }

        LocalidadeRegente? localidade = await ConferirAsync(codigo, cancellationToken).ConfigureAwait(false);
        if (localidade is not null)
        {
            Escrever(identificacao, localidade);
        }
    }

    private static string? CodigoIbgeDa(JsonObject identificacao) =>
        identificacao[CampoDoCodigo] is JsonValue valor && valor.TryGetValue(out string? codigo) ? codigo : null;

    private async Task<LocalidadeRegente?> ConferirAsync(string codigo, CancellationToken cancellationToken)
    {
        if (ReferenciaCidadeGeo.UfDoCodigoIbge(codigo) is not { } uf)
        {
            return null;
        }

        Result<string> municipio = await geo.ConferirMunicipioAsync(codigo, uf, cancellationToken).ConfigureAwait(false);
        if (municipio.IsFailure)
        {
            return null;
        }

        Result<LocalidadeRegente> localidade = LocalidadeRegente.Criar(codigo, municipio.Value, uf);
        return localidade.IsSuccess ? localidade.Value : null;
    }

    private static void Escrever(JsonObject identificacao, LocalidadeRegente localidade)
    {
        int posicao = identificacao.IndexOf(CampoDoCodigo) + 1;
        identificacao.Insert(posicao, "localidadeNome", localidade.Nome);
        identificacao.Insert(posicao + 1, "localidadeUf", localidade.Uf);
    }
}
