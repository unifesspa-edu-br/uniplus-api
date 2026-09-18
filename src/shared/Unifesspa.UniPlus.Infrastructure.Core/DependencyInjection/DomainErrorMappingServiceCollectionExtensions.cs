namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Errors;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Registra o mapeamento <c>code → (status, type, title)</c> da ADR-0024: o registry que
/// resolve status e title a partir dos registros de cada módulo, e a fábrica única do
/// campo <c>type</c>, compartilhada com os emissores de <c>problem+json</c> que não
/// passam por erro de domínio (401/403, 406 e o boundary de exceção).
/// </summary>
public static class DomainErrorMappingServiceCollectionExtensions
{
    public static IServiceCollection AddDomainErrorMapper(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<ProblemTypeOptions>()
            .Bind(configuration.GetSection(ProblemTypeOptions.SectionName))
            // ValidateOnStart: base ausente ou malformada derruba o boot. Sem isso, o
            // defeito só apareceria na primeira resposta de erro — e como o campo type
            // não é lido no caminho feliz, passaria despercebido.
            .ValidateOnStart();
        services.TryAddSingleton<IValidateOptions<ProblemTypeOptions>, ProblemTypeOptionsValidator>();
        services.TryAddSingleton<IProblemTypeUriFactory, ProblemTypeUriFactory>();

        services.AddSingleton<IDomainErrorRegistration, KernelDomainErrorRegistration>();
        services.AddSingleton<IDomainErrorMapper>(sp =>
        {
            IEnumerable<IDomainErrorRegistration> registrations = sp.GetServices<IDomainErrorRegistration>();
            return new DomainErrorMappingRegistry(
                registrations,
                sp.GetRequiredService<IProblemTypeUriFactory>());
        });
        // O envelope das recusas de LEITURA da requisição, que o MVC responde antes de
        // qualquer código nosso rodar. Sem isto elas saem no formato do framework — sem `code`,
        // sem `traceId`, título em inglês e, o que é grave, com o nome completo do tipo CLR que
        // falhou a desserialização dentro do corpo.
        //
        // Compõe, não substitui: o factory devolve `null` para tudo que não seja leitura da
        // requisição, e a cadeia segue. É o que preserva a mensagem de um `[RegularExpression]` num
        // parâmetro de rota — escrita por nós, dizendo ao cliente qual é o formato esperado —,
        // que acontece DEPOIS de o binding ter dado certo e não tem nada a ver com corpo
        // ilegível.
        //
        // Registrado AQUI, e não junto de quem compõe depois, porque este é o mais genérico da
        // cadeia: cada `PostConfigure` seguinte captura o factory corrente como `previous` e o
        // chama quando a falha não é dele. Instalar o genérico no primeiro registro do pipeline
        // de erro é o que o mantém no FIM da cadeia, e não na frente dela.
        services.PostConfigure<ApiBehaviorOptions>(options =>
        {
            Func<ActionContext, IActionResult> previous = options.InvalidModelStateResponseFactory;
            options.InvalidModelStateResponseFactory = context =>
                InvalidRequestProblemFactory.TryBuild(context) ?? previous(context);
        });

        // O sinal de que o factory acima precisa para responder também pelo valor de rota ou
        // query que não converte. Sem ele aquela recusa é indistinguível de uma validação
        // reprovada, e é a única razão por que ela continuava saindo no envelope do framework.
        //
        // Envolve os providers já registrados em vez de acrescentar mais um: um provider novo
        // teria de disputar precedência com os existentes, e quem ganha a disputa depende da
        // ordem da lista. Envolver preserva a resolução que o MVC já faz e só observa o
        // resultado dela.
        services.PostConfigure<MvcOptions>(options =>
        {
            for (int i = 0; i < options.ModelBinderProviders.Count; i++)
            {
                options.ModelBinderProviders[i] =
                    new ValueConversionTrackingModelBinderProvider(options.ModelBinderProviders[i]);
            }
        });

        return services;
    }
}
