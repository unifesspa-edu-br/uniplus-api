namespace Unifesspa.UniPlus.Autorizacao.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Seguranca;
using Unifesspa.UniPlus.Governance.Contracts;
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
        services.AddScoped<IConcessoesAtivasDoSujeito, ConcessoesAtivasDoSujeito>();
        services.AddScoped<IPedidoDeConcessaoRepository, PedidoDeConcessaoRepository>();
        services.AddSingleton<ICatalogoDePermissoesConsultavel, CatalogoDePermissoesDaAplicacao>();

        services.AddScoped<IResumoDeCodigoDeConfirmacao, ResumoDeCodigoDeConfirmacao>();

        // Lida pelas opções, e não no registro: o valor final da configuração só existe quando o host está de pé.
        services.AddOptions<InstalacaoOptions>().BindConfiguration(InstalacaoOptions.SectionName);
        services.AddSingleton<IDominiosInstitucionais, DominiosInstitucionais>();

        // Sem o endereço da Configuração o aviso sairia sem link: o pod falha no startup, e não no primeiro e-mail.
        services.AddOptions<AvisosDoPedidoOptions>()
            .BindConfiguration(AvisosDoPedidoOptions.SectionName)
            .Validate(
                static o => AvisosDoPedidoOptions.EnderecoValido(o.EnderecoDaConfiguracao),
                "Autorizacao:EnderecoDaConfiguracao é obrigatório e deve ser uma URL absoluta http ou https.")
            .ValidateOnStart();
        services.AddSingleton<ILinkDoPedido, LinkDoPedido>();

        // O leitor de unidades usa o DbContext da Organização Institucional; a fábrica o esconde do detector de
        // transação do Wolverine, para que o pedido e os avisos entrem no outbox do DbContext da Autorização.
        services.AddScoped<ILeitorDeUnidade>(static sp => new LeitorDeUnidade(sp.GetRequiredService<IUnidadeReader>()));

        // Os nomes dos buckets e a credencial são obrigatórios: sem eles o pod falha no startup, e não no primeiro envio.
        services.AddOptions<ArquivosDaAutorizacaoOptions>()
            .BindConfiguration(ArquivosDaAutorizacaoOptions.SectionName)
            .Validate(
                static o => !string.IsNullOrWhiteSpace(o.BucketDeEnvio) && !string.IsNullOrWhiteSpace(o.BucketDefinitivo)
                    && !string.IsNullOrWhiteSpace(o.AccessKey) && !string.IsNullOrWhiteSpace(o.SecretKey),
                "ArquivosDoCandidato:BucketDeEnvio, BucketDefinitivo, AccessKey e SecretKey são obrigatórios.")
            .ValidateOnStart();
        services.AddSingleton<IArmazenamentoDosDocumentosDaAutorizacao>(static sp =>
        {
            IOptions<ArquivosDaAutorizacaoOptions> instalacao = sp.GetRequiredService<IOptions<ArquivosDaAutorizacaoOptions>>();
            return new ArmazenamentoDosDocumentosDaAutorizacao(
                StorageServiceCollectionExtensions.CriarStorageComCredencialPropria(
                    sp, instalacao.Value.AccessKey, instalacao.Value.SecretKey),
                instalacao);
        });

        return services;
    }
}
