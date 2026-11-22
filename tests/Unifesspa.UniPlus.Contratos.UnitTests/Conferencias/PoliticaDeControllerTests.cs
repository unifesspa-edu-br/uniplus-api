namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using AwesomeAssertions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Contratos.UnitTests.Hosting;

/// <summary>A política de controller identifica a pessoa; a decisão de negócio cabe ao ponto único (ADR-0078).</summary>
public sealed class PoliticaDeControllerTests
{
    [Fact]
    public void Politicas_DasAcoes_SaoSomenteDeIdentidade()
    {
        IAuthorizeData[] politicas = [.. HostsDoProdutoIniciados.Executaveis
            .SelectMany(executavel => HostsDoProdutoIniciados.Obter(executavel).ProvedorDeServicos
                .GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items)
            .SelectMany(static acao => acao.EndpointMetadata.OfType<IAuthorizeData>())];

        politicas.Should().Contain(static politica => politica.Policy == null);
        politicas.Should().Contain(static politica => politica.Policy == "Instalacao");
        PoliticasDeNegocio(politicas).Should().BeEmpty();
    }

    [Fact]
    public void Politicas_ComRegraDeNegocio_SaoRecusadas()
    {
        IAuthorizeData[] politicas = [new AuthorizeAttribute(), new AuthorizeAttribute("Instalacao"),
            new AuthorizeAttribute("PodeAprovarEdital"), new AuthorizeAttribute("CoordenadorDaUnidade")];

        PoliticasDeNegocio(politicas).Should().BeEquivalentTo("PodeAprovarEdital", "CoordenadorDaUnidade");
    }

    private static IEnumerable<string> PoliticasDeNegocio(IEnumerable<IAuthorizeData> politicas) =>
        politicas.Select(static politica => politica.Policy)
            .Where(static politica => !string.IsNullOrEmpty(politica) && politica != "Instalacao")
            .Select(static politica => politica!);
}
