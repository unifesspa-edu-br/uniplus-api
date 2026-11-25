namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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

        return LerObjetoDeFormatos(leitor, formatosJson, $"{pathPai}.formatosPermitidos");
    }

    private static FormatosPermitidos? LerObjetoDeFormatos(LeitorEnvelope leitor, JsonObject formatosJson, string path)
    {
        leitor.ExigirChaves(formatosJson, path, "qualquer", "lista");
        bool qualquer = leitor.Booleano(formatosJson, "qualquer", path);
        if (leitor.Falhou)
        {
            return null;
        }

        return LerListaDeFormatos(leitor, formatosJson, path, qualquer);
    }

    private static FormatosPermitidos? LerListaDeFormatos(LeitorEnvelope leitor, JsonObject formatosJson, string path, bool qualquer)
    {
        JsonNode? listaNode = formatosJson["lista"];
        if (listaNode is not null and not JsonArray)
        {
            return leitor.Recusar<FormatosPermitidos>($"'{path}.lista' deveria ser um array ou null.");
        }

        List<(string Formato, int? TamanhoMaximoBytesMax)>? entradas = listaNode is JsonArray lista
            ? LerEntradasDeFormato(leitor, lista, $"{path}.lista") : null;
        if (leitor.Falhou)
        {
            return null;
        }

        return CriarFormatosPermitidos(leitor, qualquer, entradas, path);
    }

    private static FormatosPermitidos? CriarFormatosPermitidos(LeitorEnvelope leitor, bool qualquer, List<(string Formato, int? TamanhoMaximoBytesMax)>? entradas, string caminho)
    {
        Result<FormatosPermitidos> resultado = FormatosPermitidos.Criar(qualquer, entradas);
        return resultado.IsFailure ? leitor.Propagar<FormatosPermitidos>(resultado.Errors, caminho) : resultado.Value;
    }

    private static List<(string Formato, int? TamanhoMaximoBytesMax)> LerEntradasDeFormato(LeitorEnvelope leitor, JsonArray lista, string path)
    {
        List<(string Formato, int? TamanhoMaximoBytesMax)> entradas = [];
        for (int i = 0; i < lista.Count; i++)
        {
            (string Formato, int? TamanhoMaximoBytesMax) entrada = LerEntradaDeFormato(leitor, lista, i, path);
            if (leitor.Falhou)
            {
                return entradas;
            }

            entradas.Add(entrada);
        }

        return entradas;
    }

    private static (string Formato, int? TamanhoMaximoBytesMax) LerEntradaDeFormato(LeitorEnvelope leitor, JsonArray lista, int indice, string path)
    {
        string itemPath = $"{path}[{indice}]";
        JsonObject entrada = leitor.ItemObjeto(lista, indice, path);
        leitor.ExigirChaves(entrada, itemPath, "formato", "tamanhoMaximoBytesMax");
        string formato = leitor.TextoNaoVazio(entrada, "formato", itemPath);
        int? tamanho = leitor.InteiroOpcional(entrada, "tamanhoMaximoBytesMax", itemPath);
        return (formato, tamanho);
    }

    /// <summary>Só <c>RESOLVIDO</c> é congelado — todo item lido aqui reidrata como tal.</summary>
    internal static IReadOnlyList<DocumentoExigidoBaseLegal> LerBasesLegais(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        JsonArray array = leitor.Array(item, "basesLegais", pathPai);
        if (leitor.Falhou)
        {
            return [];
        }

        return LerListaDeBasesLegais(leitor, array, pathPai);
    }

    private static List<DocumentoExigidoBaseLegal> LerListaDeBasesLegais(LeitorEnvelope leitor, JsonArray array, string pathPai)
    {
        List<DocumentoExigidoBaseLegal> basesLegais = [];
        for (int i = 0; i < array.Count; i++)
        {
            DocumentoExigidoBaseLegal? baseLegal = LerBaseLegalDoArray(leitor, array, i, pathPai);
            if (baseLegal is null)
            {
                return [];
            }

            basesLegais.Add(baseLegal);
        }

        return basesLegais;
    }

    private static DocumentoExigidoBaseLegal? LerBaseLegalDoArray(LeitorEnvelope leitor, JsonArray array, int indice, string pathPai) =>
        LerBaseLegal(leitor, leitor.ItemObjeto(array, indice, $"{pathPai}.basesLegais"), $"{pathPai}.basesLegais[{indice}]");

    private static DocumentoExigidoBaseLegal? LerBaseLegal(LeitorEnvelope leitor, JsonObject baseItem, string path)
    {
        if (LerBaseLegalResolvida(leitor, baseItem, path) is not { } lida)
        {
            return null;
        }

        Result<DocumentoExigidoBaseLegal> baseLegal = DocumentoExigidoBaseLegal.Criar(lida.Referencia, lida.Abrangencia, lida.Situacao, lida.Observacao);
        return baseLegal.IsFailure ? leitor.Propagar<DocumentoExigidoBaseLegal?>(baseLegal.Errors, path) : baseLegal.Value;
    }

    /// <summary>
    /// Lê uma base legal congelada, que só é materializada quando resolvida. A situação é conferida na
    /// fronteira de leitura, porque Reidratar não teria como saber disso sozinho.
    /// </summary>
    internal static BaseLegalLida? LerBaseLegalResolvida(LeitorEnvelope leitor, JsonObject baseItem, string path)
    {
        leitor.ExigirChaves(baseItem, path, "referencia", "abrangencia", "situacao", "observacao");

        string referencia = leitor.TextoNaoVazio(baseItem, "referencia", path, LimitesDoEnvelope.BaseLegal);
        string abrangenciaCodigo = leitor.TextoNaoVazio(baseItem, "abrangencia", path);
        string situacaoCodigo = leitor.TextoNaoVazio(baseItem, "situacao", path);
        string? observacao = leitor.TextoOpcional(baseItem, "observacao", path, LimitesDoEnvelope.ObservacaoBaseLegal);
        if (leitor.Falhou)
        {
            return null;
        }

        return ConferirBaseLegalResolvida(leitor, path, referencia, abrangenciaCodigo, situacaoCodigo, observacao);
    }

    private static BaseLegalLida? ConferirBaseLegalResolvida(LeitorEnvelope leitor, string path, string referencia,
        string abrangenciaCodigo, string situacaoCodigo, string? observacao)
    {
        if (!CatalogoDe<StatusBaseLegal>.TentarObter(situacaoCodigo, out StatusBaseLegal situacao) || situacao != StatusBaseLegal.Resolvido)
        {
            return leitor.Recusar<BaseLegalLida?>($"A situação em {path} deve ser resolvida.");
        }

        return CatalogoDe<TipoAbrangencia>.TentarObter(abrangenciaCodigo, out TipoAbrangencia abrangencia)
            ? new BaseLegalLida(referencia, abrangencia, situacao, observacao)
            : leitor.Recusar<BaseLegalLida?>($"A abrangência em {path} é desconhecida.");
    }

    /// <summary>
    /// A base legal congelada de uma exigência ou regra de comprovação, conferida como resolvida.
    /// </summary>
    /// <param name="Referencia">O texto de referência da norma.</param>
    /// <param name="Abrangencia">A abrangência da base legal.</param>
    /// <param name="Situacao">A situação da base legal, sempre resolvida.</param>
    /// <param name="Observacao">A observação opcional.</param>
    internal sealed record BaseLegalLida(string Referencia, TipoAbrangencia Abrangencia, StatusBaseLegal Situacao, string? Observacao);
}
