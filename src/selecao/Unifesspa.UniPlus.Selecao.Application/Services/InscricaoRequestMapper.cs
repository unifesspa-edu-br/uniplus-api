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
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>> SemGrupos =
        new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>();

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
        List<FieldError> recusas = [];
        foreach ((string codigo, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>? ocorrencias) in grupos ?? SemGrupos)
        {
            LerGrupo(codigo, ocorrencias, resultado, recusas);
        }

        return recusas.Count > 0
            ? Result<Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>>.ValidationFailure(recusas)
            : Result<Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>>.Success(resultado);
    }

    private static void LerGrupo(
        string codigo,
        IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>? ocorrencias,
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> resultado,
        List<FieldError> recusas)
    {
        if (ocorrencias is null)
        {
            recusas.Add(new FieldError(codigo, new DomainError(
                InscricaoErrorCodes.GrupoMalFormado,
                $"O grupo '{codigo}' veio sem a lista de ocorrências. Envie a lista, vazia se não houver nenhuma, ou remova o grupo do corpo.")));
            return;
        }

        resultado[codigo] = LerOcorrencias(codigo, ocorrencias, recusas);
    }

    private static List<OcorrenciaDeGrupoInscricao> LerOcorrencias(
        string codigo, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput> ocorrencias, List<FieldError> recusas)
    {
        List<OcorrenciaDeGrupoInscricao> lidas = new(ocorrencias.Count);
        for (int i = 0; i < ocorrencias.Count; i++)
        {
            if (ocorrencias[i] is { } ocorrencia)
            {
                lidas.Add(new OcorrenciaDeGrupoInscricao(ocorrencia.Id, ParaRespostas(ocorrencia.Respostas)));
            }
            else
            {
                recusas.Add(OcorrenciaNula(codigo, i));
            }
        }

        return lidas;
    }

    private static FieldError OcorrenciaNula(string codigo, int posicao) => new(
        $"{codigo}[{posicao}]",
        new DomainError(
            InscricaoErrorCodes.GrupoMalFormado,
            $"A ocorrência {posicao + 1} do grupo '{codigo}' veio nula. Envie a ocorrência com id e respostas, ou remova-a da lista."));

    /// <summary>
    /// Os aceites do corpo na forma do domínio. A anotação de nulidade não alcança os elementos de uma
    /// coleção genérica, nem os campos de texto de um registro desserializado: um corpo sintaticamente
    /// válido pode trazer um aceite nulo, ou um aceite sem o código do formulário, sem o código do termo ou
    /// sem hash. Cada um é recusado no caminho do aceite (<c>aceites[i]</c>) ou do campo
    /// (<c>aceites[i].formularioCodigo</c>, <c>aceites[i].codigo</c>, <c>aceites[i].hashVersao</c>), antes de
    /// qualquer outro uso do corpo — um texto nulo chegaria à gravação, cujas colunas o exigem. Conferir se o
    /// termo é exigido pelo formulário e se a versão é a fixada cabe ao veredito da submissão.
    /// </summary>
    public static Result<List<AceiteDeTermo>> ParaAceites(IReadOnlyList<AceiteDeTermoInput>? aceites)
    {
        List<AceiteDeTermo> lidos = new(aceites?.Count ?? 0);
        List<FieldError> recusas = [];
        for (int i = 0; i < (aceites?.Count ?? 0); i++)
        {
            LerAceite(aceites![i], i, lidos, recusas);
        }

        return recusas.Count > 0
            ? Result<List<AceiteDeTermo>>.ValidationFailure(recusas)
            : Result<List<AceiteDeTermo>>.Success(lidos);
    }

    private static void LerAceite(AceiteDeTermoInput? aceite, int posicao, List<AceiteDeTermo> lidos, List<FieldError> recusas)
    {
        FieldError[] doAceite = [.. RecusasDoAceite(aceite, posicao)];
        recusas.AddRange(doAceite);
        if (doAceite.Length == 0)
        {
            lidos.Add(new AceiteDeTermo(aceite!.FormularioCodigo, aceite.Codigo, aceite.VersaoId, aceite.HashVersao));
        }
    }

    private static IEnumerable<FieldError> RecusasDoAceite(AceiteDeTermoInput? aceite, int posicao)
    {
        if (aceite is null)
        {
            return [RecusaDoAceite(
                $"aceites[{posicao}]",
                $"O aceite {posicao + 1} veio nulo. Envie o aceite com código do formulário, código do termo, id da versão e hash da versão, ou remova-o da lista.")];
        }

        return CamposAusentesDoAceite(aceite).Select(ausente => RecusaDoAceite(
            $"aceites[{posicao}].{ausente.Campo}",
            $"O aceite {posicao + 1} veio sem {ausente.Descricao}. Envie {ausente.Orientacao}, ou remova o aceite da lista."));
    }

    private static IEnumerable<(string Campo, string Descricao, string Orientacao)> CamposAusentesDoAceite(AceiteDeTermoInput aceite)
    {
        if (aceite.FormularioCodigo is null)
        {
            yield return ("formularioCodigo", "o código do formulário", "o código do formulário que exige o termo");
        }

        if (aceite.Codigo is null)
        {
            yield return ("codigo", "o código do termo", "o código do termo aceito");
        }

        if (aceite.HashVersao is null)
        {
            yield return ("hashVersao", "o hash da versão do termo", "o hash da versão que o formulário apresentou");
        }
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
