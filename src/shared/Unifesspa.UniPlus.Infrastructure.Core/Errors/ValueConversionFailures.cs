namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using Microsoft.AspNetCore.Http;

/// <summary>
/// As chaves cujo valor <b>não virou o tipo declarado</b>, anotadas no instante em que isso
/// acontece.
/// </summary>
/// <remarks>
/// <para>
/// Depois que o <c>ModelStateDictionary</c> normaliza a exceção do binder numa mensagem segura,
/// uma conversão que falhou e uma validação que reprovou ficam idênticas: mesma chave, mesmo
/// valor tentado, exceção descartada, mesmo estado de validação. O que resta para separá-las é
/// o texto da mensagem — que é do framework, muda quando ele quiser e varia com a cultura.
/// </para>
/// <para>
/// O que as distingue de verdade não sobrevive ao dicionário, mas existe: é <b>quando</b> o erro
/// entra. Conversão falha <i>durante</i> o binding, dentro do binder do parâmetro; validação de
/// atributo reprova <i>depois</i>, quando o binding inteiro já terminou e o validador percorre o
/// modelo. Anotar a chave no momento do binding preserva essa diferença em vez de tentar
/// reconstruí-la depois, quando ela já foi perdida.
/// </para>
/// <para>
/// Mesmo mecanismo que o binder de cursor de paginação usa para se identificar ao factory de
/// erro: o binder publica em <c>HttpContext.Items</c>, e quem monta a resposta lê dali.
/// </para>
/// </remarks>
public static class ValueConversionFailures
{
    /// <summary>Chave em <c>HttpContext.Items</c> onde as chaves anotadas ficam.</summary>
    public const string HttpContextItemKey = "__UniPlusValueConversionFailures";

    /// <summary>Anota que <paramref name="key"/> não converteu.</summary>
    public static void Record(HttpContext httpContext, string key)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        if (httpContext.Items.TryGetValue(HttpContextItemKey, out object? existente)
            && existente is HashSet<string> anotadas)
        {
            anotadas.Add(key);
            return;
        }

        httpContext.Items[HttpContextItemKey] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { key };
    }

    /// <summary>As chaves anotadas nesta requisição — vazio quando nenhuma conversão falhou.</summary>
    public static IReadOnlySet<string> Of(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return httpContext.Items.TryGetValue(HttpContextItemKey, out object? anotadas)
            && anotadas is HashSet<string> chaves
            ? chaves
            : System.Collections.Frozen.FrozenSet<string>.Empty;
    }
}
