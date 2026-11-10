namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>Montagens comuns às suítes da suspensão, da desativação e da trilha da contenção do perfil pelo host real.</summary>
public abstract class ContencaoDoPerfilNoHostTestBase : AprovacaoDaVersaoNoHostTestBase
{
    private protected const string Catalogo = "autorizacao:permissoes:consultar-catalogo";
    private protected const string Motivo = "Uso indevido apontado pela gestão.";
    private protected const string AssuntoDaSuspensao = "Perfil de acesso suspenso";

    private protected ContencaoDoPerfilNoHostTestBase(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    private protected async Task PrepararAsync()
    {
        await ResetarAsync();
        await ConcessoesDeTeste.RemoverDaPessoaAsync(Fixture.Factory.Services, Leandro.Sub.ToString());
        await ConcessoesDeTeste.RemoverDaPessoaAsync(Fixture.Factory.Services, Betina.Sub.ToString());
    }

    /// <summary>Perfil institucional em vigor concedido a Leandro, sem permissão protegida: a retomada pede só a gestão.</summary>
    private protected async Task<Guid> PerfilComumComConcessaoAsync(HttpClient gestor)
    {
        Envio envio = await PerfilVigenteAsync(gestor, Catalogo);
        await ConcessoesDeTeste.ConcederAsync(
            Fixture.Factory.Services, Leandro.Sub.ToString(), envio.PerfilId, EscopoDaConcessao.DaInstituicao(), email: Leandro.Email);

        return envio.PerfilId;
    }

    /// <summary>Perfil de unidade em vigor com permissão sensível, concedido a Leandro: a retomada pede a gestão e a proteção de dados.</summary>
    private protected Task<Guid> PerfilProtegidoComConcessaoAsync(HttpClient gestor) =>
        PerfilDeUnidadeComConcessaoAsync(gestor, Consultar, Sensivel);

    /// <summary>Perfil de unidade em vigor com as permissões dadas, concedido a Leandro.</summary>
    private protected async Task<Guid> PerfilDeUnidadeComConcessaoAsync(HttpClient gestor, params string[] permissoes)
    {
        Envio envio = await PerfilVigenteAsync(gestor, permissoes);
        await SemearConcessaoAsync(Leandro, envio.PerfilId, EscopoDaConcessao.DaUnidade(await SemearUnidadeAsync()));

        return envio.PerfilId;
    }

    /// <summary>Propõe uma versão só com remoção, que vale na hora: o perfil passa a ter apenas as permissões que permanecem.</summary>
    private protected static async Task<HttpResponseMessage> RemoverPermissoesAsync(HttpClient cliente, Guid perfilId, params string[] permanecem)
    {
        string nome = (await PerfilAsync(cliente, perfilId)).GetProperty("nome").GetString()!;

        return await EnviarAsync(cliente, HttpMethod.Post, $"{RotaPerfis}/{perfilId}/versoes", DadosComRemocao(nome, Motivo, permanecem));
    }

    /// <summary>As permissões que a decisão de acesso dá a Leandro pela concessão do perfil: vazias enquanto o perfil está suspenso.</summary>
    private protected async Task<string[]> PermissoesDaConcessaoAsync(Guid perfilId, Persona? titular = null)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        IReadOnlyList<ConcessaoAtiva> concessoes = await escopo.ServiceProvider.GetRequiredService<IConcessoesAtivasDoSujeito>()
            .ListarAsync(UsuarioRef.From(TestAuthHandler.DefaultIssuer, (titular ?? Leandro).Sub.ToString()).Value!, CancellationToken.None);

        return [.. concessoes.Single(c => c.PerfilId == perfilId).Permissoes];
    }

    private protected static Task<HttpResponseMessage> SuspenderAsync(HttpClient cliente, Guid perfilId, string? motivo = Motivo, string? chave = null) =>
        EnviarAsync(cliente, HttpMethod.Post, $"{RotaPerfis}/{perfilId}/suspensao", new { motivo }, chave);

    private protected static Task<HttpResponseMessage> RetomarAsync(HttpClient cliente, Guid perfilId, string? etapa, string? motivo = Motivo, string? chave = null) =>
        EnviarAsync(cliente, HttpMethod.Post, $"{RotaPerfis}/{perfilId}/suspensao/retomada", new { etapa, motivo }, chave);

    private protected static Task<HttpResponseMessage> DesativarAsync(HttpClient cliente, Guid perfilId, string? motivo = Motivo) =>
        EnviarAsync(cliente, HttpMethod.Post, $"{RotaPerfis}/{perfilId}/desativacao", new { motivo });

    private protected static Task<HttpResponseMessage> ReativarAsync(HttpClient cliente, Guid perfilId, string? motivo = Motivo) =>
        EnviarAsync(cliente, HttpMethod.Post, $"{RotaPerfis}/{perfilId}/reativacao", new { motivo });

    private protected static async Task SuspenderComSucessoAsync(HttpClient cliente, Guid perfilId)
    {
        using HttpResponseMessage resposta = await SuspenderAsync(cliente, perfilId);
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
    }

    private protected static async Task<JsonElement> RetomarComSucessoAsync(HttpClient cliente, Guid perfilId, string etapa)
    {
        using HttpResponseMessage resposta = await RetomarAsync(cliente, perfilId, etapa);

        return await CorpoAsync(resposta, HttpStatusCode.OK);
    }

    private protected static Task<JsonElement> TrilhaAsync(HttpClient cliente, Guid perfilId) =>
        LerAsync(cliente, $"{RotaPerfis}/{perfilId}/contencao");

    private protected static async Task<string[]> TiposDaContencaoAsync(HttpClient cliente, Guid perfilId) =>
        [.. (await TrilhaAsync(cliente, perfilId)).EnumerateArray().Select(static r => r.GetProperty("tipo").GetString()!)];

    /// <summary>O status que o endpoint de consulta do catálogo devolve a quem o pede: 200 com a permissão vigente, 403 sem ela.</summary>
    private protected static async Task<HttpStatusCode> ConsultarCatalogoAsync(HttpClient cliente)
    {
        using HttpResponseMessage resposta = await cliente.GetAsync(new Uri("/api/autorizacao/permissoes", UriKind.Relative));

        return resposta.StatusCode;
    }

    private protected static void DescartarTodas(HttpResponseMessage[] respostas)
    {
        foreach (HttpResponseMessage resposta in respostas)
        {
            resposta.Dispose();
        }
    }

    private protected async Task<IReadOnlyList<string>> TitularesAsync(string permissao)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<ITitularesDaPermissao>().ListarEmailsAsync(permissao);
    }
}
