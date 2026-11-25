namespace Unifesspa.UniPlus.Regras.Serializacao;

using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A forma JSON de guardar um predicado e uma obrigatoriedade: o predicado é a lista de cláusulas,
/// cada uma a lista de condições <c>{codigoDoDado, operador, valor}</c>; a obrigatoriedade é
/// <c>{tipo, predicado}</c>, com o predicado só em <c>QUANDO</c>. A leitura remonta pelas mesmas
/// factories do domínio, então conteúdo gravado que as viola é recusado, nunca aceito em silêncio.
/// </summary>
public static class PredicadoDnfJson
{
    public const string FormaInvalida = "PredicadoDnf.FormaJsonInvalida";
    private const string ChaveFato = "fato";
    private const string ChaveCodigoDoDado = "codigoDoDado";

    public static JsonArray ParaJson(PredicadoDnf predicado)
    {
        ArgumentNullException.ThrowIfNull(predicado);
        return new JsonArray([.. predicado.Clausulas.Select(static c => (JsonNode)new JsonArray([.. c.Condicoes.Select(static d => (JsonNode)new JsonObject
        {
            [ChaveCodigoDoDado] = d.Fato,
            ["operador"] = d.Operador.Codigo(),
            ["valor"] = JsonNode.Parse(d.Valor.GetRawText()),
        })]))]);
    }

    public static Result<PredicadoDnf> DeJson(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Array)
        {
            return Falha("O predicado é uma lista de cláusulas.");
        }

