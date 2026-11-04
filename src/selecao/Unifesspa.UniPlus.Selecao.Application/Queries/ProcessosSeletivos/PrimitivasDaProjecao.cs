namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Primitivas de leitura do envelope congelado: cada uma confere presença, tipo e nulidade antes de entregar o valor, e recusa a forma inesperada em vez de emitir campo vazio.
/// </summary>
internal static class PrimitivasDaProjecao
{
    /// <summary>Lê um item de um arranjo do envelope, devolvendo <see langword="false"/> quando a forma não é a esperada.</summary>
    /// <typeparam name="T">O tipo do contrato público que o item projeta.</typeparam>
    /// <param name="item">O item do arranjo.</param>
    /// <param name="valor">O item projetado, quando a leitura deu certo.</param>
    /// <returns><see langword="true"/> quando o item tem a forma esperada.</returns>
    internal delegate bool TentadorDeItem<T>(JsonNode? item, [NotNullWhen(true)] out T? valor)
        where T : class;

    /// <summary>
    /// Lê todos os itens de um arranjo; um único item de forma inesperada recusa o arranjo inteiro.
    /// </summary>
    internal static bool TentarItens<T>(JsonArray array, TentadorDeItem<T> tentar, [NotNullWhen(true)] out List<T>? itens)
        where T : class
    {
        itens = null;
        List<T> lidos = [];
        foreach (JsonNode? item in array)
        {
            if (!tentar(item, out T? lido))
            {
                return false;
            }

            lidos.Add(lido);
        }

        itens = lidos;
        return true;
    }

    private static bool TentarIdentificadores(JsonObject objeto, string chave, [NotNullWhen(true)] out List<Guid>? valores)
    {
        valores = null;
        if (!TentarArray(objeto, chave, out JsonArray? array))
        {
            return false;
        }

        List<Guid> lidos = [];
        foreach (JsonNode? item in array)
        {
            if (item is not JsonValue valor
                || !valor.TryGetValue(out string? texto)
                || !Guid.TryParse(texto, CultureInfo.InvariantCulture, out Guid identificador))
            {
                return false;
            }

            lidos.Add(identificador);
        }

        valores = lidos;
        return true;
    }

    internal static bool TentarTextos(JsonObject objeto, string chave, [NotNullWhen(true)] out List<string>? valores)
    {
        valores = null;
        if (!TentarArray(objeto, chave, out JsonArray? array))
        {
            return false;
        }

        List<string> lidos = [];
        foreach (JsonNode? item in array)
        {
            if (item is not JsonValue valor || !valor.TryGetValue(out string? texto))
            {
                return false;
            }

            lidos.Add(texto);
        }

        valores = lidos;
        return true;
    }

    internal static bool TentarArray(JsonObject? objeto, string chave, [NotNullWhen(true)] out JsonArray? valor)
    {
        valor = null;
        if (objeto is null || !objeto.TryGetPropertyValue(chave, out JsonNode? node) || node is not JsonArray array)
        {
            return false;
        }

        valor = array;
        return true;
    }

    internal static bool TentarObjeto(JsonObject? objeto, string chave, [NotNullWhen(true)] out JsonObject? valor)
    {
        valor = null;
        if (objeto is null || !objeto.TryGetPropertyValue(chave, out JsonNode? node) || node is not JsonObject encontrado)
        {
            return false;
        }

        valor = encontrado;
        return true;
    }

    internal static bool TentarTexto(JsonObject? objeto, string chave, out string valor)
    {
        valor = "";
        return objeto is not null
            && objeto.TryGetPropertyValue(chave, out JsonNode? node)
            && node is JsonValue jv
            && jv.TryGetValue(out valor!);
    }

    /// <summary>
    /// Chave presente com <c>null</c> explícito ou com texto: sucesso. Chave ausente ou de outro
    /// tipo: falha — a ausência da chave é forma inesperada, não campo opcional vazio.
    /// </summary>
    internal static bool TentarTextoOpcional(JsonObject? objeto, string chave, out string? valor)
    {
        valor = null;
        if (objeto is null || !objeto.TryGetPropertyValue(chave, out JsonNode? node))
        {
            return false;
        }

        return node is null || (node is JsonValue jv && jv.TryGetValue(out valor));
    }

