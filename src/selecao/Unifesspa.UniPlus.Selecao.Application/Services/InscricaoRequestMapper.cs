namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Domain.Entities;

using DTOs;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Converte o corpo de <see cref="InscricaoRequest"/> para os tipos de domínio — reusado pelos
/// handlers de Salvar e de Submeter (issue #1859), que fazem a mesma tradução antes de rodar a
/// avaliação ou mutar o agregado.
/// </summary>
public static class InscricaoRequestMapper
{
    public static Dictionary<string, JsonElement> ParaRespostas(IReadOnlyDictionary<string, JsonElement>? respostas) =>
        respostas is { } r ? new(r, StringComparer.Ordinal) : new(StringComparer.Ordinal);

    public static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> ParaGrupos(
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>? grupos)
    {
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> resultado = new(StringComparer.Ordinal);
        if (grupos is null)
        {
            return resultado;
        }

        foreach ((string codigo, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput> ocorrencias) in grupos)
        {
            resultado[codigo] = [.. ocorrencias.Select(static o => new OcorrenciaDeGrupoInscricao(
                o.Id, o.Respostas is { } respostas ? new Dictionary<string, JsonElement>(respostas, StringComparer.Ordinal) : new(StringComparer.Ordinal)))];
        }

        return resultado;
    }

    public static List<AceiteDeTermo> ParaAceites(IReadOnlyList<AceiteDeTermoInput>? aceites) =>
        [.. (aceites ?? []).Select(static a => new AceiteDeTermo(a.Codigo, a.VersaoId, a.HashVersao))];

    /// <summary>Os grupos de domínio, na forma que o avaliador de <c>Unifesspa.UniPlus.Regras</c> espera.</summary>
    public static Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> ParaAvaliacao(
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos)
    {
        ArgumentNullException.ThrowIfNull(grupos);

        Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> resultado = new(StringComparer.Ordinal);
        foreach ((string codigo, IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias) in grupos)
        {
            resultado[codigo] = [.. ocorrencias.Select(static o => new OcorrenciaRespondida(o.Id, o.Respostas))];
        }

        return resultado;
    }
}
