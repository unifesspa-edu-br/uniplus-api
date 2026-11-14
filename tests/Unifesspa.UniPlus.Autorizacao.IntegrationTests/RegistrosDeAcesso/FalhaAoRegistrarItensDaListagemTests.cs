namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.RegistrosDeAcesso;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Cada listagem grava um registro por item exibido, e a página só sai depois de gravados: com o destino recusando só os
/// registros dos itens, a decisão da listagem passa, mas a listagem responde 503 e nenhum item sai. Cecília Moura é
/// Encarregada de dados e tem um pedido, uma concessão e uma revisão a listar.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class FalhaAoRegistrarItensDaListagemTests
{
    private const string Cecilia = "cecilia.moura";

    private readonly AutorizacaoEndpointFixture _fixture;

    public FalhaAoRegistrarItensDaListagemTests(AutorizacaoEndpointFixture fixture) => _fixture = fixture;

    [Theory(DisplayName = "Com o registro dos itens indisponível, a listagem responde 503 sem entregar item")]
    [InlineData("/api/autorizacao/pedidos-de-concessao?fila=todos")]
    [InlineData("/api/autorizacao/concessoes")]
    [InlineData("/api/autorizacao/revisoes")]
    public async Task Listagem_RegistroDosItensIndisponivel_503SemItem(string rota)
    {
        await DescartarOQueSemeouAsync();
        try
        {
            await ConferirListagemRecusadaAsync(rota);
        }
        finally
        {
            await DescartarOQueSemeouAsync();
        }
    }

    private async Task ConferirListagemRecusadaAsync(string rota)
    {
        Guid concessao = await EncarregadaComPedidoEConcessaoAsync();
        await SemearRevisaoAsync(concessao);
        await using WebApplicationFactory<HostAssemblyMarker> comFalha = ComRegistroQueFalhaNosItens();
        using HttpClient cecilia = Cliente(comFalha);

        using HttpResponseMessage resposta = await cecilia.GetAsync(new Uri(rota, UriKind.Relative));

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, corpo);
        corpo.Should().Contain("uniplus.autorizacao.registro_de_acesso.falha_ao_gravar").And.NotContain(concessao.ToString());
    }

    // A revisão e o pedido semeados prendem a concessão da Encarregada; saem antes e depois, para não sobrar aos outros testes.
    private async Task DescartarOQueSemeouAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade banco = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database;
        await banco.ExecuteSqlRawAsync(
            "DELETE FROM autorizacao.revisao_itens WHERE concessao_id IN (SELECT id FROM autorizacao.concessoes WHERE beneficiario_sub = {0})", Cecilia);
        await banco.ExecuteSqlRawAsync("DELETE FROM autorizacao.revisoes_de_acessos r WHERE NOT EXISTS (SELECT 1 FROM autorizacao.revisao_itens i WHERE i.revisao_id = r.id)");
        await banco.ExecuteSqlRawAsync("DELETE FROM autorizacao.pedidos_de_concessao WHERE solicitante_sub = {0}", Cecilia);
    }

    // A Encarregada só com o perfil dela, com uma concessão própria e um pedido para si mesma de um perfil comum.
    private async Task<Guid> EncarregadaComPedidoEConcessaoAsync()
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Cecilia);
        Guid concessao = await ConcessoesDeTeste.ConcederGovernancaAsync(_fixture.Factory.Services, Cecilia, TipoDeGovernanca.EncarregadoDeDados);
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(_fixture.Factory.Services, "configuracao:tipos-processo:consultar");
        using HttpClient cecilia = Cliente(_fixture.Factory);
        using HttpRequestMessage pedido = new(HttpMethod.Post, new Uri("/api/autorizacao/pedidos-de-concessao", UriKind.Relative))
        {
            Content = JsonContent.Create(new { perfilId = perfil, justificativa = "Acompanha os tipos de processo seletivo." }),
        };
        pedido.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        using HttpResponseMessage criado = await cecilia.SendAsync(pedido);
        criado.StatusCode.Should().Be(HttpStatusCode.Created, await criado.Content.ReadAsStringAsync());

        return concessao;
    }

    private async Task SemearRevisaoAsync(Guid concessao)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        RevisaoDeAcessos revisao = RevisaoDeAcessos.Abrir(new Identidade(TestAuthHandler.DefaultIssuer, "marlene.martins"), [concessao], DateTimeOffset.UtcNow);
        await scope.ServiceProvider.GetRequiredService<IRevisaoDeAcessosRepository>().AdicionarAsync(revisao);
        await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>().SaveChangesAsync();
    }

    private WebApplicationFactory<HostAssemblyMarker> ComRegistroQueFalhaNosItens() =>
        _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRegistroOperacionalRestrito>();
                services.AddSingleton<IRegistroOperacionalRestrito, RegistroQueFalhaNosItens>();
            }));

    private static HttpClient Cliente(WebApplicationFactory<HostAssemblyMarker> fabrica)
    {
        HttpClient client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Cecilia);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "Cecília Moura");
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "cecilia_ester_moura@unifesspa.edu.br");
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailVerifiedHeader, "true");

        return client;
    }

    // Aceita a decisão da listagem, que não nomeia item, e recusa o lote dos registros por item exibido.
    private sealed class RegistroQueFalhaNosItens : IRegistroOperacionalRestrito
    {
        public Task RegistrarAsync(IReadOnlyList<RegistroDecisaoAcesso> registros, CancellationToken cancellationToken) =>
            registros.Any(static r => r.ResourceId is not null)
                ? throw new RegistroDeAcessoNaoGravadoException(new InvalidOperationException("tópico fora"))
                : Task.CompletedTask;
    }
}
