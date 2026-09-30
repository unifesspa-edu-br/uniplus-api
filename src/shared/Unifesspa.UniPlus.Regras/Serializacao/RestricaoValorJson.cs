namespace Unifesspa.UniPlus.Regras.Serializacao;

using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A forma JSON de guardar as restrições de valor de um item: uma lista de objetos com
/// <c>tipo</c> e os campos do tipo — <c>minimo</c>/<c>maximo</c> nas faixas, <c>entradas</c>
/// (<c>{quando, valores}</c>) nas opções permitidas e <c>fatos</c> nas opções formadas pelas
/// respostas. A leitura remonta por <see cref="RestricoesDeValor"/>, então conteúdo gravado que
/// viola as invariantes é recusado.
/// </summary>
public static class RestricaoValorJson
{
    public const string FaixaNumerica = "FAIXA_NUMERICA";
    public const string TamanhoTexto = "TAMANHO_TEXTO";
    public const string OpcoesPermitidas = "OPCOES_PERMITIDAS";
    public const string OpcoesDasRespostas = "OPCOES_DAS_RESPOSTAS";

    public static string ParaToken(TipoRestricaoValor tipo) => tipo switch
    {
        TipoRestricaoValor.FaixaNumerica => FaixaNumerica,
        TipoRestricaoValor.TamanhoTexto => TamanhoTexto,
        TipoRestricaoValor.OpcoesPermitidas => OpcoesPermitidas,
        TipoRestricaoValor.OpcoesDasRespostas => OpcoesDasRespostas,
        TipoRestricaoValor.Nenhuma => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "O sentinela não tem token."),
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de restrição sem token."),
    };

    /// <summary>O tipo do token; token desconhecido é o sentinela.</summary>
    public static TipoRestricaoValor TipoDoToken(string? token) => token switch
    {
        FaixaNumerica => TipoRestricaoValor.FaixaNumerica,
        TamanhoTexto => TipoRestricaoValor.TamanhoTexto,
        OpcoesPermitidas => TipoRestricaoValor.OpcoesPermitidas,
        OpcoesDasRespostas => TipoRestricaoValor.OpcoesDasRespostas,
        _ => TipoRestricaoValor.Nenhuma,
    };

    public static JsonArray ParaJson(IEnumerable<RestricaoValor> restricoes)
    {
        ArgumentNullException.ThrowIfNull(restricoes);
        return new JsonArray([.. restricoes.Select(static r => (JsonNode)ParaJson(r))]);
    }

    public static JsonObject ParaJson(RestricaoValor restricao)
    {
        ArgumentNullException.ThrowIfNull(restricao);
        JsonObject json = new() { ["tipo"] = ParaToken(restricao.Tipo) };
        switch (restricao)
        {
            case Formularios.FaixaNumerica faixa:
                json["minimo"] = faixa.Minimo;
                json["maximo"] = faixa.Maximo;
                break;
            case Formularios.TamanhoTexto tamanho:
                json["minimo"] = tamanho.Minimo;
                json["maximo"] = tamanho.Maximo;
                break;
            case Formularios.OpcoesPermitidas opcoes:
                json["entradas"] = new JsonArray([.. opcoes.Entradas.Select(static e => (JsonNode)new JsonObject
                {
                    ["quando"] = e.Quando is { } quando ? PredicadoDnfJson.ParaJson(quando) : null,
                    ["valores"] = new JsonArray([.. e.Valores.Order(StringComparer.Ordinal).Select(static v => (JsonNode)v)]),
                })]);
                break;
            case Formularios.OpcoesDasRespostas respostas:
                json["fatos"] = new JsonArray([.. respostas.Fatos.Select(static f => (JsonNode)f)]);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(restricao), restricao.Tipo, "Tipo de restrição sem forma JSON.");
        }

        return json;
    }

    public static Result<IReadOnlyList<RestricaoValor>> ListaDeJson(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Array)
        {
            return Result<IReadOnlyList<RestricaoValor>>.Failure(Erro("As restrições de valor são uma lista."));
        }

        List<RestricaoValor> restricoes = [];
        foreach (JsonElement item in json.EnumerateArray())
        {
            Result<RestricaoValor> lida = DeJson(item);
            if (lida.IsFailure)
            {
                return Result<IReadOnlyList<RestricaoValor>>.Failure(lida.Error!);
            }

            restricoes.Add(lida.Value!);
        }

        return Result<IReadOnlyList<RestricaoValor>>.Success(restricoes);
    }

    public static Result<RestricaoValor> DeJson(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object
            || !json.TryGetProperty("tipo", out JsonElement tipo) || tipo.ValueKind != JsonValueKind.String)
        {
            return Falha("Cada restrição de valor tem um tipo.");
        }

        return TipoDoToken(tipo.GetString()) switch
        {
            TipoRestricaoValor.FaixaNumerica => LerLimites(json, out decimal? minimo, out decimal? maximo)
                ? RestricoesDeValor.Faixa(minimo, maximo)
                : Falha("A faixa numérica tem mínimo e máximo numéricos ou nulos."),
            TipoRestricaoValor.TamanhoTexto => LerLimites(json, out decimal? minimo, out decimal? maximo)
                    && Inteiro(minimo, out int? min) && Inteiro(maximo, out int? max)
                ? RestricoesDeValor.Tamanho(min, max)
                : Falha("O tamanho de texto tem mínimo e máximo inteiros ou nulos."),
            TipoRestricaoValor.OpcoesPermitidas => LerOpcoes(json),
            TipoRestricaoValor.OpcoesDasRespostas => Textos(json, "fatos") is { } fatos
                ? RestricoesDeValor.DasRespostas(fatos)
                : Falha("As opções formadas pelas respostas têm a lista de fatos."),
            _ => Falha("O tipo da restrição de valor não é reconhecido."),
        };
    }

    /// <summary>Um limite decimal que é inteiro, para os limites de tamanho de texto.</summary>
    public static bool Inteiro(decimal? valor, out int? inteiro)
    {
        inteiro = null;
        if (valor is not { } v)
        {
            return true;
        }

        if (!decimal.IsInteger(v) || v is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        inteiro = (int)v;
        return true;
    }

    private static Result<RestricaoValor> LerOpcoes(JsonElement json)
    {
        if (!json.TryGetProperty("entradas", out JsonElement entradas) || entradas.ValueKind != JsonValueKind.Array)
        {
            return Falha("As opções permitidas têm a lista de entradas.");
        }

        List<(PredicadoDnf? Quando, IReadOnlyCollection<string> Valores)> lidas = [];
        foreach (JsonElement entrada in entradas.EnumerateArray())
        {
            if (entrada.ValueKind != JsonValueKind.Object || Textos(entrada, "valores") is not { } valores)
            {
                return Falha("Cada entrada das opções permitidas tem a lista de valores.");
            }

            PredicadoDnf? quando = null;
            if (entrada.TryGetProperty("quando", out JsonElement predicado) && predicado.ValueKind != JsonValueKind.Null)
            {
                Result<PredicadoDnf> lido = PredicadoDnfJson.DeJson(predicado);
                if (lido.IsFailure)
                {
                    return Result<RestricaoValor>.Failure(lido.Error!);
                }

                quando = lido.Value;
            }

            lidas.Add((quando, valores));
        }

        return RestricoesDeValor.Opcoes(lidas);
    }

    private static bool LerLimites(JsonElement json, out decimal? minimo, out decimal? maximo) =>
        Limite(json, "minimo", out minimo) & Limite(json, "maximo", out maximo);

    private static bool Limite(JsonElement json, string chave, out decimal? valor)
    {
        valor = null;
        if (!json.TryGetProperty(chave, out JsonElement limite) || limite.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (limite.ValueKind != JsonValueKind.Number || !limite.TryGetDecimal(out decimal lido))
        {
            return false;
        }

        valor = lido;
        return true;
    }

    private static List<string>? Textos(JsonElement json, string chave)
    {
        if (!json.TryGetProperty(chave, out JsonElement lista) || lista.ValueKind != JsonValueKind.Array
            || lista.EnumerateArray().Any(static v => v.ValueKind != JsonValueKind.String))
        {
            return null;
        }

        return [.. lista.EnumerateArray().Select(static v => v.GetString()!)];
    }

    private static Result<RestricaoValor> Falha(string mensagem) => Result<RestricaoValor>.Failure(Erro(mensagem));

    private static DomainError Erro(string mensagem) => new(RestricaoValorErrorCodes.FormaJsonInvalida, mensagem);
}
