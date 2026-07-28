using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Unifesspa.UniPlus.Infrastructure.Core.Routing;

public sealed class ModuleRoutePrefixConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        string? groupName = controller.ApiExplorer.GroupName;
        if (groupName is null)
        {
            return;
        }

        var prefix = new AttributeRouteModel(
            new RouteAttribute($"api/{groupName}")
        );

        foreach (SelectorModel selector in controller.Selectors)
        {
            selector.AttributeRouteModel = selector.AttributeRouteModel == null
                ? prefix
                : AttributeRouteModel.CombineAttributeRouteModel(
                    prefix,
                    selector.AttributeRouteModel);
        }
    }
}
