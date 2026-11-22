using System.Reflection;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

using Unifesspa.UniPlus.Infrastructure.Core.Routing;

[assembly: ApiModule("teste")]

namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Routing;

public sealed class ModuleRoutePrefixConventionTests
{
    [Fact]
    public void Apply_ComRota_CombinaPrefixoERecurso()
    {
        ControllerModel controller = new(typeof(ControllerDeExemplo).GetTypeInfo(), []);
        controller.Selectors.Add(new SelectorModel { AttributeRouteModel = new AttributeRouteModel(new RouteAttribute("exemplo")) });

        new ModuleRoutePrefixConvention().Apply(controller);

        controller.Selectors.Should().ContainSingle().Which.AttributeRouteModel!.Template.Should().Be("api/teste/exemplo");
    }

    [Fact]
    public void Apply_SemRota_UsaPrefixoDoModulo()
    {
        ControllerModel controller = new(typeof(ControllerDeExemplo).GetTypeInfo(), []);
        controller.Selectors.Add(new SelectorModel());

        new ModuleRoutePrefixConvention().Apply(controller);

        controller.Selectors.Should().ContainSingle().Which.AttributeRouteModel!.Template.Should().Be("api/teste");
    }

    private sealed class ControllerDeExemplo : ControllerBase;
}
