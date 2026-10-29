namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Dá ao administrador anônimo dos testes de endpoint a concessão que o acesso ao processo exige, do jeito que a
/// produção dá: grava a concessão de um perfil, com escopo na unidade do processo da requisição. O token dos testes só
/// identifica a pessoa, e as permissões do processo valem por unidade — sem isto, cada teste que opera um processo
/// precisaria conceder à mão o perfil na unidade que o seu semeador sorteou.
/// </summary>
/// <remarks>
/// Só vale para a requisição que traz o papel <c>plataforma-admin</c> e nenhuma identidade nomeada: as personas dos
/// cenários de acesso (que enviam o seu <c>sub</c>) e quem não traz o papel ficam como o token as descreve, sem
/// concessão alguma.
/// </remarks>
internal static partial class AdministradorDeProcessosDeTeste
{
    private const string PapelDeAdministrador = "plataforma-admin";
    private const string SubDoAdministradorAnonimo = "test-user-id";

    public static IServiceCollection AdicionarAdministradorDeProcessosDeTeste(this IServiceCollection services)
    {
        services.AddSingleton<PerfilDoAdministrador>();
        services.AddSingleton<Microsoft.AspNetCore.Hosting.IStartupFilter, ConcessaoNaUnidade>();
        return services;
    }

    [GeneratedRegex(@"/processos-seletivos(?:/(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}))?(?:/|$)")]
    private static partial Regex RotaDoProcesso();

    private sealed class PerfilDoAdministrador
    {
        private readonly Lock _trava = new();
        private Task<Guid>? _perfilId;

        public Task<Guid> ObterAsync(IServiceProvider services)
        {
            lock (_trava)
            {
                return _perfilId ??= ConcessoesDeTeste.CriarPerfilAsync(
                    services,
                    PermissaoSelecao.ProcessosConfigurar.Codigo,
                    PermissaoSelecao.ProcessosPublicar.Codigo,
                    PermissaoSelecao.ProcessosAcompanhar.Codigo);
            }
        }
    }

    private sealed class ConcessaoNaUnidade : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (contexto, proximo) =>
            {
                if (EhOAdministradorAnonimo(contexto.Request) && await UnidadeDaRequisicaoAsync(contexto) is { } unidade)
                {
                    Guid perfil = await contexto.RequestServices.GetRequiredService<PerfilDoAdministrador>()
                        .ObterAsync(contexto.RequestServices);
                    await ConcessoesDeTeste.ConcederAsync(
                        contexto.RequestServices, SubDoAdministradorAnonimo, perfil, EscopoDaConcessao.DaUnidade(unidade));
                }

                await proximo();
            });
            next(app);
        };

        private static bool EhOAdministradorAnonimo(HttpRequest requisicao) =>
            !requisicao.Headers.ContainsKey(TestAuthHandler.UserIdHeader)
            && requisicao.Headers[TestAuthHandler.RolesHeader].ToString()
                .Split(',', StringSplitOptions.TrimEntries).Contains(PapelDeAdministrador);

        private static async Task<Guid?> UnidadeDaRequisicaoAsync(HttpContext contexto)
        {
            Match rota = RotaDoProcesso().Match(contexto.Request.Path.Value ?? string.Empty);
            if (!rota.Success)
            {
                return null;
            }

            if (rota.Groups["id"].Success)
            {
                Guid processoId = Guid.Parse(rota.Groups["id"].Value);
                await using AsyncServiceScope escopo = contexto.RequestServices.CreateAsyncScope();
                return await escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>().ProcessosSeletivos
                    .AsNoTracking()
                    .Where(p => p.Id == processoId)
                    .Select(p => (Guid?)p.UnidadeAdministradoraOrigemId)
                    .FirstOrDefaultAsync();
            }

            // A criação nomeia a unidade no corpo, que o endpoint ainda vai ler.
            if (!HttpMethods.IsPost(contexto.Request.Method))
            {
                return null;
            }

            contexto.Request.EnableBuffering();
            using JsonDocument corpo = await JsonDocument.ParseAsync(contexto.Request.Body);
            contexto.Request.Body.Position = 0;
            return corpo.RootElement.ValueKind == JsonValueKind.Object
                && corpo.RootElement.TryGetProperty("unidadeAdministradoraOrigemId", out JsonElement unidade)
                && unidade.TryGetGuid(out Guid unidadeId)
                    ? unidadeId
                    : null;
        }
    }
}