        return LerClausulas(json);
    }

    private static Result<PredicadoDnf> LerClausulas(JsonElement json)
    {
        List<(int Clausula, CondicaoDnf Condicao)> linhas = [];
        int c = 0;
        foreach (JsonElement clausula in json.EnumerateArray())
        {
            Result lida = LerClausula(clausula, c++, linhas);
            if (lida.IsFailure)
            {
                return Result<PredicadoDnf>.ValidationFailure(lida.Errors);
            }
        }

        return PredicadoDnf.CriarDeCondicoesAgrupadas(linhas);
    }

    private static Result LerClausula(JsonElement clausula, int c, List<(int Clausula, CondicaoDnf Condicao)> linhas)
    {
        if (clausula.ValueKind != JsonValueKind.Array || clausula.GetArrayLength() == 0)
        {
            return Result.ValidationFailure([new FieldError($"[{c}]", Erro("Cada cláusula é uma lista não vazia de condições."))]);
        }

        return LerCondicoes(clausula, c, linhas);
    }

    private static Result LerCondicoes(JsonElement clausula, int c, List<(int Clausula, CondicaoDnf Condicao)> linhas)
    {
        int i = 0;
        foreach (JsonElement condicao in clausula.EnumerateArray())
        {
            Result<CondicaoDnf> criada = LerCondicao(condicao);
            if (criada.IsFailure)
            {
                return Result.ValidationFailure([.. criada.Errors.Select(erro => erro.NoCaminhoDe($"[{c}][{i}]"))]);
            }

            linhas.Add((c, criada.Value!));
            i++;
        }

        return Result.Success();
    }

    private static Result<CondicaoDnf> LerCondicao(JsonElement condicao)
    {
        if (condicao.ValueKind != JsonValueKind.Object)
        {
            return CondicaoInvalida("");
        }

        return LerCondicaoObjeto(condicao);
    }

    private static Result<CondicaoDnf> LerCondicaoObjeto(JsonElement condicao)
    {
        if (condicao.TryGetProperty(ChaveFato, out _))
        {
            return Result<CondicaoDnf>.ValidationFailure([new FieldError(ChaveFato, Erro("chave 'fato' não é aceita; use 'codigoDoDado'"))]);
        }

        string? campo = CampoAusente(condicao);
        if (campo is not null)
        {
            return CondicaoInvalida(campo);
        }

        return CriarCondicao(condicao);
    }

    private static Result<CondicaoDnf> CriarCondicao(JsonElement condicao)
    {
        Result<CondicaoDnf> criada = CondicaoDnf.CriarInformada(
            condicao.GetProperty(ChaveCodigoDoDado).GetString(), condicao.GetProperty("operador").GetString(), condicao.GetProperty("valor"));
        return criada.IsSuccess
            ? criada
            : Result<CondicaoDnf>.ValidationFailure([.. criada.Errors.Select(static erro =>
                erro.Field == CondicaoDnf.CampoFato ? erro with { Field = ChaveCodigoDoDado } : erro)]);
    }

    private static string? CampoAusente(JsonElement condicao)
    {
        if (!condicao.TryGetProperty(ChaveCodigoDoDado, out JsonElement codigoDoDado) || codigoDoDado.ValueKind != JsonValueKind.String)
        {
            return ChaveCodigoDoDado;
        }

        if (!condicao.TryGetProperty("operador", out JsonElement operador) || operador.ValueKind != JsonValueKind.String)
        {
            return "operador";
        }

        return condicao.TryGetProperty("valor", out _) ? null : "valor";
    }

    private static Result<CondicaoDnf> CondicaoInvalida(string campo) =>
        Result<CondicaoDnf>.ValidationFailure([new FieldError(campo, Erro("Cada condição tem codigoDoDado, operador e valor."))]);

    public static JsonObject ParaJson(Obrigatoriedade obrigatoriedade)
    {
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        return new JsonObject
        {
            ["tipo"] = obrigatoriedade.Tipo.Codigo(),
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

        return LerObrigatoriedade(tipo.GetString(), json);
    }

    private static Result<Obrigatoriedade> LerObrigatoriedade(string? codigo, JsonElement json)
    {
        json.TryGetProperty("predicado", out JsonElement predicado);
        bool temPredicado = predicado.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null);
        bool conhecido = CatalogoDe<TipoObrigatoriedade>.TentarObter(codigo, out TipoObrigatoriedade tipo);
        if (conhecido && tipo == TipoObrigatoriedade.Quando && temPredicado)
        {
            return ObrigatoriedadeCondicional(predicado);
        }

        Obrigatoriedade? forma = conhecido && !temPredicado ? Obrigatoriedade.DaForma(tipo, null) : null;
        return forma is null
            ? Result<Obrigatoriedade>.Failure(Erro("Só a obrigatoriedade QUANDO tem predicado, e ela sempre o tem."))
            : Result<Obrigatoriedade>.Success(forma);
    }

    private static Result<Obrigatoriedade> ObrigatoriedadeCondicional(JsonElement predicado)
    {
        Result<PredicadoDnf> lido = DeJson(predicado);
        return lido.IsSuccess
            ? Result<Obrigatoriedade>.Success(Obrigatoriedade.DaForma(TipoObrigatoriedade.Quando, lido.Value)!)
            : Result<Obrigatoriedade>.Failure(lido.Error!);
    }

    /// <summary>O impedimento: a condição e a mensagem ao candidato.</summary>
    public static JsonObject ParaJson(Impedimento impedimento)
    {
        ArgumentNullException.ThrowIfNull(impedimento);
        return new JsonObject
        {
            ["quando"] = ParaJson(impedimento.Quando),
            ["mensagem"] = impedimento.Mensagem,
        };
    }

    /// <summary>O impedimento do JSON; nulo quando o campo não tem.</summary>
    public static Result<Impedimento?> ImpedimentoDeJson(JsonElement json)
    {
        if (json.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return Result<Impedimento?>.Success(null);
        }

        if (json.ValueKind != JsonValueKind.Object
            || !json.TryGetProperty("quando", out JsonElement quando)
            || !json.TryGetProperty("mensagem", out JsonElement mensagem) || mensagem.ValueKind != JsonValueKind.String)
        {
            return Result<Impedimento?>.Failure(Erro("O impedimento tem a condição e a mensagem."));
        }

        return LerImpedimento(quando, mensagem.GetString()!);
    }

    private static Result<Impedimento?> LerImpedimento(JsonElement quando, string mensagem)
    {
        Result<PredicadoDnf> lido = DeJson(quando);
        return lido.IsSuccess
            ? Result<Impedimento?>.Success(new Impedimento(lido.Value!, mensagem))
            : Result<Impedimento?>.Failure(lido.Error!);
    }

    private static Result<PredicadoDnf> Falha(string mensagem, string campo = "") =>
        Result<PredicadoDnf>.ValidationFailure([new FieldError(campo, Erro(mensagem))]);

    private static DomainError Erro(string mensagem) => new(FormaInvalida, mensagem);
}
