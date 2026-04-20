namespace Unifesspa.UniPlus.Infrastructure.Common.Tests.DependencyInjection;

using System.Collections.Generic;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Common.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Common.Middleware;

public class RequestLoggingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddRequestLogging_SemConfiguracao_DeveResolverMaskerComDefaults()
    {
        ServiceCollection services = new();

        services.AddRequestLogging();

        using ServiceProvider provider = services.BuildServiceProvider();
        QueryStringMasker masker = provider.GetRequiredService<QueryStringMasker>();
        IOptions<RequestLoggingOptions> options = provider.GetRequiredService<IOptions<RequestLoggingOptions>>();

        options.Value.NomesParametrosSensiveis.Should().Contain(["cpf", "email", "senha", "password", "token"]);
        masker.Mascarar(new QueryString("?cpf=123")).Should().Be("?cpf=***");
    }

    [Fact]
    public void AddRequestLogging_ComActionConfigureLimpandoDefaults_DeveSubstituirCompletamente()
    {
        ServiceCollection services = new();

        services.AddRequestLogging(configure: opts =>
        {
            // Limpar + adicionar expressa substituição explícita. Sem Clear,
            // a semântica seria merge com os defaults (ver teste abaixo).
            opts.NomesParametrosSensiveis.Clear();
            opts.NomesParametrosSensiveis.Add("matricula");
            opts.ValorMascarado = "[hidden]";
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        QueryStringMasker masker = provider.GetRequiredService<QueryStringMasker>();

        masker.Mascarar(new QueryString("?matricula=98765&cpf=123"))
            .Should().Be("?matricula=[hidden]&cpf=123");
    }

    [Fact]
    public void AddRequestLogging_ComConfiguration_DeveMesclarDefaultsComValoresBindados()
    {
        // Binder do IConfiguration agrega às listas pré-populadas (semântica
        // "defaults como piso, config amplia"). Isso impede que um appsettings
        // acidentalmente remova `cpf` da proteção — o máximo que config pode
        // fazer é ampliar o conjunto protegido.
        Dictionary<string, string?> valores = new()
        {
            [$"{RequestLoggingOptions.SectionName}:NomesParametrosSensiveis:0"] = "documento",
            [$"{RequestLoggingOptions.SectionName}:NomesParametrosSensiveis:1"] = "rg",
            [$"{RequestLoggingOptions.SectionName}:ValorMascarado"] = "[REDACTED]",
        };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        ServiceCollection services = new();

        services.AddRequestLogging(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        RequestLoggingOptions options = provider.GetRequiredService<IOptions<RequestLoggingOptions>>().Value;

        options.NomesParametrosSensiveis.Should()
            .BeEquivalentTo(["cpf", "email", "senha", "password", "token", "documento", "rg"]);
        options.ValorMascarado.Should().Be("[REDACTED]");
    }

    [Fact]
    public void AddRequestLogging_ComEntradasDuplicadasOuEmBranco_DeveNormalizarLista()
    {
        ServiceCollection services = new();

        services.AddRequestLogging(configure: opts =>
        {
            // "CPF" duplica defaults (case-insensitive), "  " é entrada em branco,
            // "rg" é válida. PostConfigure deve manter apenas entradas normalizadas.
            opts.NomesParametrosSensiveis.Add("CPF");
            opts.NomesParametrosSensiveis.Add("  ");
            opts.NomesParametrosSensiveis.Add("rg");
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        RequestLoggingOptions options = provider.GetRequiredService<IOptions<RequestLoggingOptions>>().Value;

        options.NomesParametrosSensiveis.Should()
            .BeEquivalentTo(["cpf", "email", "senha", "password", "token", "rg"]);
    }

    [Fact]
    public void AddRequestLogging_ComPrefixoSilenciadoInvalido_DeveFalharValidacao()
    {
        ServiceCollection services = new();

        services.AddRequestLogging(configure: opts =>
            opts.PrefixosSilenciados.Add("health"));   // falta '/' inicial

        using ServiceProvider provider = services.BuildServiceProvider();
        Func<RequestLoggingOptions> acao = () => provider.GetRequiredService<IOptions<RequestLoggingOptions>>().Value;

        acao.Should().Throw<OptionsValidationException>()
            .WithMessage("*PrefixosSilenciados*");
    }

    [Fact]
    public void AddRequestLogging_ComListaVazia_DeveFalharValidacaoAoResolverOptions()
    {
        // Proteção crítica: appsettings.json que zere a lista de parâmetros
        // sensíveis equivale a desabilitar masking de PII. Falhar cedo
        // (na inicialização) evita descobrir o problema só quando o vazamento
        // já chegou no sink de logs em produção.
        ServiceCollection services = new();

        services.AddRequestLogging(configure: opts => opts.NomesParametrosSensiveis.Clear());

        using ServiceProvider provider = services.BuildServiceProvider();
        Func<RequestLoggingOptions> acao = () => provider.GetRequiredService<IOptions<RequestLoggingOptions>>().Value;

        acao.Should().Throw<OptionsValidationException>()
            .WithMessage("*NomesParametrosSensiveis*");
    }

    [Fact]
    public void AddRequestLogging_ComValorMascaradoVazio_DeveFalharValidacao()
    {
        ServiceCollection services = new();

        services.AddRequestLogging(configure: opts => opts.ValorMascarado = string.Empty);

        using ServiceProvider provider = services.BuildServiceProvider();
        Func<RequestLoggingOptions> acao = () => provider.GetRequiredService<IOptions<RequestLoggingOptions>>().Value;

        acao.Should().Throw<OptionsValidationException>()
            .WithMessage("*ValorMascarado*");
    }

    [Fact]
    public void AddRequestLogging_ComServicesNulo_DeveLancarArgumentNullException()
    {
        Func<IServiceCollection> acao = () => RequestLoggingServiceCollectionExtensions.AddRequestLogging(null!);

        acao.Should().Throw<ArgumentNullException>();
    }
}
