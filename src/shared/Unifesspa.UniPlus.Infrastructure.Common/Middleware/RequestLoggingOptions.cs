namespace Unifesspa.UniPlus.Infrastructure.Common.Middleware;

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Options;

public sealed class RequestLoggingOptions
{
    public const string SectionName = "RequestLogging";

    // Listas são `IList<string>` mutáveis (setter público) porque o binder
    // de `IConfiguration.Bind` e `Configure<T>(Action<T>)` precisam mutar a
    // instância após construção. CA2227 é suprimido: DTOs de Options são
    // explicitamente projetados para esse ciclo de vida. Os consumidores
    // internos copiam o conteúdo para `FrozenSet` e não mutam a lista.
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO de IOptions: binder exige setter mutável.")]
    public IList<string> NomesParametrosSensiveis { get; set; } = DefaultsNomesParametrosSensiveis();

    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "DTO de IOptions: binder exige setter mutável.")]
    public IList<string> PathsSilenciados { get; set; } = DefaultsPathsSilenciados();

    public string ValorMascarado { get; set; } = "***";

    // Os defaults são expostos para que validadores e testes possam checar a
    // intenção inicial da configuração sem depender de uma nova instância.
    public static IList<string> DefaultsNomesParametrosSensiveis() =>
        ["cpf", "email", "senha", "password", "token"];

    // Cada entrada é tratada como prefixo de rota pelo middleware (match por
    // prefixo com boundary em `/`). Assim `/health` cobre automaticamente
    // `/health/ready`, `/health/live`, `/health/db/postgresql` e qualquer
    // outro subpath exposto por libraries de health-check.
    public static IList<string> DefaultsPathsSilenciados() =>
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

        if (options.PathsSilenciados is null)
        {
            erros.Add($"{nameof(RequestLoggingOptions.PathsSilenciados)} não pode ser nulo — use uma lista vazia para desativar o silenciamento.");
        }
        else if (options.PathsSilenciados.Any(p => !p.StartsWith('/')))
        {
            erros.Add($"{nameof(RequestLoggingOptions.PathsSilenciados)} contém path inválido — cada entrada deve começar com '/'.");
        }

        if (string.IsNullOrEmpty(options.ValorMascarado))
        {
            erros.Add($"{nameof(RequestLoggingOptions.ValorMascarado)} não pode ser vazio — valor mascarado é o que aparece nos logs no lugar do dado sensível.");
        }

        return erros.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(erros);
    }
}
