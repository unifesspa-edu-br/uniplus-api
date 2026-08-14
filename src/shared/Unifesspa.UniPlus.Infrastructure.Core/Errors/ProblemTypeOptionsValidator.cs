namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

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

    public ValidateOptionsResult Validate(string? name, ProblemTypeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Só valida a instância default — não há named options para o catálogo de erros.
        if (!string.IsNullOrEmpty(name) && name != Options.DefaultName)
        {
            return ValidateOptionsResult.Skip;
        }

        string valor = options.BaseUri?.Trim() ?? string.Empty;

        if (valor.Length == 0)
        {
            return ValidateOptionsResult.Fail(
                $"{Chave} é obrigatório: sem ele o campo type do corpo de erro não aponta " +
                $"para o catálogo público. {Exemplo}");
        }

        // A URI tem de declarar o próprio esquema. Em sistemas de arquivos Unix,
        // `Uri.TryCreate` promove um caminho absoluto como `/erros/` a `file:` e devolve
        // sucesso — a recusa sairia então falando de HTTPS, quando o que o operador
        // escreveu foi um caminho, não uma URI. Comparar o valor com o esquema resolvido
        // separa os dois casos, e a mensagem descreve a causa real nas duas plataformas.
        if (!Uri.TryCreate(valor, UriKind.Absolute, out Uri? uri)
            || !valor.StartsWith(uri.Scheme + Uri.SchemeDelimiter, StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(
                $"{Chave} deve ser URI absoluta — o campo type da RFC 9457 é resolvido pelo " +
                $"consumidor sem contexto do servidor. Recebido: '{options.BaseUri}'. {Exemplo}");
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return ValidateOptionsResult.Fail(
                $"{Chave} deve usar HTTPS. Recebido: '{options.BaseUri}'. {Exemplo}");
        }

        if (uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            return ValidateOptionsResult.Fail(
                $"{Chave} não aceita query string nem fragmento: o código do erro é " +
                $"concatenado ao fim do caminho. Recebido: '{options.BaseUri}'. {Exemplo}");
        }

        return ValidateOptionsResult.Success;
    }
}
