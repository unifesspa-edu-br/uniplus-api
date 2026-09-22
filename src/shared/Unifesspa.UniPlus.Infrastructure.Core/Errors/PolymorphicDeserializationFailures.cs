namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using Microsoft.AspNetCore.Http;

/// <summary>
/// A causa estruturada da recusa de leitura em que o corpo traz o objeto, mas não declara qual
/// variante do contrato ele representa — anotada no instante da falha, com o campo e o nome da
/// propriedade que faltou.
/// </summary>
/// <remarks>
/// <para>
/// Vive em <c>HttpContext.Items</c> pela mesma razão que <see cref="ValueConversionFailures"/>:
/// depois que o <c>ModelState</c> normaliza a recusa, uma união sem discriminador e um documento
/// sintaticamente quebrado ficam indistinguíveis, e a resposta genérica de corpo ilegível manda o
/// cliente reler um documento que está bem formado. O que separa as duas é o que se sabia no
/// momento da leitura, e é isso que fica guardado aqui.
/// </para>
/// <para>
/// Nada do que se anota vem do texto da exceção do framework: o campo é o caminho JSON que o
/// desserializador reporta, e o discriminador é lido do contrato do próprio modelo.
/// </para>
/// </remarks>
internal static class PolymorphicDeserializationFailures
{
    /// <summary>Chave em <c>HttpContext.Items</c> onde a falha anotada fica.</summary>
    public const string HttpContextItemKey = "__UniPlusPolymorphicDeserializationFailure";

    /// <summary>
    /// Anota que <paramref name="campo"/> — caminho JSON, ou vazio para o corpo inteiro — foi
    /// recusado por não declarar <paramref name="discriminador"/>.
    /// </summary>
    public static void Record(HttpContext httpContext, string campo, string discriminador)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrEmpty(discriminador);

        httpContext.Items[HttpContextItemKey] = new Falha(campo, discriminador);
    }

    /// <summary>
    /// A falha anotada nesta requisição, ou <see langword="null"/> quando a recusa de leitura
    /// teve outra causa — caso em que quem monta a resposta usa a recusa genérica.
    /// </summary>
    public static Falha? Of(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return httpContext.Items.TryGetValue(HttpContextItemKey, out object? anotada)
            && anotada is Falha falha
            ? falha
            : null;
    }

    /// <param name="Campo">
    /// Caminho JSON do campo recusado, sem o <c>$</c> inicial — vazio quando o corpo inteiro é
    /// que é a união.
    /// </param>
    /// <param name="Discriminador">Nome da propriedade que identifica a variante, por exemplo <c>$tipo</c>.</param>
    internal readonly record struct Falha(string Campo, string Discriminador);
}
