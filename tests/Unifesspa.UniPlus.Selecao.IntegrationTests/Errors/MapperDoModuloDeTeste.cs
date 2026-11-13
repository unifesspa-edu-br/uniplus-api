namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Errors;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.API.Errors;

/// <summary>
/// O mapper de erros montado pelo mesmo caminho da produção — o registro do módulo servido por DI —
/// para que o teste leia o que a API realmente devolve, e não uma cópia.
/// </summary>
internal static class MapperDoModuloDeTeste
{
    internal const string BaseUriDeErros = "https://unifesspa-edu-br.github.io/uniplus-developers/erros/";

    internal static IDomainErrorMapper Criar()
    {
        IConfiguration configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ProblemTypeOptions.SectionName}:{nameof(ProblemTypeOptions.BaseUri)}"] = BaseUriDeErros,
            })
            .Build();

        ServiceCollection services = new();
        services.AddDomainErrorMapper(configuracao);
        services.AddSingleton<IDomainErrorRegistration, SelecaoDomainErrorRegistration>();

        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IDomainErrorMapper>();
    }
}
