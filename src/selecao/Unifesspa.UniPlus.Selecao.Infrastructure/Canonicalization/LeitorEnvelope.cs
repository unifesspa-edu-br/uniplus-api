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
/// primeiro desvio para a leitura inteira.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que a gramática é fechada, e não tolerante.</b> Um leitor que ignora o que
/// não conhece degrada em silêncio — e silêncio, aqui, é configuração perdida. O caso
/// concreto: <c>bonusRegional</c> tem duas formas, <c>{"presente":false}</c> e a
/// completa. Um leitor que só olhasse <c>presente</c> leria
/// <c>{"presente":false,"fator":"1.2000",…}</c> como “sem bônus” e <b>descartaria o
/// bônus do certame</b> sem uma linha de log. Por isso: chave a mais é erro, chave a
/// menos é erro, e um objeto sem args tem de ser exatamente <c>{}</c>.
/// </para>
/// <para>
/// <b>Acumula o primeiro erro em vez de propagar <c>Result</c> a cada leitura.</b> A
/// alternativa — encadear dezenas de <c>Result</c> — produziria um decoder onde o
/// caminho de erro é mais longo que o de sucesso, e é no caminho de erro que os
/// buracos se escondem. O contrato é: o chamador verifica <see cref="Falhou"/> antes
/// de usar qualquer valor lido.
/// </para>
/// </remarks>
internal sealed class LeitorEnvelope
{
    private DomainError? _erro;

    public bool Falhou => _erro is not null;

    public DomainError Erro => _erro ?? throw new InvalidOperationException("Leitura sem erro registrado.");

    public Result<T> Falha<T>() => Result<T>.Failure(Erro);

    internal T Registrar<T>(string codigo, string mensagem)
    {
        _erro ??= new DomainError(codigo, mensagem);
        return default!;
    }

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

        HashSet<string> presentes = [.. objeto.Select(static p => p.Key)];

        foreach (string ausente in esperadas.Where(esperada => !presentes.Contains(esperada)))
        {
            Malformado<object>(path, $"a chave '{ausente}' está ausente.");
            return;
        }

        foreach (string intrusa in presentes.Where(presente => !esperadas.Contains(presente, StringComparer.Ordinal)))
        {
            Malformado<object>(path, $"a chave '{intrusa}' não pertence a esta versão do envelope.");
            return;
        }
    }

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
}
