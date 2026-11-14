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
using Unifesspa.UniPlus.Infrastructure.Core.Storage;

/// <summary>
/// Registra a infraestrutura do módulo Autorização: o interceptador da trilha de contenção, os repositórios e a porta do
/// catálogo. O contexto e a unidade de trabalho vêm da persistência única do monólito.
/// </summary>
public static class AutorizacaoInfrastructureRegistration
{
    public static IServiceCollection AddAutorizacaoInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // A AutorizacaoContribuicao acrescenta o interceptador às opções do contexto.
        services.AddScoped<OrdemDaTrilhaDeContencaoInterceptor>();
        services.AddScoped<ITravaDaRotina, TravaDaRotinaNoBanco>();
        AddRepositorios(services);
        AddCatalogo(services);
        AddInstalacao(services);
        AddAvisos(services);
        AddArmazenamento(services);

        return services;
    }

    private static void AddRepositorios(IServiceCollection services)
    {
        services.AddScoped<IPerfilRepository, PerfilRepository>();
        services.AddScoped<IConcessaoRepository, ConcessaoRepository>();
        services.AddScoped<IJanelaDeMigracaoRepository, JanelaDeMigracaoRepository>();
        services.AddScoped<IPapelDoRepresentante, PapelDoRepresentante>();
        services.AddScoped<IIncompatibilidadeDosPapeis, IncompatibilidadeDosPapeisNoBanco>();
        services.AddScoped<IInstalacaoRepository, InstalacaoRepository>();
        services.AddScoped<IConcessoesAtivasDoSujeito, ConcessoesAtivasDoSujeito>();
        services.AddScoped<ITitularesDaPermissao, TitularesDaPermissao>();
        services.AddScoped<IPedidoDeConcessaoRepository, PedidoDeConcessaoRepository>();
        services.AddScoped<ITermoDeResponsabilidadeRepository, TermoDeResponsabilidadeRepository>();
        services.AddScoped<IRevisaoDeAcessosRepository, RevisaoDeAcessosRepository>();
        services.AddScoped<IProvidenciaRepository, ProvidenciaRepository>();
        services.AddScoped<IReclassificacaoRepository, ReclassificacaoRepository>();
        services.AddScoped<ILeituraDoMonitoramento, LeituraDoMonitoramento>();
    }

    private static void AddCatalogo(IServiceCollection services)
    {
        services.AddSingleton<ICatalogoDePermissoesConsultavel, CatalogoDePermissoesDaAplicacao>();
        services.AddScoped<ConciliacaoDoCatalogo>();
    }

    private static void AddInstalacao(IServiceCollection services)
    {
        services.AddScoped<IResumoDeCodigoDeConfirmacao, ResumoDeCodigoDeConfirmacao>();

        // Lida pelas opções, e não no registro: o valor final da configuração só existe quando o host está de pé.
        services.AddOptions<InstalacaoOptions>().BindConfiguration(InstalacaoOptions.SectionName);
        services.AddSingleton<IDominiosInstitucionais, DominiosInstitucionais>();
    }

    private static void AddAvisos(IServiceCollection services)
    {
        // Sem o endereço da Configuração o aviso sairia sem link: o pod falha no startup, e não no primeiro e-mail.
        services.AddOptions<AvisosDoPedidoOptions>()
            .BindConfiguration(AvisosDoPedidoOptions.SectionName)
            .Validate(
                static o => AvisosDoPedidoOptions.EnderecoValido(o.EnderecoDaConfiguracao),
                "Autorizacao:EnderecoDaConfiguracao é obrigatório e deve ser uma URL absoluta http ou https.")
            .ValidateOnStart();
        services.AddSingleton<ILinkDoPedido, LinkDoPedido>();
        services.AddSingleton<ILinkDaGovernanca, LinkDaGovernanca>();
        services.AddScoped<IRegistroDeAvisos, RegistroDeAvisos>();

        // Por fábrica, e não por tipo: o concreto é interno e o ponto de decisão o injeta (ADR-0098).
        services.AddScoped<ILeitorDeUnidade>(static sp => new LeitorDeUnidade(sp.GetRequiredService<IUnidadeReader>()));
    }

    private static void AddArmazenamento(IServiceCollection services)
    {
        // Os nomes dos buckets e a credencial são obrigatórios: sem eles o pod falha no startup, e não no primeiro envio.
        services.AddOptions<ArquivosDaAutorizacaoOptions>()
            .BindConfiguration(ArquivosDaAutorizacaoOptions.SectionName)
            .Validate(
                static o => !string.IsNullOrWhiteSpace(o.BucketDeEnvio) && !string.IsNullOrWhiteSpace(o.BucketDefinitivo)
                    && !string.IsNullOrWhiteSpace(o.AccessKey) && !string.IsNullOrWhiteSpace(o.SecretKey),
                "ArquivosDoCandidato:BucketDeEnvio, BucketDefinitivo, AccessKey e SecretKey são obrigatórios.")
            .ValidateOnStart();
        services.AddSingleton<IArmazenamentoDosDocumentosDaAutorizacao>(CriarArmazenamento);
    }

    private static ArmazenamentoDosDocumentosDaAutorizacao CriarArmazenamento(IServiceProvider sp)
    {
        IOptions<ArquivosDaAutorizacaoOptions> instalacao = sp.GetRequiredService<IOptions<ArquivosDaAutorizacaoOptions>>();
        return new ArmazenamentoDosDocumentosDaAutorizacao(
            StorageServiceCollectionExtensions.CriarStorageComCredencialPropria(
                sp, instalacao.Value.AccessKey, instalacao.Value.SecretKey),
            instalacao);
    }
}
