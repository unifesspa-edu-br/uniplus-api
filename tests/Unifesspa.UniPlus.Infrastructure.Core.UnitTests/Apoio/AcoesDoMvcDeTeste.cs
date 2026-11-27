namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio;

using System.Reflection;

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

internal sealed class AcoesDoMvcDeTeste : IDisposable
{
    private readonly ServiceProvider _provider;

    public AcoesDoMvcDeTeste(params Type[] controllers)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddMvcCore().AddApiExplorer().ConfigureApplicationPartManager(manager =>
        {
            manager.ApplicationParts.Clear();
            manager.ApplicationParts.Add(new ControllersInformados(controllers));
        });
        _provider = services.BuildServiceProvider();
    }

    public IActionDescriptorCollectionProvider Acoes => _provider.GetRequiredService<IActionDescriptorCollectionProvider>();

    public IApiDescriptionGroupCollectionProvider Descricoes => _provider.GetRequiredService<IApiDescriptionGroupCollectionProvider>();

    public ControllerActionDescriptor Acao(Type controller, string nome) => Acoes.ActionDescriptors.Items
        .OfType<ControllerActionDescriptor>().Single(a => a.ControllerTypeInfo.AsType() == controller && a.ActionName == nome);

    public ApiDescription Descricao(Type controller, string nome, string metodo) => _provider
        .GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items
        .SelectMany(grupo => grupo.Items)
        .Single(d => d.ActionDescriptor == Acao(controller, nome) && d.HttpMethod == metodo);

    public void Dispose() => _provider.Dispose();

    internal sealed class ControllersInformados(Type[] controllers) : ApplicationPart, IApplicationPartTypeProvider
    {
        public override string Name => nameof(ControllersInformados);
        public IEnumerable<TypeInfo> Types => controllers.Select(static controller => controller.GetTypeInfo());
    }
}
