namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Errors;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
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

        AddProblemTypeOptions(services, configuration);
        AddErrorRegistrations(services);
        // O código que cada violação do FluentValidation publica em errors[].code. Instalado aqui,
        // junto do registro que o resolve, porque todo host que responde o 422 de validação passa
        // por este ponto — e é configuração global do FluentValidation, não algo que o container
        // possa injetar nos validators.
        ValidationErrorCodes.InstallGlobalResolver();
        InstallInvalidRequestResponse(services);
        WrapReadBindersAndFormatters(services);

        return services;
    }

    private static void AddProblemTypeOptions(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<ProblemTypeOptions>()
            .Bind(configuration.GetSection(ProblemTypeOptions.SectionName))
            // ValidateOnStart: base ausente ou malformada derruba o boot. Sem isso, o
            // defeito só apareceria na primeira resposta de erro — e como o campo type
            // não é lido no caminho feliz, passaria despercebido.
            .ValidateOnStart();
        services.TryAddSingleton<IValidateOptions<ProblemTypeOptions>, ProblemTypeOptionsValidator>();
        services.TryAddSingleton<IProblemTypeUriFactory, ProblemTypeUriFactory>();
    }

    private static void AddErrorRegistrations(IServiceCollection services)
    {
        services.AddSingleton<IDomainErrorRegistration, KernelDomainErrorRegistration>();
        services.AddSingleton<IDomainErrorRegistration, RegrasDomainErrorRegistration>();
        services.AddSingleton<IDomainErrorRegistration, ErrosDaFronteiraHttpDomainErrorRegistration>();
        services.AddSingleton<IDomainErrorMapper>(provider => new DomainErrorMappingRegistry(
            provider.GetServices<IDomainErrorRegistration>(),
            provider.GetRequiredService<IProblemTypeUriFactory>()));
    }

    /// <summary>
    /// Instala o envelope das recusas de LEITURA da requisição, que o MVC responde antes de
    /// qualquer código nosso rodar: com <c>code</c>, <c>traceId</c> e título em português, e sem
    /// o nome do tipo CLR que falhou a desserialização. Fica AQUI, e não junto de quem compõe
    /// depois, porque é a resposta mais genérica da cadeia: cada <c>PostConfigure</c> seguinte
    /// guarda a fábrica corrente e a chama quando a falha não é dele, e instalar a genérica no
    /// primeiro registro do pipeline de erro é o que a mantém no FIM da cadeia.
    /// </summary>
    /// <remarks>
    /// Compõe, não substitui: a fábrica devolve <c>null</c> para tudo que não seja leitura da
    /// requisição, e a fábrica anterior responde. É o que preserva a mensagem de um
    /// <c>[RegularExpression]</c> num parâmetro de rota — escrita por nós, dizendo ao cliente qual
    /// é o formato esperado —, que acontece DEPOIS de o binding ter dado certo e não tem nada a
    /// ver com corpo ilegível.
    /// </remarks>
    private static void InstallInvalidRequestResponse(IServiceCollection services) =>
        services.PostConfigure<ApiBehaviorOptions>(options =>
        {
            Func<ActionContext, IActionResult> anterior = options.InvalidModelStateResponseFactory;
            options.InvalidModelStateResponseFactory = contexto =>
                InvalidRequestProblemFactory.TryBuild(contexto) ?? anterior(contexto);
        });

    /// <summary>
    /// Dá à fábrica instalada por <see cref="InstallInvalidRequestResponse"/> o sinal de que
    /// precisa para responder também pelo valor de rota ou query que não converte, e converte em
    /// recusa de leitura a união polimórfica cujo documento não declara o discriminador.
    /// </summary>
    /// <remarks>
    /// Envolve os providers e os formatters já registrados em vez de acrescentar mais um: um novo
    /// teria de disputar precedência com os existentes, e quem ganha a disputa depende da ordem
    /// da lista. Envolver preserva a resolução que o MVC já faz e só observa o resultado dela.
    /// </remarks>
    private static void WrapReadBindersAndFormatters(IServiceCollection services) =>
        services.PostConfigure<MvcOptions>(options =>
        {
            WrapBindersToTrackConversionFailure(options);
            WrapJsonFormatterForPolymorphicUnion(options);
        });

    private static void WrapBindersToTrackConversionFailure(MvcOptions options)
    {
        for (int index = 0; index < options.ModelBinderProviders.Count; index++)
        {
            options.ModelBinderProviders[index] =
                new ValueConversionTrackingModelBinderProvider(options.ModelBinderProviders[index]);
        }
    }

    /// <remarks>
    /// A união polimórfica cujo documento não declara o discriminador de tipo é tratada pelo
    /// formatter do framework como <c>NotSupportedException</c>, que ele deixa passar como erro
    /// interno do servidor. O envoltório a anota como defeito do cliente, que a API sabe descrever.
    /// </remarks>
    private static void WrapJsonFormatterForPolymorphicUnion(MvcOptions options)
    {
        for (int index = 0; index < options.InputFormatters.Count; index++)
        {
            if (options.InputFormatters[index] is SystemTextJsonInputFormatter formatterJson)
            {
                options.InputFormatters[index] = new PolymorphicDeserializationInputFormatter(formatterJson);
            }
        }
    }
}
