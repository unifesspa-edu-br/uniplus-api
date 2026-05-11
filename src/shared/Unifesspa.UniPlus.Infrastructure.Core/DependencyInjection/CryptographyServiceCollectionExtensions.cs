namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Cryptography;

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

        // Validator condicional Provider × campos dependentes. Composto com ValidateDataAnnotations:
        // ambos rodam ao materializar IOptions<EncryptionOptions>.Value e ValidateOnStart força no boot.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<EncryptionOptions>, EncryptionOptionsValidator>());

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

        // Warmup hosted service força a resolução do IUniPlusEncryptionService no
        // Host.StartAsync — falha do construtor (JWT ausente, mutex auth method)
        // vira CrashLoopBackOff antes do app aceitar tráfego, em vez de 500 na
        // primeira request cifrada. Test factories (ApiFactoryBase) carregam
        // appsettings.Development.json com LocalKey válida e por isso o warmup
        // roda normalmente em integration tests; fixtures que precisem mockar o
        // pipeline sem cifragem real podem filtrar via heurística estável de
        // ImplementationType, mesmo pattern de MigrationHostedService/Wolverine.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, EncryptionWarmupHostedService>());

        return services;
    }
}
