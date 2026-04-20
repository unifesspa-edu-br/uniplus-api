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
        // case-insensitive e preservar ordem de inserção. Mutação in-place
        // respeita o contrato get-only da propriedade — nunca reassignamos,
        // apenas editamos a lista que já foi construída com os defaults.
        optionsBuilder.PostConfigure(opts =>
        {
            NormalizarListaInPlace(opts.NomesParametrosSensiveis);
            NormalizarListaInPlace(opts.PrefixosSilenciados);
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

    private static void NormalizarListaInPlace(IList<string> lista)
    {
        if (lista.Count == 0)
        {
            return;
        }

        // Duas passadas: a primeira coleta o estado normalizado em buffer
        // temporário (trim + dedup case-insensitive + skip de brancos,
        // preservando ordem de primeira ocorrência). A segunda substitui o
        // conteúdo da lista original via Clear + Add. Evita a complexidade
        // e o custo O(N²) de remoções sucessivas em IList durante iteração.
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
