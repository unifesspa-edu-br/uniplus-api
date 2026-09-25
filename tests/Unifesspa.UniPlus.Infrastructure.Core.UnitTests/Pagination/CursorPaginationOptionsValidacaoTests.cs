namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

public sealed class CursorPaginationOptionsValidacaoTests
{
    private static IOptions<CursorPaginationOptions> Resolver(string keyName)
    {
        IConfiguration configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection([new($"{CursorPaginationOptions.SectionName}:KeyName", keyName)])
            .Build();
        ServiceCollection services = new();
        services.AddCursorPagination(configuracao);
        return services.BuildServiceProvider().GetRequiredService<IOptions<CursorPaginationOptions>>();
    }

    [Fact]
    public void KeyName_Configurado_ChegaAoEncoder()
    {
        IOptions<CursorPaginationOptions> opcoes = Resolver("uniplus-portal-cursor-aesgcm");

        opcoes.Value.KeyName.Should().Be("uniplus-portal-cursor-aesgcm");
    }

    [Fact]
    public void KeyName_EmBranco_EhRecusado()
    {
        IOptions<CursorPaginationOptions> opcoes = Resolver(" ");

        Action acessar = () => _ = opcoes.Value;

        acessar.Should().Throw<OptionsValidationException>()
            .WithMessage("*KeyName é obrigatório*");
    }
}
