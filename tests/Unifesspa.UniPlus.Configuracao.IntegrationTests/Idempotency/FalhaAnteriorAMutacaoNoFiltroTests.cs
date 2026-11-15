namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Idempotency;

using System.Reflection;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using Npgsql;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// O filtro de idempotência só libera a chave quando a falha nasceu antes da mutação. A espera esgotada pela trava é
/// diferente: o banco abortou a transação e nada foi gravado, mas repetir a requisição depois da corrida perdida pode agir
/// sobre o que o vencedor mudou, então a recusa fica guardada pela chave, e a repetição recebe a mesma resposta sem executar.
/// Qualquer outra falha pendente mantém a reserva, porque a mutação pode já ter sido gravada.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[Trait("Category", "Integration")]
public sealed class FalhaAnteriorAMutacaoNoFiltroTests(ConfiguracaoEndpointFixture fixture)
{
    [Theory(DisplayName = "A espera esgotada guarda a recusa: 503 sem Retry-After, e a mesma chave recebe a mesma resposta")]
    [InlineData(nameof(Rota.ComDeclaracao))]
    [InlineData(nameof(Rota.SemDeclaracao))]
    public async Task EsperaEsgotada_GuardaARecusa(string acao)
    {
        await using FiltroComFalhaPendente filtro = FiltroComFalhaPendente.Criar(fixture.Factory.Services, acao);

        RespostaObservada primeira = await filtro.ExecutarComFalhaAsync(EsperaEsgotada());
        IdempotencyOutcome depois = await filtro.EstadoDaChaveAsync();
        RespostaObservada repeticao = await filtro.ReenviarSemExecutarAsync();

        depois.Should().Be(IdempotencyOutcome.HitMatch);
        ConferirRecusaSemPrazo(primeira);
        ConferirRecusaSemPrazo(repeticao);
        repeticao.Corpo.Should().Be(primeira.Corpo);
    }

    [Fact(DisplayName = "A espera esgotada embrulhada em outra exceção também guarda a recusa")]
    public async Task EsperaEsgotadaEmbrulhada_GuardaARecusa()
    {
        await using FiltroComFalhaPendente filtro = FiltroComFalhaPendente.Criar(fixture.Factory.Services, nameof(Rota.ComDeclaracao));

        await filtro.ExecutarComFalhaAsync(new InvalidOperationException("envoltório", EsperaEsgotada()));

        (await filtro.EstadoDaChaveAsync()).Should().Be(IdempotencyOutcome.HitMatch);
    }

    [Fact(DisplayName = "A exceção genérica não libera a reserva: a mutação pode já ter sido gravada")]
    public async Task ExcecaoGenerica_MantemAReserva()
    {
        await using FiltroComFalhaPendente filtro = FiltroComFalhaPendente.Criar(fixture.Factory.Services, nameof(Rota.ComDeclaracao));

        await filtro.ExecutarComFalhaAsync(new InvalidOperationException("falha qualquer"));

        (await filtro.EstadoDaChaveAsync()).Should().Be(IdempotencyOutcome.Processing);
    }

    [Fact(DisplayName = "O erro 55P03 do banco, sem a conversão feita na trava do processo, não libera a reserva")]
    public async Task LockNotAvailableSemConversao_MantemAReserva()
    {
        await using FiltroComFalhaPendente filtro = FiltroComFalhaPendente.Criar(fixture.Factory.Services, nameof(Rota.ComDeclaracao));
        PostgresException crua = new("ERROR", "ERROR", "canceling statement due to lock timeout", PostgresErrorCodes.LockNotAvailable);

        await filtro.ExecutarComFalhaAsync(crua);

        (await filtro.EstadoDaChaveAsync()).Should().Be(IdempotencyOutcome.Processing);
    }

    private static EsperaPelaTravaEsgotadaException EsperaEsgotada() =>
        new(new InvalidOperationException("causa"));

    private static void ConferirRecusaSemPrazo(RespostaObservada resposta)
    {
        resposta.Status.Should().Be(StatusCodes.Status503ServiceUnavailable);
        resposta.RetryAfter.Should().BeEmpty();
        using JsonDocument problema = JsonDocument.Parse(resposta.Corpo);
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.concorrencia.espera_esgotada");
        problema.RootElement.GetProperty("detail").GetString().Should().Contain("nova chave de idempotência");
    }

    private sealed record RespostaObservada(int Status, StringValues RetryAfter, string Corpo);

    private sealed class Rota
    {
        [RequiresIdempotencyKey]
        [EmiteEsperaEsgotadaPelaTrava]
        public static void ComDeclaracao()
        {
        }

        [RequiresIdempotencyKey]
        public static void SemDeclaracao()
        {
        }
    }

