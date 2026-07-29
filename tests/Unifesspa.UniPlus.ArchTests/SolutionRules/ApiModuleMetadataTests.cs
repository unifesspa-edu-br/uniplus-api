namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Reflection;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

using Unifesspa.UniPlus.Infrastructure.Core.Routing;
using Unifesspa.UniPlus.Portal.API.Controllers;

public sealed class ApiModuleMetadataTests
{
    private const string UniPlusAssemblyPrefix = "Unifesspa.UniPlus.";
    private const string ApiAssemblySuffix = ".API";

    private static readonly Assembly[] ApiAssemblies = DiscoverApiAssemblies();

    [Fact]
    public void ApiAssemblies_DeclareSingleModuleName()
    {
        foreach (Assembly assembly in ApiAssemblies)
        {
            assembly.GetCustomAttributes<ApiModuleAttribute>()
                .Should().ContainSingle()
                .Which.Name.Should().NotBeNullOrWhiteSpace(
                    $"o assembly {assembly.GetName().Name} deve declarar seu contrato público de módulo");
        }
    }

    [Fact]
    public void ApiAssemblies_ModuleNamesAreUnique()
    {
        IEnumerable<string> names = ApiAssemblies
            .Select(ApiModuleMetadata.GetRequiredName);

        names.Should().OnlyHaveUniqueItems(
            "dois assemblies de API não podem disputar o mesmo prefixo e documento OpenAPI");
    }

    [Fact]
    public void ModuleRoutePrefixConvention_PortalPing_CombinesAssemblyModuleAndControllerRoute()
    {
        TypeInfo controllerType = typeof(PingController).GetTypeInfo();
        RouteAttribute route = controllerType.GetCustomAttribute<RouteAttribute>()
            ?? throw new InvalidOperationException("PingController deve declarar o recurso relativo.");
        var controller = new ControllerModel(
            controllerType,
            controllerType.GetCustomAttributes(inherit: true).ToArray());
        controller.Selectors.Add(new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel(route),
        });

        new ModuleRoutePrefixConvention().Apply(controller);

        controller.Selectors.Should().ContainSingle()
            .Which.AttributeRouteModel!.Template.Should().Be("api/portal/ping");
    }

    [Fact]
    public void ModuleRoutePrefixConvention_SelectorWithoutRoute_UsesAssemblyModulePrefix()
    {
        TypeInfo controllerType = typeof(PingController).GetTypeInfo();
        var controller = new ControllerModel(controllerType, []);
        controller.Selectors.Add(new SelectorModel());

        new ModuleRoutePrefixConvention().Apply(controller);

        controller.Selectors.Should().ContainSingle()
            .Which.AttributeRouteModel!.Template.Should().Be("api/portal");
    }

    private static Assembly[] DiscoverApiAssemblies()
    {
        string searchPattern = $"{UniPlusAssemblyPrefix}*{ApiAssemblySuffix}.dll";
        Assembly[] assemblies = [.. Directory
            .EnumerateFiles(AppContext.BaseDirectory, searchPattern, SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal)
            .Select(static name => Assembly.Load(
                new AssemblyName(name
                    ?? throw new InvalidOperationException(
                        "Arquivo de assembly de API sem nome."))))];

        assemblies.Should().NotBeEmpty(
            "o projeto ArchTests referencia por glob todos os projetos Unifesspa.UniPlus.*.API");

        return assemblies;
    }
}
