namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using LinhaDoPredicado = (int Clausula, string Fato, Unifesspa.UniPlus.Regras.Enums.Operador Operador, System.Text.Json.JsonElement Valor);

/// <summary>
/// Leitura dos formatos de arquivo permitidos de uma exigência e das bases legais congeladas.
/// </summary>
internal static class LeituraDosFormatosEBasesLegais
{
    /// <summary>
    /// <see cref="FormatosPermitidos"/> substitui o campo singular
    /// <c>formatoPermitido</c> — objeto SEMPRE presente (o VO é obrigatório em
    /// <see cref="DocumentoExigido"/>), com <c>lista</c> nula ⟺ <c>qualquer</c> verdadeiro.
    /// A validação de forma/coerência (formato reconhecido, sem duplicata, teto por formato
    /// positivo, QUALQUER exclusivo) é <see cref="Domain.ValueObjects.FormatosPermitidos.Criar"/> —
    /// mesma fronteira de responsabilidade que o restante deste decoder (congelamento dos parâmetros na publicação, UNI-REQ-0019: o congelado
    /// não revalida semântica, só forma).
    /// </summary>
    internal static FormatosPermitidos? LerFormatosPermitidos(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        JsonObject formatosJson = leitor.Objeto(item, "formatosPermitidos", pathPai);
        if (leitor.Falhou)
        {
            return null;
        }

        string path = $"{pathPai}.formatosPermitidos";
        leitor.ExigirChaves(formatosJson, path, "qualquer", "lista");

        bool qualquer = leitor.Booleano(formatosJson, "qualquer", path);
        if (leitor.Falhou)
        {
            return null;
        }

        JsonNode? listaNode = formatosJson["lista"];
        if (listaNode is not null and not JsonArray)
        {
            return leitor.Recusar<FormatosPermitidos>($"'{path}.lista' deveria ser um array ou null.");
        }

        List<(string Formato, int? TamanhoMaximoBytesMax)>? entradas = listaNode is JsonArray listaArray
            ? LerEntradasDeFormato(leitor, listaArray, $"{path}.lista")
            : null;
        if (leitor.Falhou)
        {
            return null;
        }

        Result<FormatosPermitidos> resultado = FormatosPermitidos.Criar(qualquer, entradas);
        return resultado.IsFailure ? leitor.Propagar<FormatosPermitidos>(resultado.Error!) : resultado.Value;
    }

    private static List<(string Formato, int? TamanhoMaximoBytesMax)> LerEntradasDeFormato(LeitorEnvelope leitor, JsonArray lista, string path)
    {
        List<(string Formato, int? TamanhoMaximoBytesMax)> entradas = [];
        for (int i = 0; i < lista.Count; i++)
        {
            string itemPath = $"{path}[{i}]";
            JsonObject entradaJson = leitor.ItemObjeto(lista, i, path);
            leitor.ExigirChaves(entradaJson, itemPath, "formato", "tamanhoMaximoBytesMax");

            string formatoCodigo = leitor.TextoNaoVazio(entradaJson, "formato", itemPath);
            int? tamanhoMaximoBytesMax = leitor.InteiroOpcional(entradaJson, "tamanhoMaximoBytesMax", itemPath);
            if (leitor.Falhou)
            {
                return entradas;
            }

            entradas.Add((formatoCodigo, tamanhoMaximoBytesMax));
        }

        return entradas;
    }

    /// <summary>Só <c>RESOLVIDO</c> é congelado — todo item lido aqui reidrata como tal.</summary>
    internal static IReadOnlyList<DocumentoExigidoBaseLegal> LerBasesLegais(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        JsonArray array = leitor.Array(item, "basesLegais", pathPai);
        if (leitor.Falhou)
        {
            return [];
        }

        List<DocumentoExigidoBaseLegal> basesLegais = [];
        for (int i = 0; i < array.Count; i++)
        {
            DocumentoExigidoBaseLegal? baseLegal = LerBaseLegal(
                leitor,
                leitor.ItemObjeto(array, i, $"{pathPai}.basesLegais"),
                $"{pathPai}.basesLegais[{i}]");
            if (baseLegal is null)
            {
                return [];
            }

            basesLegais.Add(baseLegal);
        }

        return basesLegais;
    }

    private static DocumentoExigidoBaseLegal? LerBaseLegal(LeitorEnvelope leitor, JsonObject baseItem, string path)
    {
        if (LerBaseLegalResolvida(leitor, baseItem, path) is not { } lida)
        {
            return null;
        }

        Result<DocumentoExigidoBaseLegal> baseLegal = DocumentoExigidoBaseLegal.Criar(lida.Referencia, lida.Abrangencia, lida.Status, lida.Observacao);
        return baseLegal.IsFailure ? leitor.Propagar<DocumentoExigidoBaseLegal?>(baseLegal.Error!) : baseLegal.Value;
    }

    /// <summary>
    /// Lê uma base legal congelada, que só é materializada quando resolvida. O status é conferido na
    /// fronteira de leitura, porque Reidratar não teria como saber disso sozinho.
    /// </summary>
    internal static BaseLegalLida? LerBaseLegalResolvida(LeitorEnvelope leitor, JsonObject baseItem, string path)
    {
        leitor.ExigirChaves(baseItem, path, "referencia", "abrangencia", "status", "observacao");

        string referencia = leitor.TextoNaoVazio(baseItem, "referencia", path, LimitesDoEnvelope.BaseLegal);
        string abrangenciaCodigo = leitor.TextoNaoVazio(baseItem, "abrangencia", path);
        string statusCodigo = leitor.TextoNaoVazio(baseItem, "status", path);
        string? observacao = leitor.TextoOpcional(baseItem, "observacao", path, LimitesDoEnvelope.ObservacaoBaseLegal);
        if (leitor.Falhou)
        {
            return null;
        }

        StatusBaseLegal status = StatusBaseLegalCodigo.FromCodigo(statusCodigo);
        return status == StatusBaseLegal.Resolvido
            ? new BaseLegalLida(referencia, TipoAbrangenciaCodigo.FromCodigo(abrangenciaCodigo), status, observacao)
            : leitor.Recusar<BaseLegalLida?>($"'{path}.status' deveria ser sempre RESOLVIDO — encontrado '{statusCodigo}'.");
    }

    /// <summary>
    /// A base legal congelada de uma exigência ou de um nó da árvore, já conferida como resolvida.
    /// </summary>
    /// <param name="Referencia">O texto de referência da norma.</param>
    /// <param name="Abrangencia">A abrangência da base legal.</param>
    /// <param name="Status">O status da base legal, sempre resolvido.</param>
    /// <param name="Observacao">A observação opcional.</param>
    internal sealed record BaseLegalLida(string Referencia, TipoAbrangencia Abrangencia, StatusBaseLegal Status, string? Observacao);
}
