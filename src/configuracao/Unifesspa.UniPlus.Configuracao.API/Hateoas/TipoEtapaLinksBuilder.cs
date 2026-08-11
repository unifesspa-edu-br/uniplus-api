namespace Unifesspa.UniPlus.Configuracao.API.Hateoas;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Configuracao.API.Controllers;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;

internal sealed class TipoEtapaLinksBuilder : IResourceLinksBuilder<TipoEtapaDto>
{
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TipoEtapaLinksBuilder(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    {
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyDictionary<string, string> Build(TipoEtapaDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        HttpContext? context = _httpContextAccessor.HttpContext;
        const string controller = "TiposEtapa";
        string self = Resolver(context, nameof(TiposEtapaController.ObterPorId), controller, new { id = dto.Id });
        string collection = Resolver(context, nameof(TiposEtapaController.Listar), controller, null);
        return new Dictionary<string, string>(StringComparer.Ordinal) { ["self"] = self, ["collection"] = collection };
    }

    private string Resolver(HttpContext? context, string action, string controller, object? values) =>
        (context is not null
            ? _linkGenerator.GetPathByAction(context, action, controller, values)
            : _linkGenerator.GetPathByAction(action, controller, values))
        ?? throw new InvalidOperationException($"LinkGenerator não resolveu a rota {action}.");
}
