namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// O pedido cujo perfil passou a exigir o termo depois da criação, e que por isso não tem versão fixada, é devolvido na
/// aprovação do gestor, e o solicitante é avisado para refazê-lo (ADR-0162, UNI-REQ-0201). O pedido é gravado direto no
/// banco, sem termo, como o pedido criado antes de o perfil passar a dar acesso a dado pessoal.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class DevolucaoPorTermoExigidoNoHostTests : PedidosDeConcessaoTestBase
{
    public DevolucaoPorTermoExigidoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "O pedido devolvido por exigir o termo avisa o solicitante com o link do pedido, sem dado pessoal")]
    public async Task DevolvidoPorTermoExigido_AvisaOSolicitante()
    {
        (Guid perfil, Guid unidade) = await PerfilEUnidadeAsync(sensivel: true);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await PedidoSemTermoAsync(perfil, unidade);

        await ConferirRecusaAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.Conflict, "termo_exigido");

        JsonElement aviso = await AguardarAvisoAsync(Betina, id, "passou a exigir o termo de responsabilidade");
        aviso.GetProperty("Text").GetString().Should().Contain($"/acessos/pedidos/{id}").And.NotContain(Betina.Nome).And.NotContain(Justificativa);
        (await SituacaoAsync(diogo, id)).Should().Be("devolvido");
        (await TiposDaTrilhaAsync(diogo, id)).Should().Contain("termo_exigido");
    }

    // O pedido de Betina para si mesma, aguardando o gestor, sem versão do termo fixada.
    private async Task<Guid> PedidoSemTermoAsync(Guid perfilId, Guid unidade)
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        IServiceProvider servicos = escopo.ServiceProvider;
        Perfil perfil = (await servicos.GetRequiredService<IConcessaoRepository>().ObterParaConcederAsync(perfilId))!;
        PessoaNoToken betina = new(new Identidade(TestAuthHandler.DefaultIssuer, Betina.Sub.ToString(), Betina.Nome), Betina.Nome, Betina.Email, true);
        PedidoDeConcessao pedido = PedidoDeConcessao.Criar(
            new PartesDoPedido(betina, null), perfil, EscopoDaConcessao.DaUnidade(unidade), new DeclaracaoDoPedido(Justificativa),
            servicos.GetRequiredService<IDominiosInstitucionais>(), servicos.GetRequiredService<ICatalogoDePermissoesConsultavel>(), DateTimeOffset.UtcNow).Value!;
        await servicos.GetRequiredService<IPedidoDeConcessaoRepository>().AdicionarAsync(pedido);
        await servicos.GetRequiredService<AutorizacaoDbContext>().SaveChangesAsync();

        return pedido.Id;
    }
}
