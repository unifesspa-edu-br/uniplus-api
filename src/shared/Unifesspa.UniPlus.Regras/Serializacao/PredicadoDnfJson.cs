namespace Unifesspa.UniPlus.Regras.Serializacao;

using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A forma JSON de guardar um predicado e uma obrigatoriedade: o predicado é a lista de cláusulas,
/// cada uma a lista de condições <c>{fato, operador, valor}</c>; a obrigatoriedade é
/// <c>{tipo, predicado}</c>, com o predicado só em <c>QUANDO</c>. A leitura remonta pelas mesmas
/// factories do domínio, então conteúdo gravado que as viola é recusado, nunca aceito em silêncio.
/// </summary>
public static class PredicadoDnfJson
{
    public const string ObrigatoriedadeSempre = "SEMPRE";
    public const string ObrigatoriedadeNunca = "NUNCA";
    public const string ObrigatoriedadeQuando = "QUANDO";

    public const string FormaInvalida = "PredicadoDnf.FormaJsonInvalida";

    public static JsonArray ParaJson(PredicadoDnf predicado)
    {
        ArgumentNullException.ThrowIfNull(predicado);
        return new JsonArray([.. predicado.Clausulas.Select(static c => (JsonNode)new JsonArray([.. c.Condicoes.Select(static d => (JsonNode)new JsonObject
        {
            ["fato"] = d.Fato,
            ["operador"] = d.Operador.ToCodigo(),
            ["valor"] = JsonNode.Parse(d.Valor.GetRawText()),
        })]))]);
    }

    public static Result<PredicadoDnf> DeJson(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Array)
        {
            return Falha("O predicado é uma lista de cláusulas.");
        }

        List<(int Clausula, CondicaoDnf Condicao)> linhas = [];
        int indice = 0;
        foreach (JsonElement clausula in json.EnumerateArray())
        {
            if (clausula.ValueKind != JsonValueKind.Array || clausula.GetArrayLength() == 0)
            {
                return Falha("Cada cláusula é uma lista não vazia de condições.");
            }

            foreach (JsonElement condicao in clausula.EnumerateArray())
            {
                if (condicao.ValueKind != JsonValueKind.Object
                    || !condicao.TryGetProperty("fato", out JsonElement fato) || fato.ValueKind != JsonValueKind.String
                    || !condicao.TryGetProperty("operador", out JsonElement operador) || operador.ValueKind != JsonValueKind.String
                    || !condicao.TryGetProperty("valor", out JsonElement valor))
                {
                    return Falha("Cada condição tem fato, operador e valor.");
                }

                Result<CondicaoDnf> criada = CondicaoDnf.Criar(fato.GetString()!, OperadorCodigo.FromCodigo(operador.GetString()), valor);
                if (criada.IsFailure)
                {
                    return Result<PredicadoDnf>.Failure(criada.Error!);
                }

                linhas.Add((indice, criada.Value!));
            }

            indice++;
        }

        return PredicadoDnf.CriarDeCondicoesAgrupadas(linhas);
    }

    public static JsonObject ParaJson(Obrigatoriedade obrigatoriedade)
    {
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        return new JsonObject
        {
            ["tipo"] = ParaToken(obrigatoriedade.Tipo),
            ["predicado"] = obrigatoriedade.Predicado is { } predicado ? ParaJson(predicado) : null,
        };
    }

    public static Result<Obrigatoriedade> ObrigatoriedadeDeJson(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object
            || !json.TryGetProperty("tipo", out JsonElement tipo) || tipo.ValueKind != JsonValueKind.String)
        {
            return Result<Obrigatoriedade>.Failure(Erro("A obrigatoriedade tem tipo SEMPRE, NUNCA ou QUANDO."));
        }

        json.TryGetProperty("predicado", out JsonElement predicado);
        bool temPredicado = predicado.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null);
        string? token = tipo.GetString();
        if (token == ObrigatoriedadeQuando && temPredicado)
        {
            Result<PredicadoDnf> lido = DeJson(predicado);
            return lido.IsSuccess
                ? Result<Obrigatoriedade>.Success(Obrigatoriedade.Quando(lido.Value!))
                : Result<Obrigatoriedade>.Failure(lido.Error!);
        }

        return (token, temPredicado) switch
        {
            (ObrigatoriedadeSempre, false) => Result<Obrigatoriedade>.Success(Obrigatoriedade.Sempre),
            (ObrigatoriedadeNunca, false) => Result<Obrigatoriedade>.Success(Obrigatoriedade.Nunca),
            _ => Result<Obrigatoriedade>.Failure(Erro("Só a obrigatoriedade QUANDO tem predicado, e ela sempre o tem.")),
        };
    }

    /// <summary>O token do tipo de obrigatoriedade; o sentinela não tem token.</summary>
    public static string ParaToken(TipoObrigatoriedade tipo) => tipo switch
    {
        TipoObrigatoriedade.Sempre => ObrigatoriedadeSempre,
        TipoObrigatoriedade.Nunca => ObrigatoriedadeNunca,
        TipoObrigatoriedade.Quando => ObrigatoriedadeQuando,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de obrigatoriedade sem token."),
    };

    /// <summary>O tipo de obrigatoriedade do token; token desconhecido é o sentinela.</summary>
    public static TipoObrigatoriedade TipoDoToken(string? token) => token switch
    {
        ObrigatoriedadeSempre => TipoObrigatoriedade.Sempre,
        ObrigatoriedadeNunca => TipoObrigatoriedade.Nunca,
        ObrigatoriedadeQuando => TipoObrigatoriedade.Quando,
        _ => TipoObrigatoriedade.Nenhuma,
    };

    private static Result<PredicadoDnf> Falha(string mensagem) => Result<PredicadoDnf>.Failure(Erro(mensagem));

    private static DomainError Erro(string mensagem) => new(FormaInvalida, mensagem);
}
