namespace Unifesspa.UniPlus.Selecao.Infrastructure;

using Canonicalization;

using Domain.Interfaces;

using ExternalServices;

using Microsoft.Extensions.DependencyInjection;

using Persistence;
using Persistence.Interceptors;
using Persistence.Readers;
using Persistence.Repositories;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;

public static class SelecaoInfrastructureRegistration
{
    private const string ConnectionStringName = "SelecaoDb";

    /// <summary>
    /// Registra a infraestrutura do módulo Seleção (DbContext + interceptors +
    /// repositórios + serviços externos). Wire-up centralizado em
    /// <see cref="UniPlusDbContextOptionsExtensions"/> (ADR-0054): convenção
    /// snake_case global, soft delete + audit interceptors, leitura lazy de
    /// connection string.
    /// </summary>
    public static IServiceCollection AddSelecaoInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUniPlusEfInterceptors();

        // ObrigatoriedadeLegalHistoricoInterceptor (Story #460, ADR-0058) é
        // scoped por simetria com SoftDelete/Auditable — depende do
        // IUserContext scoped para preencher snapshot_by.
        services.AddScoped<ObrigatoriedadeLegalHistoricoInterceptor>();

        services.AddDbContext<SelecaoDbContext>((serviceProvider, options) =>
        {
            options.UseUniPlusNpgsqlConventions<SelecaoDbContext>(serviceProvider, ConnectionStringName, schema: SelecaoDbContext.Schema);

            // Encaixe deliberado em sequência aos interceptors cross-cutting do
            // UseUniPlusNpgsqlConventions — roda DEPOIS de SoftDelete + Auditable,
            // garantindo que mutações via soft-delete (Delete convertido para
            // Modified+IsDeleted=true) também gerem linha no histórico.
            options.AddInterceptors(
                serviceProvider.GetRequiredService<ObrigatoriedadeLegalHistoricoInterceptor>());
        });

        services.AddScoped<ISelecaoUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<SelecaoDbContext>());

        services.AddScoped<IProcessoSeletivoRepository, ProcessoSeletivoRepository>();
        services.AddScoped<ISnapshotPublicacaoCanonicalizer, SnapshotPublicacaoCanonicalizer>();
        services.AddScoped<IRegistroCodecsEnvelope, RegistroCodecsEnvelope>();
        services.AddScoped<IRestauradorDeConfiguracao, RestauradorDeConfiguracao>();
        services.AddScoped<IObrigatoriedadeLegalRepository, ObrigatoriedadeLegalRepository>();
        services.AddScoped<IDocumentoEditalRepository, DocumentoEditalRepository>();
        services.AddScoped<IRegraCatalogoReader, RegraCatalogoReader>();
        services.AddScoped<IRetificacaoEmCursoReader, RetificacaoEmCursoReader>();
        services.AddScoped<IGovBrAuthService, GovBrAuthService>();

        // Storage de documento do Edital (Story #759, T3 #784) — envolve o
        // IStorageService compartilhado (registrado uma vez no host via
        // AddUniPlusStorage). Não registra AddUniPlusStorage aqui: é
        // cross-cutting compartilhado entre módulos, já ligado no host.
        services.AddScoped<IDocumentoEditalStorage, DocumentoEditalStorageService>();

        return services;
    }
}
