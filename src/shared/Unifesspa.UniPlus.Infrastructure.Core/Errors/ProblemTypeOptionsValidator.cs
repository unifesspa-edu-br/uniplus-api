namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Options;

/// <summary>
/// Valida <see cref="ProblemTypeOptions"/> no boot. Sem esta checagem, base ausente ou
/// malformada só apareceria na primeira resposta de erro — e como <c>type</c> é um campo
/// que ninguém consulta no caminho feliz, passaria despercebida até alguém tentar seguir
/// o link.
/// </summary>
internal sealed class ProblemTypeOptionsValidator : IValidateOptions<ProblemTypeOptions>
{
    private const string Chave = $"{ProblemTypeOptions.SectionName}:{nameof(ProblemTypeOptions.BaseUri)}";
    private const string Exemplo = "Ex.: https://unifesspa-edu-br.github.io/uniplus-developers/erros/";
    private const string MotivoObrigatorio =
        "é obrigatório: sem ele o campo type do corpo de erro não aponta para o catálogo público.";
    private const string MotivoNaoAbsoluta =
        "deve ser URI absoluta — o campo type da RFC 9457 é resolvido pelo consumidor sem contexto do servidor.";
    private const string MotivoSemHttps = "deve usar HTTPS.";
    private const string MotivoComQueryOuFragmento =
        "não aceita query string nem fragmento: o código do erro é concatenado ao fim do caminho.";

    public ValidateOptionsResult Validate(string? name, ProblemTypeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Só valida a instância default — não há named options para o catálogo de erros.
        if (!string.IsNullOrEmpty(name) && name != Options.DefaultName)
        {
            return ValidateOptionsResult.Skip;
        }

        string? falha = FalhaDaBase(options.BaseUri);

        return falha is null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falha);
    }

    private static string? FalhaDaBase(string? baseUri)
    {
        string valor = baseUri?.Trim() ?? string.Empty;

        return valor.Length == 0 ? $"{Chave} {MotivoObrigatorio} {Exemplo}" : FalhaDaUri(valor, baseUri);
    }

    private static string? FalhaDaUri(string valor, string? baseUri)
    {
        if (!DeclaraOProprioEsquema(valor, out Uri? uri))
        {
            return RecusaComOValorRecebido(MotivoNaoAbsoluta, baseUri);
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return RecusaComOValorRecebido(MotivoSemHttps, baseUri);
        }

        return uri.Query.Length > 0 || uri.Fragment.Length > 0
            ? RecusaComOValorRecebido(MotivoComQueryOuFragmento, baseUri)
            : null;
    }

    // A URI tem de declarar o próprio esquema. Em sistemas de arquivos Unix, `Uri.TryCreate` promove um caminho
    // absoluto como `/erros/` a `file:` e devolve sucesso — a recusa sairia então falando de HTTPS, quando o que o
    // operador escreveu foi um caminho, não uma URI. Comparar o valor com o esquema resolvido separa os dois casos,
    // e a mensagem descreve a causa real nas duas plataformas.
    private static bool DeclaraOProprioEsquema(string valor, [NotNullWhen(true)] out Uri? uri) =>
        Uri.TryCreate(valor, UriKind.Absolute, out uri)
        && valor.StartsWith(uri.Scheme + Uri.SchemeDelimiter, StringComparison.OrdinalIgnoreCase);

    private static string RecusaComOValorRecebido(string motivo, string? baseUri) =>
        $"{Chave} {motivo} Recebido: '{baseUri}'. {Exemplo}";
}
