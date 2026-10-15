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

    /// <summary>
    /// Os aceites do corpo na forma do domínio. A anotação de nulidade não alcança os elementos de uma
    /// coleção genérica, nem os campos de texto de um registro desserializado: um corpo sintaticamente
    /// válido pode trazer um aceite nulo, ou um aceite sem código ou sem hash. Cada um é recusado no
    /// caminho do aceite (<c>aceites[i]</c>) ou do campo (<c>aceites[i].codigo</c>,
    /// <c>aceites[i].hashVersao</c>), antes de qualquer outro uso do corpo — um texto nulo chegaria à
    /// gravação, cujas colunas o exigem. Conferir se o termo é exigido e se a versão é a fixada cabe ao
    /// veredito da submissão.
    /// </summary>
    public static Result<List<AceiteDeTermo>> ParaAceites(IReadOnlyList<AceiteDeTermoInput>? aceites)
    {
        List<AceiteDeTermo> lidos = new(aceites?.Count ?? 0);
        if (aceites is null)
        {
            return Result<List<AceiteDeTermo>>.Success(lidos);
        }

        List<FieldError> recusas = [];
        for (int i = 0; i < aceites.Count; i++)
        {
            if (aceites[i] is not { } aceite)
            {
                recusas.Add(RecusaDoAceite(
                    $"aceites[{i}]",
                    $"O aceite {i + 1} veio nulo. Envie o aceite com código do termo, id da versão e hash da versão, ou remova-o da lista."));
                continue;
            }

            if (aceite.Codigo is null)
            {
                recusas.Add(RecusaDoAceite(
                    $"aceites[{i}].codigo",
                    $"O aceite {i + 1} veio sem o código do termo. Envie o código do termo aceito, ou remova o aceite da lista."));
            }

            if (aceite.HashVersao is null)
            {
                recusas.Add(RecusaDoAceite(
                    $"aceites[{i}].hashVersao",
                    $"O aceite {i + 1} veio sem o hash da versão do termo. Envie o hash da versão que o formulário apresentou, ou remova o aceite da lista."));
            }

            if (aceite.Codigo is not null && aceite.HashVersao is not null)
            {
                lidos.Add(new AceiteDeTermo(aceite.Codigo, aceite.VersaoId, aceite.HashVersao));
            }
        }

        return recusas.Count > 0
            ? Result<List<AceiteDeTermo>>.ValidationFailure(recusas)
            : Result<List<AceiteDeTermo>>.Success(lidos);
    }

    private static FieldError RecusaDoAceite(string campo, string mensagem) =>
        new(campo, new DomainError(InscricaoErrorCodes.AceiteMalFormado, mensagem));

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
