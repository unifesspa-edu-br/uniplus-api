namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;

/// <summary>
/// Os três perfis de governança, inclusive o de Encarregado de dados, são imutáveis pelo endpoint real: nova versão e
/// desativação são recusadas com 422, e o perfil segue ativo. Diogo Souza opera o cadastro.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class ImutabilidadeDosPerfisDeGovernancaTests : PerfilEndpointTestBase
{
    public ImutabilidadeDosPerfisDeGovernancaTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    public static TheoryData<Guid, string> PerfisDeGovernanca => new()
    {
        { PerfisDeSistema.GestorDeAcessosId, "gestor-de-acessos" },
        { PerfisDeSistema.RepresentanteDoControladorId, "representante-do-controlador" },
        { PerfisDeSistema.EncarregadoDeDadosId, "encarregado-de-dados" },
    };

    [Theory(DisplayName = "Todo perfil de governança recusa nova versão e desativação com 422 e segue ativo")]
    [MemberData(nameof(PerfisDeGovernanca))]
    public async Task PerfilDeGovernanca_RecusaVersaoEDesativacao(Guid id, string governanca)
    {
        using HttpClient diogo = await ClienteDoGestorAsync();

        using HttpResponseMessage versao = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [ConsultarCatalogo]));
        using HttpResponseMessage desativacao = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/desativacao", new { motivo = "Desativado para o teste." });

        (await versao.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.governanca_imutavel");
        (await desativacao.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.governanca_imutavel");
        using JsonDocument perfil = await LerAsync(diogo, id);
        perfil.RootElement.GetProperty("governanca").GetString().Should().Be(governanca);
        perfil.RootElement.GetProperty("ativo").GetBoolean().Should().BeTrue();
    }
}