    internal static bool TentarBooleano(JsonObject? objeto, string chave, out bool valor)
    {
        valor = false;
        return objeto is not null
            && objeto.TryGetPropertyValue(chave, out JsonNode? node)
            && node is JsonValue jv
            && jv.TryGetValue(out valor);
    }

    internal static bool TentarInteiro(JsonObject? objeto, string chave, out int valor)
    {
        valor = 0;
        return objeto is not null
            && objeto.TryGetPropertyValue(chave, out JsonNode? node)
            && node is JsonValue jv
            && jv.TryGetValue(out valor);
    }

    internal static bool TentarInteiroOpcional(JsonObject? objeto, string chave, out int? valor)
    {
        valor = null;
        if (objeto is null || !objeto.TryGetPropertyValue(chave, out JsonNode? node))
        {
            return false;
        }

        if (node is null)
        {
            return true;
        }

        if (node is JsonValue jv && jv.TryGetValue(out int lido))
        {
            valor = lido;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Identificador na forma <b>exata</b> em que o envelope o congela: <c>D</c> minúsculo, e
    /// nunca o Guid vazio.
    /// </summary>
    /// <remarks>
    /// Mesma exigência do decodificador do envelope, pelo mesmo motivo do instante canônico:
    /// aceitar chaves, maiúsculas ou o vazio tornaria a projeção mais leniente do que o
    /// decodificador, e um identificador que o envelope recusa como malformado atravessaria a
    /// fronteira pública — no caso do vazio, como um documento de edital ou uma oferta que o
    /// consumidor tentaria buscar e não acharia.
    /// </remarks>
    internal static bool TentarIdentificador(JsonObject? objeto, string chave, out Guid valor)
    {
        valor = Guid.Empty;

        if (!TentarTexto(objeto, chave, out string texto)
            || !Guid.TryParseExact(texto, "D", out Guid lido)
            || !string.Equals(lido.ToString(), texto, StringComparison.Ordinal)
            || lido == Guid.Empty)
        {
            return false;
        }

        valor = lido;
        return true;
    }

    /// <summary>
    /// A forma exata em que o envelope congela um instante: RFC 3339, UTC, granularidade de
    /// segundo, sem fração.
    /// </summary>
    private const string FormatoInstanteCanonico = "yyyy-MM-ddTHH:mm:ssZ";

    /// <summary>Instante congelado, exigido na forma canônica — nunca em outra.</summary>
    /// <remarks>
    /// Exato, e assumindo UTC, pelos dois lados da mesma moeda. Um texto sem designador de fuso
    /// recebe, numa leitura permissiva, o fuso LOCAL do processo que lê: o mesmo envelope
    /// responderia instantes diferentes conforme a máquina que serve a requisição. E aceitar
    /// variações que o emissor nunca produz (fração de segundo, deslocamento explícito) tornaria
    /// a projeção mais leniente que o decodificador do envelope, que recusa qualquer forma fora
    /// da canônica — divergência que esconde adulteração em vez de expô-la.
    /// </remarks>
    internal static bool TentarInstante(JsonObject? objeto, string chave, out DateTimeOffset valor)
    {
        valor = default;
        return TentarTexto(objeto, chave, out string texto) && TentarInstanteCanonico(texto, out valor);
    }

    internal static bool TentarInstanteOpcional(JsonObject? objeto, string chave, out DateTimeOffset? valor)
    {
        valor = null;
        if (objeto is null || !objeto.TryGetPropertyValue(chave, out JsonNode? node))
        {
            return false;
        }

        if (node is null)
        {
            return true;
        }

        if (node is JsonValue jv
            && jv.TryGetValue(out string? texto)
            && TentarInstanteCanonico(texto, out DateTimeOffset lido))
        {
            valor = lido;
            return true;
        }

        return false;
    }

    private static bool TentarInstanteCanonico(string texto, out DateTimeOffset valor) =>
        DateTimeOffset.TryParseExact(
            texto,
            FormatoInstanteCanonico,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out valor);
}
