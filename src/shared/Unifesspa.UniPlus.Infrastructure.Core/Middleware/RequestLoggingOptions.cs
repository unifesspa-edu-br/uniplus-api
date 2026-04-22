namespace Unifesspa.UniPlus.Infrastructure.Core.Middleware;

using Microsoft.Extensions.Options;

public sealed class RequestLoggingOptions
{
    public const string SectionName = "RequestLogging";

    /// <summary>
    /// Nomes de parâmetros de query string cujo valor deve ser mascarado no log
    /// (comparação case-insensitive após decodificação percent-encoding). Defaults
    /// cobrem o mínimo regulatório LGPD: cpf, email, senha, password, token.
    /// Configuração amplia os defaults em vez de substituir — é impossível
    /// acidentalmente remover um parâmetro sensível via appsettings. Para substituir
    /// integralmente os defaults, chamar <c>Clear()</c> antes de adicionar novos
    /// valores via <c>Configure&lt;T&gt;(Action)</c>.
    /// </summary>
    public IList<string> NomesParametrosSensiveis { get; } = DefaultsNomesParametrosSensiveis();

    /// <summary>
    /// Prefixos de path silenciados para respostas de sucesso (status &lt; 400).
    /// Match case-insensitive com boundary em '/': <c>/health</c> cobre
    /// <c>/health</c>, <c>/health/</c>, <c>/health/ready</c>, <c>/health/db/postgresql</c>,
    /// mas <b>não</b> <c>/healthy</c>, <c>/health-ui</c> ou <c>/healthcheck</c> —
    /// apenas separadores hierárquicos de URL são aceitos como extensão do prefixo.
    /// Trailing slash na entrada ou no request é normalizado. Para silenciar apenas
    /// uma rota exata, declare-a sem filhos (o match também casa o prefixo puro).
    /// Respostas de erro (status &ge; 400) em paths silenciados são <b>sempre</b>
    /// logadas — silenciar falhas equivaleria a apagar alarmes.
    /// Lista vazia desativa o silenciamento.
    /// </summary>
    public IList<string> PrefixosSilenciados { get; } = DefaultsPrefixosSilenciados();

    /// <summary>
    /// String que substitui o valor de parâmetros sensíveis no log. Default "***".
    /// Não pode ser vazio — validado na inicialização.
    /// </summary>
    public string ValorMascarado { get; set; } = "***";

    public static IList<string> DefaultsNomesParametrosSensiveis() =>
        ["cpf", "email", "senha", "password", "token"];

    public static IList<string> DefaultsPrefixosSilenciados() =>
        ["/health", "/metrics"];
}

internal sealed class RequestLoggingOptionsValidator : IValidateOptions<RequestLoggingOptions>
{
    public ValidateOptionsResult Validate(string? name, RequestLoggingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> erros = new();
        ValidarNomesParametrosSensiveis(options.NomesParametrosSensiveis, erros);
        ValidarPrefixosSilenciados(options.PrefixosSilenciados, erros);
        ValidarValorMascarado(options.ValorMascarado, erros);

        return erros.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(erros);
    }

    private static void ValidarNomesParametrosSensiveis(IList<string> lista, List<string> erros)
    {
        if (lista is null || lista.Count == 0)
        {
            erros.Add($"{nameof(RequestLoggingOptions.NomesParametrosSensiveis)} não pode estar vazio — remover masking de PII viola LGPD.");
        }
    }

    private static void ValidarPrefixosSilenciados(IList<string> lista, List<string> erros)
    {
        if (lista.Any(p => !p.StartsWith('/')))
        {
            erros.Add($"{nameof(RequestLoggingOptions.PrefixosSilenciados)} contém path inválido — cada entrada deve começar com '/'.");
        }
    }

    private static void ValidarValorMascarado(string valor, List<string> erros)
    {
        if (string.IsNullOrEmpty(valor))
        {
            erros.Add($"{nameof(RequestLoggingOptions.ValorMascarado)} não pode ser vazio — valor mascarado é o que aparece nos logs no lugar do dado sensível.");
        }
    }
}
