namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

public static class CryptographyServiceCollectionExtensions
{
    public static IServiceCollection AddUniPlusEncryption(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        Action<EncryptionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        OptionsBuilder<EncryptionOptions> optionsBuilder = services.AddOptions<EncryptionOptions>();

        if (configuration is not null)
        {
            IConfigurationSection section = configuration.GetSection(EncryptionOptions.SectionName);
            if (section.Exists())
            {
                optionsBuilder.Bind(section);
            }
        }

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

        services.AddSingleton<IUniPlusEncryptionService>(sp =>
        {
            EncryptionOptions opts = sp.GetRequiredService<IOptions<EncryptionOptions>>().Value;

            if (string.Equals(opts.Provider, "vault", StringComparison.OrdinalIgnoreCase))
            {
                return ActivatorUtilities.CreateInstance<VaultTransitEncryptionService>(sp);
            }

            if (string.Equals(opts.Provider, "local", StringComparison.OrdinalIgnoreCase))
            {
                return ActivatorUtilities.CreateInstance<LocalAesEncryptionService>(sp);
            }

            throw new InvalidOperationException(
                $"UniPlus:Encryption:Provider inválido: '{opts.Provider}'. Use 'vault' ou 'local'.");
        });

        return services;
    }
}
