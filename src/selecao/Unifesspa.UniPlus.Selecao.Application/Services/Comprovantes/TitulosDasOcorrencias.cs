namespace Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Como o formulário titula cada ocorrência de um grupo repetível: a do próprio candidato é <c>Você (candidato)</c> e as demais
/// são integrantes, numerados a partir de 1 na ordem da lista.
/// </summary>
internal static class TitulosDasOcorrencias
{
    public const string DoCandidato = "Você (candidato)";

    /// <summary>O título de cada ocorrência respondida, pelo id dela.</summary>
    public static Dictionary<string, string> Dos(
        IReadOnlyList<GrupoDoProcesso> grupos, IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> respondidos)
    {
        Dictionary<string, string> titulos = new(StringComparer.Ordinal);
        foreach (GrupoDoProcesso grupo in grupos.Where(g => respondidos.ContainsKey(g.Codigo)))
        {
            foreach ((OcorrenciaDeGrupoInscricao ocorrencia, string titulo) in Do(grupo.IncluiCandidato, respondidos[grupo.Codigo]))
            {
                titulos.TryAdd(ocorrencia.Id, titulo);
            }
        }

        return titulos;
    }

    private static IEnumerable<(OcorrenciaDeGrupoInscricao Ocorrencia, string Titulo)> Do(
        bool incluiCandidato, IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias)
    {
        OcorrenciaDeGrupoInscricao? doCandidato = incluiCandidato
            ? ocorrencias.FirstOrDefault(static o => CandidatoComoMembro.EhDoCandidato(o.Respostas))
            : null;
        int integrante = 0;
        foreach (OcorrenciaDeGrupoInscricao ocorrencia in ocorrencias)
        {
            yield return (ocorrencia, ocorrencia == doCandidato ? DoCandidato : $"Integrante {++integrante}");
        }
    }
}
