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

        // Binder do IConfiguration agrega sem deduplicar; normalizar in-place.
        optionsBuilder.PostConfigure(opts =>
        {
            NormalizarListaInPlace(opts.NomesParametrosSensiveis);
            NormalizarListaInPlace(opts.PrefixosSilenciados);
        });

        // ValidateOnStart: falha no boot se appsettings remover masking LGPD.
        services.AddSingleton<IValidateOptions<RequestLoggingOptions>, RequestLoggingOptionsValidator>();
        optionsBuilder.ValidateOnStart();

        services.AddSingleton<QueryStringMasker>();

        return services;
    }

    // Duas passadas (coleta + rewrite) evitam O(N²) de RemoveAt em single-pass.
    private static void NormalizarListaInPlace(IList<string> lista)
    {
        if (lista.Count == 0)
        {
            return;
        }

        HashSet<string> vistos = new(StringComparer.OrdinalIgnoreCase);
        List<string> normalizada = new(lista.Count);
        foreach (string item in lista)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            string trimmed = item.Trim();
            if (vistos.Add(trimmed))
            {
                normalizada.Add(trimmed);
            }
        }

        lista.Clear();
        foreach (string item in normalizada)
        {
            lista.Add(item);
        }
    }
}
