namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Contratos.UnitTests.Hosting;
using Unifesspa.UniPlus.Infrastructure.Core.Routing;

public sealed class ApiModuleMetadataTests
{
    [Fact]
    public void Acoes_UsamPrefixoDoModuloDeclarado()
    {
        ControllerActionDescriptor[] acoes = [.. HostsDoProdutoIniciados.Executaveis
            .SelectMany(executavel => HostsDoProdutoIniciados.Obter(executavel).ProvedorDeServicos
                .GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items)
            .OfType<ControllerActionDescriptor>()];

        acoes.Should().NotBeEmpty();
        acoes.Should().OnlyContain(static acao => acao.AttributeRouteInfo != null
            && acao.AttributeRouteInfo.Template!.StartsWith(
                $"api/{ApiModuleMetadata.GetRequiredName(acao.ControllerTypeInfo.Assembly)}/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AssembliesComAcoes_DeclaramNomesDeModuloUnicos()
    {
        System.Reflection.Assembly[] assemblies = HostsDoProdutoIniciados.Executaveis
            .SelectMany(executavel => HostsDoProdutoIniciados.Obter(executavel).ProvedorDeServicos
                .GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items)
            .OfType<ControllerActionDescriptor>()
            .Select(static acao => acao.ControllerTypeInfo.Assembly)
            .Distinct()
            .ToArray();

        assemblies.Should().NotBeEmpty();
        assemblies.Select(ApiModuleMetadata.GetRequiredName).Should().OnlyHaveUniqueItems();
    }
}
