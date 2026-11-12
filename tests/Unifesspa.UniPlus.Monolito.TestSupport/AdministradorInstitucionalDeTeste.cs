namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Dá ao administrador anônimo dos testes de endpoint a concessão de todas as permissões de um módulo, no escopo da
/// instituição, do jeito que a produção dá: grava a concessão de um perfil, e a do Representante do controlador quando o
/// módulo tem permissão reservada à governança. O token dos testes só identifica a pessoa,
/// então sem isto cada teste que opera um cadastro precisaria conceder à mão o perfil do módulo.
/// </summary>
/// <remarks>
/// Só vale para a requisição que traz o papel <c>plataforma-admin</c> e nenhuma identidade nomeada: as personas dos
/// cenários de acesso (que enviam o seu <c>sub</c>) e quem não traz o papel ficam como o token as descreve, sem
/// concessão alguma.
/// </remarks>
public static class AdministradorInstitucionalDeTeste
{
    private const string PapelDeAdministrador = "plataforma-admin";
    private const string SubDoAdministradorAnonimo = "test-user-id";

    public static IServiceCollection AdicionarAdministradorInstitucionalDeTeste<TPermissao>(this IServiceCollection services)
        where TPermissao : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IStartupFilter, ConcessaoInstitucional<TPermissao>>();
        return services;
    }

    private sealed class ConcessaoInstitucional<TPermissao> : IStartupFilter
        where TPermissao : struct, Enum
    {
        private readonly Lock _trava = new();
        private Task? _concessao;

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (contexto, proximo) =>
            {
                if (EhOAdministradorAnonimo(contexto.Request))
                {
                    await ConcederUmaVezAsync(contexto.RequestServices);
                }

                await proximo();
            });
            next(app);
        };

        private static bool EhOAdministradorAnonimo(HttpRequest requisicao) =>
            !requisicao.Headers.ContainsKey(TestAuthHandler.UserIdHeader)
            && requisicao.Headers[TestAuthHandler.RolesHeader].ToString()
                .Split(',', StringSplitOptions.TrimEntries).Contains(PapelDeAdministrador);

        private Task ConcederUmaVezAsync(IServiceProvider services)
        {
            lock (_trava)
            {
                return _concessao ??= ConcederAsync(services);
            }
        }

        // O perfil comum só reúne as permissões não reservadas; as reservadas à governança de outro módulo chegam, como em
        // produção, pela concessão do perfil de Representante do controlador, o único que as reúne (ADR-0162).
        private static async Task ConcederAsync(IServiceProvider services)
        {
            DefinicaoDePermissao[] todas = [.. CatalogoDePermissoes<TPermissao>.Todas()];
            await ConcessoesDeTeste.ConcederPermissoesAsync(
                services, SubDoAdministradorAnonimo, [.. todas.Where(static d => !d.ReservadaAGovernanca).Select(static d => d.Codigo)]);
            if (todas.Any(static d => d.ReservadaAGovernanca))
            {
                await ConcessoesDeTeste.ConcederGovernancaAsync(services, SubDoAdministradorAnonimo, TipoDeGovernanca.RepresentanteDoControlador);
            }
        }
    }
}
