namespace Unifesspa.UniPlus.Publicacoes.Infrastructure;

using HealthChecks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Persistence.Repositories;

using Readers;

using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

public static class PublicacoesInfrastructureRegistration
{
    /// <summary>
    /// Registra a infraestrutura do módulo Publicações: repositórios, leitores e a verificação do catálogo de tipos de
    /// ato. O contexto e a unidade de trabalho vêm da persistência única do monólito.
    /// </summary>
    public static IServiceCollection AddPublicacoesInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ITipoAtoPublicadoRepository, TipoAtoPublicadoRepository>();

        // Leitura cross-módulo do catálogo (ADR-0056): deixa o domínio que vai publicar
        // conferir o tipo declarado ANTES de escrever, em vez de descobrir na dead letter.
        services.AddScoped<ITipoAtoPublicadoReader, TipoAtoPublicadoReader>();
        services.AddScoped<IAtoNormativoRepository, AtoNormativoRepository>();

        // O que mantém o pod no Service é degradado responder 200, e não a ausência da tag: a
        // readinessProbe do chart aponta para /health, que não filtra por tag. A tag fica de fora
        // de `ready` porque dado de cadastro não é critério de prontidão, e o /health/ready
        // continua respondendo só sobre dependência de infraestrutura.
        //
        // O timeout existe porque esta conferência consulta uma tabela, ao contrário do SELECT 1
        // do check de Postgres: sem ele a registration nasce com espera infinita, e uma consulta
        // presa por lock seguraria o /health além dos 3s da sonda até o kubelet tirar todas as
        // réplicas — o oposto do que "degradado, nunca indisponível" promete.
        services.AddOptions<CatalogoDeTiposAtoOptions>()
            .BindConfiguration(CatalogoDeTiposAtoOptions.SectionName);

        services.AddHealthChecks().AddCheck<CatalogoDeTiposAtoHealthCheck>(
            name: "catalogo-tipos-ato",
            failureStatus: HealthStatus.Degraded,
            tags: ["catalogo", "publicacoes"],
            timeout: TimeSpan.FromSeconds(2));

        return services;
    }
}
