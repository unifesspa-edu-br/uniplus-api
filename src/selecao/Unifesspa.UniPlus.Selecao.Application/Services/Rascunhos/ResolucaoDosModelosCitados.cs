namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Resolve as citações de modelos pertencentes ao rascunho, na ordem das exigências.</summary>
public sealed class ResolucaoDosModelosCitados(IModeloDeDocumentoRepository modelos)
{
    public async Task<ResultadoDosModelosCitados> ResolverAsync(Guid donoId, JsonObject documento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documento);
        List<(int Posicao, Guid Id)> citados = Citacoes(documento);
        IReadOnlyList<ModeloDeDocumento> encontrados = citados.Count == 0 ? [] : await modelos
            .ListarDoProcessoAsync(donoId, [.. citados.Select(static citado => citado.Id).Distinct()], cancellationToken)
            .ConfigureAwait(false);
        return ResultadoDosModelosCitados.DasCitacoes(citados, encontrados.ToDictionary(static modelo => modelo.Id));
    }

    private static List<(int Posicao, Guid Id)> Citacoes(JsonObject documento)
    {
        List<(int Posicao, Guid Id)> citados = [];
        if (ExigenciasDoDocumento.Lista(documento) is JsonArray exigencias)
        {
            for (int posicao = 0; posicao < exigencias.Count; posicao++)
            {
                if (exigencias[posicao] is JsonObject item && Guid.TryParse(item["modeloId"]?.GetValue<string>(), out Guid id))
                {
                    citados.Add((posicao, id));
                }
            }
        }

        return citados;
    }
}
