namespace Unifesspa.UniPlus.Selecao.API;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Selecao.API.Errors;
using Unifesspa.UniPlus.Selecao.API.Hateoas;
using Unifesspa.UniPlus.Selecao.API.Rotinas;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Queries.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Infrastructure;

/// <summary>
/// Registro self-describing do módulo Selecao para o composition root do
/// monólito modular. Reúne tudo que é específico do módulo — OpenAPI
/// doc, registro de erros de domínio, builders HATEOAS, permissões, Application
/// e Infrastructure.
///
/// O que é compartilhado entre módulos (Serilog, auth, CORS, cache, storage,
/// observabilidade, middleware, AddControllers, AddDomainErrorMapper, health
/// checks de infra) fica no host. O wiring de Wolverine (ADR-0003/0004/0005) —
/// discovery de assembly e composição de routing — é setup de host/processo e
/// permanece no Program.cs. Mantém o módulo extraível: um serviço próprio
/// chamaria este mesmo método.
/// </summary>
public static class SelecaoModuleRegistration
{
    public static IServiceCollection AddSelecaoModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        RegistrarContratoHttp(services, configuration);
        RegistrarPermissoes(services);
        RegistrarAplicacaoEInfraestrutura(services);
        RegistrarRemocaoDeArquivosPendentes(services, configuration);
        RegistrarReconciliacaoDaVerificacao(services, configuration);

        return services;
    }

    /// <summary>
    /// OpenAPI 3.1 (ADR-0030): documento nomeado por módulo, com os transformers Uni+, exposto em
    /// <c>/openapi/selecao.json</c>. Os erros de domínio do módulo são consumidos pelo <c>AddDomainErrorMapper</c>,
    /// registrado uma vez no host via <c>IEnumerable&lt;IDomainErrorRegistration&gt;</c>. O HATEOAS Level 1 (ADR-0029)
    /// tem um builder de <c>_links</c> por recurso, singleton porque encapsula só um <c>LinkGenerator</c> (também
    /// singleton) e é função pura.
    /// </summary>
    private static void RegistrarContratoHttp(IServiceCollection services, IConfiguration configuration)
    {
        services.AddUniPlusOpenApi(SelecaoApiAssemblyMarker.ModuleName, configuration);
        services.AddSingleton<IDomainErrorRegistration, SelecaoDomainErrorRegistration>();
        services.AddSingleton<IResourceLinksBuilder<ObrigatoriedadeLegalDto>, ObrigatoriedadeLegalLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<MotivoDeDecisaoDto>, MotivoDeDecisaoLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<ProcessoSeletivoDto>, ProcessoSeletivoLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<RegraCatalogoDto>, RegraCatalogoLinksBuilder>();
    }

    /// <summary>
    /// O enum de permissões do módulo entra no catálogo da aplicação e é conferido na subida (ADR-0151).
    /// </summary>
    private static void RegistrarPermissoes(IServiceCollection services)
    {
        services.AddCatalogoDePermissoes<PermissaoSelecao>();
    }

    private static void RegistrarAplicacaoEInfraestrutura(IServiceCollection services)
    {
        services.AddSelecaoApplication();
        services.AddSelecaoInfrastructure();
    }

    /// <summary>
    /// Rotina periódica que remove os envios de arquivo pendentes vencidos. Registrada depois das migrations e com a
    /// primeira execução um intervalo após a partida; os hosts de teste a removem pelo tipo base (<c>ApiFactoryBase</c>).
    /// A seção é opcional: sem ela vale o padrão.
    /// </summary>
    private static void RegistrarRemocaoDeArquivosPendentes(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RemocaoDeArquivosPendentesOptions>()
            .Bind(configuration.GetSection(RemocaoDeArquivosPendentesOptions.SectionName))
            .Validate(
                static options => options.Intervalo >= RemocaoDeArquivosPendentesOptions.IntervaloMinimo
                    && options.Intervalo <= RemocaoDeArquivosPendentesOptions.IntervaloMaximo,
                $"{RemocaoDeArquivosPendentesOptions.SectionName}:Intervalo deve ficar entre {RemocaoDeArquivosPendentesOptions.IntervaloMinimo} e {RemocaoDeArquivosPendentesOptions.IntervaloMaximo}.")
            .ValidateOnStart();
        services.AddHostedService<RemocaoDeArquivosPendentesVencidosHostedService>();
    }

    private static void RegistrarReconciliacaoDaVerificacao(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ReconciliacaoDaVerificacaoOptions>()
            .Bind(configuration.GetSection(ReconciliacaoDaVerificacaoOptions.SectionName))
            .Validate(
                static options => options.Prazo >= ReconciliacaoDaVerificacaoOptions.PrazoMinimo
                    && options.PrazoMaximo >= options.Prazo
                    && options.Intervalo >= ReconciliacaoDaVerificacaoOptions.IntervaloMinimo
                    && options.TamanhoDoLote > 0,
                $"{ReconciliacaoDaVerificacaoOptions.SectionName}: Prazo de pelo menos {ReconciliacaoDaVerificacaoOptions.PrazoMinimo} e PrazoMaximo não menor que ele, Intervalo de pelo menos {ReconciliacaoDaVerificacaoOptions.IntervaloMinimo} e TamanhoDoLote positivo.")
            .ValidateOnStart();
        services.AddHostedService<ReconciliacaoDaVerificacaoHostedService>();
    }
}
