namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura <b>fechada</b> de um envelope canônico: cada objeto declara o conjunto
/// exato das suas chaves, cada valor é lido na forma exata em que foi escrito, e o
/// primeiro desvio para a leitura inteira; uma factory pode devolver várias causas desse desvio.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que a gramática é fechada, e não tolerante.</b> Um leitor que ignora o que
/// não conhece degrada em silêncio — e silêncio, aqui, é configuração perdida. O caso
/// concreto: <c>fada</c> tem duas formas, <c>{"presente":false}</c> e a
/// completa. Um leitor que só olhasse <c>presente</c> leria
/// <c>{"presente":false,"fator":"1.2000",…}</c> como “sem bônus” e <b>descartaria o
/// bônus do certame</b> sem uma linha de log. Por isso: chave a mais é erro, chave a
/// menos é erro, e um objeto sem parâmetros tem de ser exatamente <c>{}</c>.
/// </para>
/// <para>
/// <b>Registra a primeira falha em vez de propagar <c>Result</c> a cada leitura.</b> A
/// alternativa — encadear dezenas de <c>Result</c> — produziria um decoder onde o
/// caminho de erro é mais longo que o de sucesso, e é no caminho de erro que os
/// buracos se escondem. O contrato é: o chamador verifica <see cref="Falhou"/> antes
/// de usar qualquer valor lido.
/// </para>
/// </remarks>
internal sealed class LeitorEnvelope
{
    private DomainError? _erro;
    private IReadOnlyList<FieldError>? _erros;

    public bool Falhou => _erro is not null;

    public DomainError Erro => _erro ?? throw new InvalidOperationException("Leitura sem erro registrado.");

    public Result<T> Falha<T>() => _erros is null ? Result<T>.Failure(Erro) : Result<T>.ValidationFailure(_erros);

    internal T Registrar<T>(string codigo, string mensagem)
    {
        _erro ??= new DomainError(codigo, mensagem);
        return default!;
    }

    /// <summary>
    /// Recusa o envelope com a mensagem como está, no código de envelope malformado.
    /// </summary>
    public T Recusar<T>(string mensagem) => Registrar<T>(ErrosCodecEnvelope.EnvelopeMalformado, mensagem);

    internal T Malformado<T>(string path, string detalhe) =>
        Registrar<T>(ErrosCodecEnvelope.EnvelopeMalformado, $"Envelope malformado em '{path}': {detalhe}");

    /// <summary>
    /// Exige que <paramref name="objeto"/> tenha <b>exatamente</b> as chaves esperadas —
    /// nem mais, nem menos. É esta função que fecha a gramática em cada nível.
    /// </summary>
    public void ExigirChaves(JsonObject objeto, string path, params string[] esperadas)
    {
        if (Falhou)
        {
            return;
        }

        ConferirChaves(objeto, path, esperadas);
    }

    private void ConferirChaves(JsonObject objeto, string path, string[] esperadas)
    {
        string? problema = ProblemaDeChaves(objeto, esperadas);
        if (problema is not null)
        {
            Malformado<object>(path, problema);
            return;
        }

        ConferirContextoSePresente(objeto, path);
    }

    private static string? ProblemaDeChaves(JsonObject objeto, string[] esperadas)
    {
        HashSet<string> presentes = [.. objeto.Select(static p => p.Key)];
        string? ausente = esperadas.FirstOrDefault(esperada => !presentes.Contains(esperada));
        string? intrusa = presentes.FirstOrDefault(presente => presente != "contexto" && !esperadas.Contains(presente, StringComparer.Ordinal));
        if (ausente is not null)
        {
            return $"a chave '{ausente}' está ausente.";
        }

        return intrusa is null ? null : $"a chave '{intrusa}' não pertence a esta versão do envelope.";
    }

    private void ConferirContextoSePresente(JsonObject objeto, string path)
    {
        if (objeto.ContainsKey("contexto"))
        {
            ConferirContexto(objeto, path);
        }
    }

    private void ConferirContexto(JsonObject dono, string path)
    {
        if (dono["contexto"] is not JsonObject contexto || contexto.Any(propriedade => !RotuloValido(dono, propriedade)))
        {
            Malformado<object>($"{path}.contexto", "esperado um objeto de rótulos textuais.");
        }
    }

