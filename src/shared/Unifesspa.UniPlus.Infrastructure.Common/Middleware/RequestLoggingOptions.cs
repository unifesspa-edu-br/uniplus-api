namespace Unifesspa.UniPlus.Infrastructure.Common.Middleware;

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Options;

public sealed class RequestLoggingOptions
{
    public const string SectionName = "RequestLogging";

    /// <summary>
    /// Nomes de parâmetros de query string cujo valor deve ser mascarado no log
    /// (comparação case-insensitive após decodificação percent-encoding). Defaults
    /// cobrem o mínimo regulatório LGPD: cpf, email, senha, password, token.
    /// Configuração amplia os defaults em vez de substituir — é impossível
    /// acidentalmente remover um parâmetro sensível via appsettings.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO de IOptions: binder exige setter mutável.")]
    public IList<string> NomesParametrosSensiveis { get; set; } = DefaultsNomesParametrosSensiveis();

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
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO de IOptions: binder exige setter mutável.")]
    public IList<string> PrefixosSilenciados { get; set; } = DefaultsPrefixosSilenciados();

    /// <summary>
    /// String que substitui o valor de parâmetros sensíveis no log. Default "***".
    /// Não pode ser vazio — validado na inicialização.
    /// </summary>
    public string ValorMascarado { get; set; } = "***";

    // Os defaults são expostos para que validadores e testes possam checar a
    // intenção inicial da configuração sem depender de uma nova instância.
    public static IList<string> DefaultsNomesParametrosSensiveis() =>
        ["cpf", "email", "senha", "password", "token"];

    // Cada entrada é tratada como prefixo de rota pelo middleware (match por
    // prefixo com boundary em `/`). Assim `/health` cobre automaticamente
    // `/health/ready`, `/health/live`, `/health/db/postgresql` e qualquer
    // outro subpath exposto por libraries de health-check.
    public static IList<string> DefaultsPrefixosSilenciados() =>
        ["/health", "/metrics"];
}

internal sealed class RequestLoggingOptionsValidator : IValidateOptions<RequestLoggingOptions>
{
    public ValidateOptionsResult Validate(string? name, RequestLoggingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> erros = new();

        // Entradas em branco são normalizadas (dropadas) pelo PostConfigure do
        // DI extension — aqui validamos apenas o estado final, após normalização.
        if (options.NomesParametrosSensiveis is null || options.NomesParametrosSensiveis.Count == 0)
        {
            erros.Add($"{nameof(RequestLoggingOptions.NomesParametrosSensiveis)} não pode estar vazio — remover masking de PII viola LGPD.");
        }

        if (options.PrefixosSilenciados is null)
        {
            erros.Add($"{nameof(RequestLoggingOptions.PrefixosSilenciados)} não pode ser nulo — use uma lista vazia para desativar o silenciamento.");
        }
        else if (options.PrefixosSilenciados.Any(p => !p.StartsWith('/')))
        {
            erros.Add($"{nameof(RequestLoggingOptions.PrefixosSilenciados)} contém path inválido — cada entrada deve começar com '/'.");
        }

        if (string.IsNullOrEmpty(options.ValorMascarado))
        {
            erros.Add($"{nameof(RequestLoggingOptions.ValorMascarado)} não pode ser vazio — valor mascarado é o que aparece nos logs no lugar do dado sensível.");
        }

        return erros.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(erros);
    }
}
