namespace Unifesspa.UniPlus.Regras.Serializacao;

using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A forma JSON de guardar as restrições de valor de um item: uma lista de objetos com
/// <c>tipo</c> e os campos do tipo — <c>minimo</c>/<c>maximo</c> nas faixas numéricas e de tamanho,
/// <c>dataMinima</c>/<c>dataMaxima</c> (<c>{referenciaDaData, data, anos, dias}</c>) na faixa de data,
/// <c>entradas</c> (<c>{quando, valores}</c>) nas opções permitidas e <c>codigosDosDados</c> nas opções formadas
/// pelas respostas. A leitura remonta por <see cref="RestricoesDeValor"/>, então conteúdo gravado que
/// viola as invariantes é recusado.
/// </summary>
public static class RestricaoValorJson
{
    public const string AncoraDataFixa = "DATA_FIXA";
    public const string AncoraEnvio = "ENVIO";

    /// <summary>A âncora do token; nula quando o token não é uma âncora de limite de data.</summary>
    public static AncoraDoLimiteDeData? AncoraDoToken(string? token) => token switch
    {
        AncoraDataFixa => AncoraDoLimiteDeData.DataFixa,
        AncoraEnvio => AncoraDoLimiteDeData.Envio,
        _ => null,
    };

    /// <summary>
    /// O limite da faixa de data na forma JSON: a âncora e, conforme ela, a data ou o deslocamento em
    /// anos e dias. O limite que falta é <c>null</c>.
    /// </summary>
    public static JsonObject? ParaJson(LimiteDeData? limite) => limite switch
    {
        null => null,
        { Ancora: AncoraDoLimiteDeData.DataFixa } => new JsonObject
        {
            ["referenciaDaData"] = AncoraDataFixa,
            ["data"] = DiaDoCalendario.Escrever(limite.Data!.Value),
        },
        _ => new JsonObject { ["referenciaDaData"] = AncoraEnvio, ["anos"] = limite.Anos, ["dias"] = limite.Dias },
    };

    public static JsonArray ParaJson(IEnumerable<RestricaoValor> restricoes)
    {
        ArgumentNullException.ThrowIfNull(restricoes);
        return new JsonArray([.. restricoes.Select(static r => (JsonNode)ParaJson(r))]);
    }

    public static JsonObject ParaJson(RestricaoValor restricao)
    {
        ArgumentNullException.ThrowIfNull(restricao);
        JsonObject json = new() { ["tipo"] = restricao.Tipo.Codigo() };
        PreencherFormaJson(json, restricao);

        return json;
    }

    private static void PreencherFormaJson(JsonObject json, RestricaoValor restricao)
    {
        switch (restricao)
        {
            case Formularios.FaixaNumerica faixa: PreencherFaixa(json, faixa); break;
            case Formularios.TamanhoTexto tamanho: PreencherTamanho(json, tamanho); break;
            case Formularios.OpcoesPermitidas opcoes: PreencherOpcoes(json, opcoes); break;
            case Formularios.OpcoesDasRespostas respostas: json["codigosDosDados"] = new JsonArray([.. respostas.Fatos.Select(static f => (JsonNode)f)]); break;
            case Formularios.MunicipiosDaUf daUf: json["codigosDosDados"] = new JsonArray(daUf.FatoUf); break;
            case Formularios.FaixaDeData faixa: PreencherFaixaDeData(json, faixa); break;
            default: throw new ArgumentOutOfRangeException(nameof(restricao), restricao.Tipo, "Tipo de restrição sem forma JSON.");
        }
    }

    private static void PreencherFaixa(JsonObject json, Formularios.FaixaNumerica faixa)
    {
        json["minimo"] = faixa.Minimo;
        json["maximo"] = faixa.Maximo;
    }

    private static void PreencherTamanho(JsonObject json, Formularios.TamanhoTexto tamanho)
    {
        json["minimo"] = tamanho.Minimo;
        json["maximo"] = tamanho.Maximo;
    }