    private static bool RotuloValido(JsonObject dono, KeyValuePair<string, JsonNode?> rotulo) =>
        rotulo.Key.EndsWith("Rotulos", StringComparison.Ordinal)
            ? ListaDeRotulosValida(dono, rotulo)
            : RotuloUnicoValido(dono, rotulo);

    private static bool RotuloUnicoValido(JsonObject dono, KeyValuePair<string, JsonNode?> rotulo) =>
        rotulo.Key.EndsWith("Rotulo", StringComparison.Ordinal)
        && dono.ContainsKey(rotulo.Key[..^"Rotulo".Length])
        && rotulo.Value is JsonValue valor && valor.TryGetValue<string>(out _);

    private static bool ListaDeRotulosValida(JsonObject dono, KeyValuePair<string, JsonNode?> rotulos) =>
        dono[rotulos.Key[..^"Rotulos".Length]] is JsonArray codigos
        && rotulos.Value is JsonArray textos
        && textos.Count == codigos.Count
        && textos.All(static texto => texto is JsonValue valor && valor.TryGetValue<string>(out _));

    public JsonObject Objeto(JsonObject pai, string chave, string path)
    {
        if (Falhou)
        {
            return default!;
        }

        return pai[chave] is JsonObject objeto
            ? objeto
            : Malformado<JsonObject>($"{path}.{chave}", "esperado um objeto.");
    }

    /// <summary>
    /// Lê um bloco que declara <c>presente</c>: devolve o objeto quando presente e nulo quando ausente,
    /// caso em que a única chave admitida é <c>presente</c>.
    /// </summary>
    public JsonObject? ObjetoSePresente(JsonObject pai, string chave)
    {
        JsonObject bloco = Objeto(pai, chave, "$");
        if (Falhou)
        {
            return null;
        }

        return ObjetoPresente(bloco, chave);
    }

    private JsonObject? ObjetoPresente(JsonObject bloco, string chave)
    {
        bool presente = this.Booleano(bloco, "presente", chave);
        if (Falhou)
        {
            return null;
        }

        if (presente)
        {
            return bloco;
        }

        ExigirChaves(bloco, chave, "presente");
        return null;
    }

    public JsonObject? ObjetoOpcional(JsonObject pai, string chave, string path)
    {
        if (Falhou)
        {
            return null;
        }

        JsonNode? node = pai[chave];
        return node switch
        {
            null => null,
            JsonObject objeto => objeto,
            _ => Malformado<JsonObject?>($"{path}.{chave}", "esperado um objeto ou null."),
        };
    }

    public JsonArray Array(JsonObject pai, string chave, string path)
    {
        if (Falhou)
        {
            return default!;
        }

        return pai[chave] is JsonArray array
            ? array
            : Malformado<JsonArray>($"{path}.{chave}", "esperado um array.");
    }

    public JsonArray? ArrayOpcional(JsonObject pai, string chave, string path)
    {
        if (Falhou)
        {
            return null;
        }

        return pai[chave] switch
        {
            null => null,
            JsonArray array => array,
            _ => Malformado<JsonArray?>($"{path}.{chave}", "esperado um array ou null."),
        };
    }

    public JsonObject ItemObjeto(JsonArray array, int indice, string path)
    {
        if (Falhou)
        {
            return default!;
        }

        return array[indice] is JsonObject objeto
            ? objeto
            : Malformado<JsonObject>($"{path}[{indice}]", "esperado um objeto.");
    }

    /// <summary>
    /// Propaga o <see cref="DomainError"/> de uma factory do domínio como está — as
    /// invariantes do agregado já <b>são</b> a validação; reescrevê-las aqui criaria uma
    /// segunda fonte de verdade que envelheceria em silêncio.
    /// </summary>
    public T Propagar<T>(DomainError erro)
    {
        _erro ??= erro;
        return default!;
    }

    public T Propagar<T>(IReadOnlyList<FieldError> erros)
    {
        if (!Falhou)
        {
            _erros = erros;
            _erro = erros[0].Error;
        }

        return default!;
    }
}
