namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Idempotency;

using System.Reflection;
using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.API.Controllers;
using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

internal sealed class CenarioDeIdempotencia
{
    private const string Rota = "/api/configuracao/admin/termos-consentimento/00000000-0000-0000-0000-000000000001/revisar";
    private readonly IServiceProvider _services;
    private readonly string _usuario;
    private readonly string _chave = Guid.NewGuid().ToString();
    private readonly byte[] _corpo = Encoding.UTF8.GetBytes("""{"texto":"corpo de teste"}""");
    private readonly EfCoreIdempotencyStore _store;

    public CenarioDeIdempotencia(IServiceProvider services, string usuario)
    {
        _services = services;
        _usuario = usuario;
        _store = ActivatorUtilities.CreateInstance<EfCoreIdempotencyStore>(services);
        Filter = CriarFiltro(services, _store, usuario);
    }

    public IdempotencyFilter Filter { get; }

    public ResourceExecutingContext CriarRequisicao()
    {
        DefaultHttpContext http = new() { RequestServices = _services };
        http.Request.Method = "POST";
        http.Request.Path = Rota;
        http.Request.Headers["Idempotency-Key"] = _chave;
        http.Request.Body = new MemoryStream(_corpo);
        http.Request.ContentLength = _corpo.Length;
        http.Response.Body = new MemoryStream();
        return new ResourceExecutingContext(new ActionContext(http, new RouteData(), CriarAction()), [], []);
    }

    private static ControllerActionDescriptor CriarAction() => new()
    {
        MethodInfo = typeof(TermosConsentimentoController).GetMethod(nameof(TermosConsentimentoController.MarcarRevisado))!,
        ControllerTypeInfo = typeof(TermosConsentimentoController).GetTypeInfo(),
    };

    public async Task<IdempotencyLookupResult> ConsultarReservaAsync()
    {
        string hash = Convert.ToHexStringLower(SHA256.HashData(_corpo));
        return await _store.LookupAsync($"user:{_usuario}", $"POST {Rota}", _chave, hash, CancellationToken.None);
    }

    private static IdempotencyFilter CriarFiltro(IServiceProvider services, EfCoreIdempotencyStore store, string usuario)
    {
        IUserContext user = Substitute.For<IUserContext>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns(usuario);
        return new IdempotencyFilter(
            store, services.GetRequiredService<IUniPlusEncryptionService>(),
            services.GetRequiredService<IDomainErrorMapper>(), services.GetRequiredService<TimeProvider>(),
            user, services.GetRequiredService<IOptions<IdempotencyOptions>>(),
            services.GetRequiredService<IProblemTypeUriFactory>());
    }
}
