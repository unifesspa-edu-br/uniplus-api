namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A janela vencida e ainda não materializada diante de quem não tem acesso: a tentativa recusada não grava o fim da
/// janela, que só a operação de quem pode (ou a rotina) materializa.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class JanelaVencidaNoHostTests : JanelaDeMigracaoNoHostTestBase
{
    private static readonly TimeSpan DepoisDoPrazo = TimeSpan.FromHours(73);

    public JanelaVencidaNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A aprovação de pedido sem permissão sobre perfil com janela vencida é recusada e não materializa a janela")]
    public async Task AprovacaoSemPermissao_NaoMaterializaAJanela()
    {
        (Guid perfilId, Guid pedido) = await PedidoEmPerfilComJanelaAsync();
        Relogio.Avancar(DepoisDoPrazo);
        using HttpClient semPermissao = Cliente(Leandro);

        using HttpResponseMessage resposta = await EnviarAsync(semPermissao, HttpMethod.Post, $"{Rota}/{pedido}/aprovacoes");

        resposta.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        (await EncerradaEmAsync(perfilId)).Should().BeNull();
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        using HttpResponseMessage comPermissao = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{pedido}/aprovacoes");
        comPermissao.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        (await EncerradaEmAsync(perfilId)).Should().NotBeNull();
    }

    // O pedido nasce antes da janela, que a janela aberta o impediria; a troca de composição abre a janela como a produção.
    private async Task<(Guid PerfilId, Guid Pedido)> PedidoEmPerfilComJanelaAsync()
    {
        await ResetarAsync();
        Guid perfilId = await SemearPerfilAsync("Perfil com janela", Consultar);
        using HttpClient betina = Cliente(Betina);
        Guid pedido = await CriarAsync(betina, perfilId, await SemearUnidadeAsync(), null);
        await ConcessoesDeTeste.ConcederAsync(
            Fixture.Factory.Services, Leandro.Sub.ToString(), perfilId, EscopoDaConcessao.DaUnidade(Guid.CreateVersion7()), email: Leandro.Email);
        await TrocarComposicaoAsync(perfilId, Consultar, Sensivel);

        return (perfilId, pedido);
    }

    private async Task<DateTimeOffset?> EncerradaEmAsync(Guid perfilId)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<AutorizacaoDbContext>().Database
            .SqlQuery<DateTimeOffset?>($"SELECT encerrada_em AS \"Value\" FROM autorizacao.janelas_de_migracao WHERE perfil_id = {perfilId}")
            .SingleAsync();
    }
}
