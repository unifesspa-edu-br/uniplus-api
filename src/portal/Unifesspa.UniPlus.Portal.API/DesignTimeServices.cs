namespace Unifesspa.UniPlus.Portal.API;

using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;

/// <summary>
/// Serviços de design-time do <c>dotnet ef</c>, que usa este projeto como startup das migrations do Portal.
/// </summary>
public sealed class DesignTimeServices : IDesignTimeServices
{
    public void ConfigureDesignTimeServices(IServiceCollection serviceCollection) =>
        serviceCollection.ExcluirClassificacaoDoSnapshot();
}
