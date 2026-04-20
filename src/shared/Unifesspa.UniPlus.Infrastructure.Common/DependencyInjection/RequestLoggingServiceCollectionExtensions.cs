namespace Unifesspa.UniPlus.Infrastructure.Common.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Common.Middleware;

public static class RequestLoggingServiceCollectionExtensions
{
    public static IServiceCollection AddRequestLogging(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        Action<RequestLoggingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        OptionsBuilder<RequestLoggingOptions> optionsBuilder = services.AddOptions<RequestLoggingOptions>();

        if (configuration is not null)
        {
            IConfigurationSection secao = configuration.GetSection(RequestLoggingOptions.SectionName);
            if (secao.Exists())
            {
                optionsBuilder.Bind(secao);
            }
        }

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        // Binder de IConfiguration e Action<T> agregam às listas pré-populadas
        // em vez de substituí-las. Normalizar aqui: remover brancos, deduplicar
        // case-insensitive e preservar ordem de inserção. Para o caso de uso
        // (masking PII + silêncio de paths) essa semântica "defaults como piso,
        // config amplia" é a mais segura — impede que um appsettings remova
        // acidentalmente `cpf` da proteção.
        optionsBuilder.PostConfigure(opts =>
        {
            opts.NomesParametrosSensiveis = NormalizarLista(opts.NomesParametrosSensiveis);
            opts.PathsSilenciados = NormalizarLista(opts.PathsSilenciados);
        });

        // Validação executada uma vez ao materializar `IOptions<T>`. O flag
        // ValidateOnStart garante falha de startup se algum appsettings
        // remover a lista de parâmetros sensíveis — impede regressão LGPD
        // passar despercebida em produção.
        services.AddSingleton<IValidateOptions<RequestLoggingOptions>, RequestLoggingOptionsValidator>();
        optionsBuilder.ValidateOnStart();

        services.AddSingleton<QueryStringMasker>();

        return services;
    }

    private static List<string> NormalizarLista(IList<string>? entrada)
    {
        if (entrada is null || entrada.Count == 0)
        {
            return [];
        }

        HashSet<string> vistos = new(StringComparer.OrdinalIgnoreCase);
        List<string> saida = new(entrada.Count);
        foreach (string item in entrada)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            string normalizado = item.Trim();
            if (vistos.Add(normalizado))
            {
                saida.Add(normalizado);
            }
        }

        return saida;
    }
}
