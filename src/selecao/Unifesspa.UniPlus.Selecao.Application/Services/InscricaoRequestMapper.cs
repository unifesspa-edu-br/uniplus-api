namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Domain.Entities;
using Domain.Errors;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;
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

    /// <summary>
    /// Os grupos do corpo na forma do domínio. A anotação de nulidade não alcança os elementos de uma
    /// coleção genérica, então um corpo sintaticamente válido pode trazer a lista de um grupo nula ou
    /// uma ocorrência nula; ambas são recusadas aqui, no caminho do grupo (<c>GRUPO</c>) ou da
    /// ocorrência (<c>GRUPO[i]</c>), antes de qualquer outro uso do corpo. Conferir o id da ocorrência cabe ao
    /// agregado.
    /// </summary>
    public static Result<Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>> ParaGrupos(
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>? grupos)
    {
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> resultado = new(StringComparer.Ordinal);
        if (grupos is null)
        {
            return Result<Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>>.Success(resultado);
        }

        List<FieldError> recusas = [];
        foreach ((string codigo, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>? ocorrencias) in grupos)
        {
            if (ocorrencias is null)
            {
                recusas.Add(new FieldError(codigo, new DomainError(
                    InscricaoErrorCodes.GrupoMalFormado,
                    $"O grupo '{codigo}' veio sem a lista de ocorrências. Envie a lista, vazia se não houver nenhuma, ou remova o grupo do corpo.")));
                continue;
            }

            List<OcorrenciaDeGrupoInscricao> lidas = new(ocorrencias.Count);
            for (int i = 0; i < ocorrencias.Count; i++)
            {
                if (ocorrencias[i] is not { } ocorrencia)
                {
                    recusas.Add(new FieldError($"{codigo}[{i}]", new DomainError(
                        InscricaoErrorCodes.GrupoMalFormado,
                        $"A ocorrência {i + 1} do grupo '{codigo}' veio nula. Envie a ocorrência com id e respostas, ou remova-a da lista.")));
                    continue;
                }

                lidas.Add(new OcorrenciaDeGrupoInscricao(
                    ocorrencia.Id,
                    ocorrencia.Respostas is { } respostas ? new Dictionary<string, JsonElement>(respostas, StringComparer.Ordinal) : new(StringComparer.Ordinal)));
            }

            resultado[codigo] = lidas;
        }

        return recusas.Count > 0
            ? Result<Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>>.ValidationFailure(recusas)
            : Result<Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>>.Success(resultado);
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