    private static void PreencherOpcoes(JsonObject json, Formularios.OpcoesPermitidas opcoes) =>
        json["entradas"] = new JsonArray([.. opcoes.Entradas.Select(static e => (JsonNode)new JsonObject
        {
            ["quando"] = e.Quando is { } quando ? PredicadoDnfJson.ParaJson(quando) : null,
            ["valores"] = new JsonArray([.. e.Valores.Order(StringComparer.Ordinal).Select(static v => (JsonNode)v)]),
        })]);

    private static void PreencherFaixaDeData(JsonObject json, Formularios.FaixaDeData faixa)
    {
        json["dataMinima"] = ParaJson(faixa.Minima);
        json["dataMaxima"] = ParaJson(faixa.Maxima);
    }

    public static Result<IReadOnlyList<RestricaoValor>> ListaDeJson(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Array)
        {
            return Result<IReadOnlyList<RestricaoValor>>.Failure(Erro("As restrições de valor são uma lista."));
        }

        return LerListaDeRestricoes(json);
    }

    private static Result<IReadOnlyList<RestricaoValor>> LerListaDeRestricoes(JsonElement json)
    {
        List<RestricaoValor> restricoes = [];
        foreach (JsonElement item in json.EnumerateArray())
        {
            Result<RestricaoValor> lida = DeJson(item);
            if (lida.IsFailure)
            {
                return Result<IReadOnlyList<RestricaoValor>>.ValidationFailure(lida.Errors);
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

        return LerRestricaoTipada(json, RestricoesDeValor.TipoDeclarado(tipo.GetString()));
    }

    private static Result<RestricaoValor> LerRestricaoTipada(JsonElement json, TipoRestricaoValor? tipo) => tipo switch
    {
        TipoRestricaoValor.FaixaNumerica => LerFaixaNumerica(json),
        TipoRestricaoValor.TamanhoTexto => LerTamanhoTexto(json),
        TipoRestricaoValor.OpcoesPermitidas => LerOpcoes(json),
        TipoRestricaoValor.OpcoesDasRespostas => Textos(json, "codigosDosDados") is { } fatos
            ? RestricoesDeValor.DasRespostas(fatos)
            : Falha("As opções formadas pelas respostas têm a lista de fatos."),
        TipoRestricaoValor.MunicipiosDaUf => Textos(json, "codigosDosDados") is { } fatosDaUf
            ? RestricoesDeValor.DaUf(fatosDaUf)
            : Falha("Os municípios da UF têm a lista com o fato da UF."),
        TipoRestricaoValor.FaixaDeData => LerFaixaDeData(json),
        _ => Falha("O tipo da restrição de valor não é reconhecido."),
    };

    private static Result<RestricaoValor> LerFaixaNumerica(JsonElement json) =>
        LerLimites(json, out decimal? minimo, out decimal? maximo)
            ? RestricoesDeValor.Faixa(minimo, maximo)
            : Falha("A faixa numérica tem mínimo e máximo numéricos ou nulos.");

    private static Result<RestricaoValor> LerTamanhoTexto(JsonElement json) =>
        LerLimites(json, out decimal? minimo, out decimal? maximo)
            && Inteiro(minimo, out int? min) && Inteiro(maximo, out int? max)
            ? RestricoesDeValor.Tamanho(min, max)
            : Falha("O tamanho de texto tem mínimo e máximo inteiros ou nulos.");

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

        return LerEntradasDeOpcoes(entradas);
    }

    private static Result<RestricaoValor> LerEntradasDeOpcoes(JsonElement entradas)
    {
        List<(PredicadoDnf? Quando, IReadOnlyCollection<string> Valores)> lidas = [];
        foreach (JsonElement entrada in entradas.EnumerateArray())
        {
            Result<(PredicadoDnf? Quando, IReadOnlyCollection<string> Valores)> lida = LerEntradaDeOpcoes(entrada);
            if (lida.IsFailure)
            {
                return Result<RestricaoValor>.ValidationFailure(lida.Errors);
            }

            lidas.Add(lida.Value);
        }

        return RestricoesDeValor.Opcoes(lidas);
    }

    private static Result<(PredicadoDnf? Quando, IReadOnlyCollection<string> Valores)> LerEntradaDeOpcoes(JsonElement entrada)
    {
        if (entrada.ValueKind != JsonValueKind.Object || Textos(entrada, "valores") is not { } valores)
        {
            return Result<(PredicadoDnf?, IReadOnlyCollection<string>)>.Failure(Erro("Cada entrada das opções permitidas tem a lista de valores."));
        }

        Result<PredicadoDnf?> quando = LerPredicadoDaEntrada(entrada);
        return quando.IsFailure
            ? Result<(PredicadoDnf?, IReadOnlyCollection<string>)>.ValidationFailure(quando.Errors)
            : Result<(PredicadoDnf?, IReadOnlyCollection<string>)>.Success((quando.Value, valores));
    }

    private static Result<PredicadoDnf?> LerPredicadoDaEntrada(JsonElement entrada)
    {
        if (!entrada.TryGetProperty("quando", out JsonElement predicado) || predicado.ValueKind == JsonValueKind.Null)
        {
            return Result<PredicadoDnf?>.Success(null);
        }

        Result<PredicadoDnf> lido = PredicadoDnfJson.DeJson(predicado);
        return lido.IsFailure ? Result<PredicadoDnf?>.ValidationFailure(lido.Errors) : Result<PredicadoDnf?>.Success(lido.Value);
    }

    private static Result<RestricaoValor> LerFaixaDeData(JsonElement json) =>
        RestricoesDeValor.FaixaDeDataLida(LimiteDeJson(json, "dataMinima"), LimiteDeJson(json, "dataMaxima"));

    /// <summary>O limite da faixa de data guardado na chave; nulo quando a chave falta ou é nula.</summary>
    private static Result<LimiteDeData>? LimiteDeJson(JsonElement json, string chave)
    {
        if (!json.TryGetProperty(chave, out JsonElement limite) || limite.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return LerLimiteDeData(limite, chave);
    }

    private static Result<LimiteDeData> LerLimiteDeData(JsonElement limite, string chave)
    {
        if (limite.ValueKind != JsonValueKind.Object
            || !Texto(limite, "referenciaDaData", out string? ancora)
            || !Texto(limite, "data", out string? data)
            || !InteiroOpcional(limite, "anos", out int? anos)
            || !InteiroOpcional(limite, "dias", out int? dias))
        {
            return Result<LimiteDeData>.Failure(Erro($"O limite '{chave}' da faixa de data tem a âncora e a data ou o deslocamento."));
        }

        return ConstruirLimiteDeData(chave, ancora, data, anos, dias);
    }

    private static Result<LimiteDeData> ConstruirLimiteDeData(string chave, string? ancora, string? data, int? anos, int? dias)
    {
        DateOnly? dia = DiaDoCalendario.Ler(data);
        return data is not null && dia is null
            ? Result<LimiteDeData>.Failure(Erro($"A data do limite '{chave}' da faixa de data está em aaaa-mm-dd."))
            : RestricoesDeValor.Limite(ancora, dia, anos, dias);
    }

    private static bool Texto(JsonElement json, string chave, out string? texto)
    {
        texto = null;
        if (!json.TryGetProperty(chave, out JsonElement valor) || valor.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        texto = valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;
        return texto is not null;
    }

    private static bool InteiroOpcional(JsonElement json, string chave, out int? inteiro)
    {
        inteiro = null;
        if (!json.TryGetProperty(chave, out JsonElement valor) || valor.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (valor.ValueKind != JsonValueKind.Number || !valor.TryGetInt32(out int lido))
        {
            return false;
        }

        inteiro = lido;
        return true;
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
