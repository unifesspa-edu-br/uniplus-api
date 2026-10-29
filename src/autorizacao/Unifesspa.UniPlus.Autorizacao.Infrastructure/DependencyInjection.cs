namespace Unifesspa.UniPlus.Autorizacao.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Seguranca;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;

/// <summary>
/// Registra a infraestrutura do módulo Autorização (DbContext, interceptors, repositório e a
/// porta do catálogo). Wire-up centralizado em <see cref="UniPlusDbContextOptionsExtensions"/>
/// (ADR-0054).
/// </summary>
public static class AutorizacaoInfrastructureRegistration
{
    private const string ConnectionStringName = "AutorizacaoDb";

    public static IServiceCollection AddAutorizacaoInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUniPlusEfInterceptors();

        services.AddDbContext<AutorizacaoDbContext>((serviceProvider, options) =>
            options.UseUniPlusNpgsqlConventions<AutorizacaoDbContext>(serviceProvider, ConnectionStringName, schema: AutorizacaoDbContext.Schema));

        // A Application consome a abstração do próprio módulo, roteada para o DbContext (ADR-0042).
        services.AddScoped<IAutorizacaoUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<AutorizacaoDbContext>());

        services.AddScoped<IPerfilRepository, PerfilRepository>();
        services.AddScoped<IConcessaoRepository, ConcessaoRepository>();
        services.AddScoped<IInstalacaoRepository, InstalacaoRepository>();
        services.AddSingleton<ICatalogoDePermissoesConsultavel, CatalogoDePermissoesDaAplicacao>();

        services.AddScoped<IResumoDeCodigoDeConfirmacao, ResumoDeCodigoDeConfirmacao>();

        // Lida pelas opções, e não no registro: o valor final da configuração só existe quando o host está de pé.
        services.AddOptions<InstalacaoOptions>().BindConfiguration(InstalacaoOptions.SectionName);
        services.AddSingleton<IDominiosInstitucionais, DominiosInstitucionais>();

        // Os nomes dos buckets e a credencial são obrigatórios: sem eles o pod falha no startup, e não no primeiro envio.
        services.AddOptions<ArquivosDaInstalacaoOptions>()
            .BindConfiguration(ArquivosDaInstalacaoOptions.SectionName)
            .Validate(
                static o => !string.IsNullOrWhiteSpace(o.BucketDeEnvio)
                    && !string.IsNullOrWhiteSpace(o.AccessKey) && !string.IsNullOrWhiteSpace(o.SecretKey),
                "ArquivosDoCandidato:BucketDeEnvio, AccessKey e SecretKey são obrigatórios.")
            .ValidateOnStart();
        services.AddSingleton<IArmazenamentoDoDocumentoDaInstalacao>(static sp =>
        {
            IOptions<ArquivosDaInstalacaoOptions> instalacao = sp.GetRequiredService<IOptions<ArquivosDaInstalacaoOptions>>();
            return new ArmazenamentoDaInstalacao(
                StorageServiceCollectionExtensions.CriarStorageComCredencialPropria(
                    sp, instalacao.Value.AccessKey, instalacao.Value.SecretKey),
                instalacao);
        });

        return services;
    }
}
