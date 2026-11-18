namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Idempotency;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

public sealed class IdempotencyFilterTetoDoCorpoTests
{
    private const long TetoGlobal = 50;

    [Theory(DisplayName = "O teto do corpo é o do atributo quando ele declara um, senão o global; o tamanho exato passa e um byte a mais recusa")]
    [InlineData(nameof(EndpointsDeTeste.SemTeto), 50, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(nameof(EndpointsDeTeste.SemTeto), 51, StatusCodes.Status413PayloadTooLarge)]
    [InlineData(nameof(EndpointsDeTeste.ComTetoMaiorQueOGlobal), 200, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(nameof(EndpointsDeTeste.ComTetoMaiorQueOGlobal), 201, StatusCodes.Status413PayloadTooLarge)]
    [InlineData(nameof(EndpointsDeTeste.ComTetoMenorQueOGlobal), 10, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(nameof(EndpointsDeTeste.ComTetoMenorQueOGlobal), 11, StatusCodes.Status413PayloadTooLarge)]
    public async Task Corpo_ComparadoAoTetoDoEndpoint(string endpoint, int tamanhoDoCorpo, int statusEsperado)
    {
        ResourceExecutingContext contexto = Contexto(endpoint, tamanhoDoCorpo);

        await Filtro().OnResourceExecutionAsync(contexto, static () => throw new InvalidOperationException("A action não deve rodar."));

        contexto.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(statusEsperado);
    }

    private static IdempotencyFilter Filtro() => new(
        StoreComEntradaDeCorpoDiferente(),
        Substitute.For<IUniPlusEncryptionService>(),
        new MapeadorDeTeste(),
        TimeProvider.System,
        UsuarioAutenticado(),
        Options.Create(new IdempotencyOptions { MaxBodyBytes = TetoGlobal }),
        Substitute.For<IProblemTypeUriFactory>());

    private static IIdempotencyStore StoreComEntradaDeCorpoDiferente()
    {
        IIdempotencyStore store = Substitute.For<IIdempotencyStore>();
        store.LookupAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(new IdempotencyLookupResult(IdempotencyOutcome.HitMismatch, Entry: null));
        return store;
    }

    private static IUserContext UsuarioAutenticado()
    {
        IUserContext usuario = Substitute.For<IUserContext>();
        usuario.IsAuthenticated.Returns(true);
        usuario.UserId.Returns("usuario-de-teste");
        return usuario;
    }

    private static ResourceExecutingContext Contexto(string endpoint, int tamanhoDoCorpo)
    {
        DefaultHttpContext http = new();
        http.Request.Method = HttpMethods.Post;
        http.Request.Path = "/api/teste";
        http.Request.Headers["Idempotency-Key"] = "chave-de-teste";
        http.Request.Body = new MemoryStream(new byte[tamanhoDoCorpo]);
        http.Request.ContentLength = tamanhoDoCorpo;

        ControllerActionDescriptor acao = new()
        {
            MethodInfo = typeof(EndpointsDeTeste).GetMethod(endpoint)!,
            ControllerTypeInfo = typeof(EndpointsDeTeste).GetTypeInfo(),
        };

        return new ResourceExecutingContext(new ActionContext(http, new RouteData(), acao), [], []);
    }

    private static class EndpointsDeTeste
    {
        [RequiresIdempotencyKey]
        public static void SemTeto()
        {
        }

        [RequiresIdempotencyKey(MaxBodyBytes = 200)]
        public static void ComTetoMaiorQueOGlobal()
        {
        }

        [RequiresIdempotencyKey(MaxBodyBytes = 10)]
        public static void ComTetoMenorQueOGlobal()
        {
        }
    }

    private sealed class MapeadorDeTeste : IDomainErrorMapper
    {
        public bool TryGetMapping(string code, [MaybeNullWhen(false)] out DomainErrorMapping mapping)
        {
            mapping = code switch
            {
                IdempotencyDomainErrorCodes.BodyMuitoGrande => new DomainErrorMapping(StatusCodes.Status413PayloadTooLarge, "uniplus.idempotency.body_muito_grande", "Corpo grande demais"),
                IdempotencyDomainErrorCodes.BodyMismatch => new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.idempotency.body_mismatch", "Corpo diferente"),
                _ => null,
            };
            return mapping is not null;
        }

        public string GetProblemTypeUri(string code) => $"https://catalogo.exemplo/{code}";
    }
}
