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

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Primitivas de leitura do envelope congelado: cada uma confere presença, tipo e nulidade antes de entregar o valor, e recusa a forma inesperada em vez de emitir campo vazio.
/// </summary>
internal static class PrimitivasDaProjecao
{
    /// <summary>Lê um item de um arranjo do envelope, devolvendo <see langword="false"/> quando a forma não é a esperada.</summary>
    internal delegate bool LeituraDeItem<T>(JsonNode? item, [NotNullWhen(true)] out T? valor)
        where T : class;

    /// <summary>
    /// Lê todos os itens de um arranjo; um único item de forma inesperada recusa o arranjo inteiro.
    /// </summary>
    internal static bool TentarItens<T>(JsonArray array, LeituraDeItem<T> tentar, [NotNullWhen(true)] out List<T>? itens)
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

    internal static bool TentarTextos(JsonObject objeto, string chave, [NotNullWhen(true)] out List<string>? valores)
    {
        valores = null;
        return TentarArray(objeto, chave, out JsonArray? array) && TentarItens<string>(array, TentarTextoDoItem, out valores);
    }

    private static bool TentarTextoDoItem(JsonNode? item, [NotNullWhen(true)] out string? texto)
    {
        texto = null;
        return item is JsonValue valor && valor.TryGetValue(out texto);
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

        if (node is JsonValue jv && jv.TryGetValue(out int lido))
        {
            valor = lido;
            return true;
        }

        return node is null;
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

    /// <summary>Chave presente com <c>null</c> explícito ou com identificador na forma exata do envelope; ausente ou fora da forma, falha.</summary>
    internal static bool TentarIdentificadorOpcional(JsonObject? objeto, string chave, out Guid? valor)
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

        bool lido = TentarIdentificador(objeto, chave, out Guid identificador);
        valor = lido ? identificador : null;
        return lido;
    }

    /// <summary>Chave presente com <c>null</c> explícito ou com data <c>yyyy-MM-dd</c>; ausente ou fora da forma, falha.</summary>
    internal static bool TentarDataOpcional(JsonObject? objeto, string chave, out DateOnly? valor)
    {
        valor = null;
        if (!TentarTextoOpcional(objeto, chave, out string? texto))
        {
            return false;
        }

        if (texto is null)
        {
            return true;
        }

        bool lida = DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly data);
        valor = lida ? data : null;
        return lida;
    }

    /// <summary>Código de contrato de um enum, na forma exata que o envelope grava; texto fora do catálogo falha.</summary>
    internal static bool TentarCodigo<TEnum>(JsonObject? objeto, string chave, out TEnum valor)
        where TEnum : struct, Enum
    {
        valor = default;
        return TentarTexto(objeto, chave, out string codigo) && CatalogoDe<TEnum>.TentarObter(codigo, out valor);
    }

    /// <summary>Chave presente com <c>null</c> explícito ou com código de contrato; ausente ou fora do catálogo, falha.</summary>
    internal static bool TentarCodigoOpcional<TEnum>(JsonObject? objeto, string chave, out TEnum? valor)
        where TEnum : struct, Enum
    {
        valor = null;
        if (!TentarTextoOpcional(objeto, chave, out string? codigo))
        {
            return false;
        }

        if (codigo is null)
        {
            return true;
        }

        bool lido = CatalogoDe<TEnum>.TentarObter(codigo, out TEnum encontrado);
        valor = lido ? encontrado : null;
        return lido;
    }

    /// <summary>Instante congelado, exigido na forma canônica do envelope: a projeção não é mais leniente que o decodificador.</summary>
    internal static bool TentarInstante(JsonObject? objeto, string chave, out DateTimeOffset valor)
    {
        valor = default;
        return TentarTexto(objeto, chave, out string texto) && HashCanonicalComputer.TryParseInstantCanonical(texto, out valor);
    }

    internal static bool TentarInstanteOpcional(JsonObject? objeto, string chave, out DateTimeOffset? valor)
    {
        valor = null;
        bool lido = TentarTextoOpcional(objeto, chave, out string? texto);
        if (lido && texto is not null)
        {
            lido = HashCanonicalComputer.TryParseInstantCanonical(texto, out DateTimeOffset instante);
            valor = lido ? instante : null;
        }

        return lido;
    }
}
