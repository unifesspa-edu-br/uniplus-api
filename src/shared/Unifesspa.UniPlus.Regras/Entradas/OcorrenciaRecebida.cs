namespace Unifesspa.UniPlus.Regras.Entradas;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>Uma ocorrência de grupo repetível recebida numa simulação: a identidade dela e as respostas dos campos.</summary>
public sealed record OcorrenciaRecebida(string? Id, IReadOnlyDictionary<string, JsonElement>? Respostas);

/// <summary>
/// Lê as ocorrências simuladas dos grupos para o avaliador: cada uma precisa de uma identidade própria,
/// que não se repita no grupo, porque é por ela que o documento exigido por membro se liga à ocorrência
/// (UNI-REQ-0069). A ocorrência sem identidade, ou com a de outra, é recusada com o caminho dela.
/// </summary>
public static class OcorrenciasRecebidas
{
    public static (Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> Ocorrencias, List<FieldError> Erros) Ler(
        IEnumerable<KeyValuePair<string, IReadOnlyList<OcorrenciaRecebida>?>>? grupos, string codigoDeErro)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoDeErro);

        Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> ocorrencias = new(StringComparer.Ordinal);
        List<FieldError> erros = [];
        foreach ((string grupo, IReadOnlyList<OcorrenciaRecebida>? recebidas) in grupos ?? [])
        {
            HashSet<string> vistas = new(StringComparer.Ordinal);
            List<OcorrenciaRespondida> respondidas = [];
            IReadOnlyList<OcorrenciaRecebida> lista = recebidas ?? [];
            for (int i = 0; i < lista.Count; i++)
            {
                if (lista[i]?.Id is not { } id || string.IsNullOrWhiteSpace(id) || !vistas.Add(id))
                {
                    erros.Add(new($"grupos.{grupo}[{i}].id", new DomainError(
                        codigoDeErro, "Cada ocorrência do grupo precisa de uma identidade própria, que não se repita no grupo.")));
                    continue;
                }

                respondidas.Add(new OcorrenciaRespondida(id, lista[i].Respostas ?? new Dictionary<string, JsonElement>()));
            }

            ocorrencias[grupo] = respondidas;
        }

        return (ocorrencias, erros);
    }
}