    private sealed class FiltroComFalhaPendente : IAsyncDisposable
    {
        private const string Caminho = "/api/selecao/processos-seletivos/00000000-0000-0000-0000-000000000001/motivos-de-decisao/00000000-0000-0000-0000-000000000002";
        private const string Usuario = "filtro-falha-anterior-a-mutacao";
        private static readonly byte[] Corpo = Encoding.UTF8.GetBytes("""{"justificativa":"corpo de teste"}""");

        private readonly AsyncServiceScope _escopo;
        private readonly EfCoreIdempotencyStore _store;
        private readonly IdempotencyFilter _filtro;
        private readonly ControllerActionDescriptor _acao;
        private readonly string _chave = Guid.NewGuid().ToString();

        private FiltroComFalhaPendente(AsyncServiceScope escopo, string acao)
        {
            _escopo = escopo;
            _acao = new ControllerActionDescriptor
            {
                MethodInfo = typeof(Rota).GetMethod(acao)!,
                ControllerTypeInfo = typeof(Rota).GetTypeInfo(),
            };
            _store = ActivatorUtilities.CreateInstance<EfCoreIdempotencyStore>(escopo.ServiceProvider);
            _filtro = CriarFiltro(escopo.ServiceProvider, _store);
        }

        internal static FiltroComFalhaPendente Criar(IServiceProvider servicos, string acao) => new(servicos.CreateAsyncScope(), acao);

        /// <summary>Roda o filtro com a falha pendente no <c>next()</c> e devolve a resposta que o cliente recebeu.</summary>
        internal async Task<RespostaObservada> ExecutarComFalhaAsync(Exception falha)
        {
            ActionContext acao = ContextoDaAcao();
            List<IFilterMetadata> filtros = [];

            await _filtro.OnResourceExecutionAsync(
                new ResourceExecutingContext(acao, filtros, []),
                () => Task.FromResult(new ResourceExecutedContext(acao, filtros) { Exception = falha, ExceptionHandled = false }));

            return await Observar(acao.HttpContext);
        }

        /// <summary>Repete a requisição com a mesma chave e o mesmo corpo, falhando se o <c>next()</c> for chamado.</summary>
        internal async Task<RespostaObservada> ReenviarSemExecutarAsync()
        {
            ActionContext acao = ContextoDaAcao();
            ResourceExecutingContext contexto = new(acao, [], []);

            await _filtro.OnResourceExecutionAsync(
                contexto,
                () => throw new InvalidOperationException("A repetição guardada não deveria executar a action."));

            FileContentResult guardada = contexto.Result.Should().BeOfType<FileContentResult>().Subject;
            return new RespostaObservada(
                acao.HttpContext.Response.StatusCode,
                acao.HttpContext.Response.Headers.RetryAfter,
                Encoding.UTF8.GetString(guardada.FileContents));
        }

        internal async Task<IdempotencyOutcome> EstadoDaChaveAsync()
        {
            IdempotencyLookupResult lookup = await _store.LookupAsync(
                $"user:{Usuario}", $"POST {Caminho}", _chave, HashDoCorpo(), CancellationToken.None);
            return lookup.Outcome;
        }

        public ValueTask DisposeAsync() => _escopo.DisposeAsync();

        private static IdempotencyFilter CriarFiltro(IServiceProvider servicos, EfCoreIdempotencyStore store)
        {
            IUserContext usuario = Substitute.For<IUserContext>();
            usuario.IsAuthenticated.Returns(true);
            usuario.UserId.Returns(Usuario);
            return new IdempotencyFilter(
                store,
                servicos.GetRequiredService<IUniPlusEncryptionService>(),
                servicos.GetRequiredService<IDomainErrorMapper>(),
                servicos.GetRequiredService<TimeProvider>(),
                usuario,
                servicos.GetRequiredService<IOptions<IdempotencyOptions>>(),
                servicos.GetRequiredService<IProblemTypeUriFactory>());
        }

        private static string HashDoCorpo() => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Corpo));

        private static async Task<RespostaObservada> Observar(HttpContext http)
        {
            http.Response.Body.Seek(0, SeekOrigin.Begin);
            using StreamReader leitor = new(http.Response.Body);
            return new RespostaObservada(http.Response.StatusCode, http.Response.Headers.RetryAfter, await leitor.ReadToEndAsync());
        }

        private ActionContext ContextoDaAcao()
        {
            DefaultHttpContext http = new() { RequestServices = _escopo.ServiceProvider };
            http.Request.Method = "POST";
            http.Request.Path = Caminho;
            http.Request.Headers["Idempotency-Key"] = _chave;
            http.Request.Body = new MemoryStream(Corpo);
            http.Request.ContentLength = Corpo.Length;
            http.Response.Body = new MemoryStream();
            http.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(_acao), "rota-de-teste"));
            return new ActionContext(http, new RouteData(), _acao);
        }
    }
}
