namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.RegistrosDeAcesso;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Toda leitura dos próprios pedidos só sai com o registro gravado: com o registro indisponível, a fila dos próprios
/// pedidos, o detalhe, a trilha e o anexo respondem 503 sem o dado, para quem tem a consulta dos pedidos (UNI-REQ-0372) e
/// para quem não a tem, que é registrado por titularidade (ADR-0166). A fila sem pedido não grava nada e responde mesmo
/// com o registro indisponível. Cecília Moura é o Encarregado de dados; Paulo Nogueira não tem permissão alguma. Cada
/// um pediu um perfil comum para si mesmo.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class FalhaAoRegistrarLeituraDosPropriosPedidosTests
{
    private const string Falha = "uniplus.autorizacao.registro_de_acesso.falha_ao_gravar";
    private const string Rota = "/api/autorizacao/pedidos-de-concessao";
    private const string Cecilia = "cecilia.moura";
    private const string Paulo = "paulo.nogueira";

    private readonly AutorizacaoEndpointFixture _fixture;

    public FalhaAoRegistrarLeituraDosPropriosPedidosTests(AutorizacaoEndpointFixture fixture) => _fixture = fixture;

    [Theory(DisplayName = "Com o registro indisponível, a leitura dos próprios pedidos pelo Encarregado de dados responde 503 sem o pedido")]
    [InlineData(LeituraDoPedido.FilaDosProprios)]
    [InlineData(LeituraDoPedido.Detalhe)]
    [InlineData(LeituraDoPedido.Trilha)]
    [InlineData(LeituraDoPedido.Anexo)]
    public async Task LeituraDoEncarregado_RegistroIndisponivel_503SemOPedido(LeituraDoPedido leitura)
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Cecilia);
        await ConcessoesDeTeste.ConcederGovernancaAsync(_fixture.Factory.Services, Cecilia, TipoDeGovernanca.EncarregadoDeDados);

        (HttpStatusCode status, string corpo, Guid pedido) = await LerOProprioPedidoAsync(Cecilia, leitura, new RegistroQueFalha());

        status.Should().Be(HttpStatusCode.ServiceUnavailable, corpo);
        corpo.Should().Contain(Falha).And.NotContain(pedido.ToString());
    }

    [Theory(DisplayName = "Com o registro indisponível, a leitura dos próprios pedidos por quem não tem a consulta responde 503 sem o pedido")]
    [InlineData(LeituraDoPedido.FilaDosProprios)]
    [InlineData(LeituraDoPedido.Detalhe)]
    [InlineData(LeituraDoPedido.Trilha)]
    [InlineData(LeituraDoPedido.Anexo)]
    public async Task LeituraSemAConsulta_RegistroIndisponivel_503SemOPedido(LeituraDoPedido leitura)
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Paulo);

        (HttpStatusCode status, string corpo, Guid pedido) = await LerOProprioPedidoAsync(Paulo, leitura, new RegistroQueFalha());

        status.Should().Be(HttpStatusCode.ServiceUnavailable, corpo);
        corpo.Should().Contain(Falha).And.NotContain(pedido.ToString());
    }

    [Fact(DisplayName = "Com o registro indisponível, a fila dos próprios pedidos sem pedido responde 200 vazia, sem depender do registro")]
    public async Task FilaVaziaSemAConsulta_RegistroIndisponivel_200Vazia()
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Paulo);
        await DescartarPedidosAsync(Paulo);

        await using WebApplicationFactory<HostAssemblyMarker> comFalha = ComRegistro(new RegistroQueFalha());
        using HttpClient cliente = Cliente(comFalha, Paulo);
        using HttpResponseMessage resposta = await cliente.GetAsync(RotaDa(LeituraDoPedido.FilaDosProprios, Guid.Empty));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
    }

    [Theory(DisplayName = "Quem não tem a consulta tem a leitura do próprio pedido gravada por titularidade, com o código da operação do titular")]
    [InlineData(LeituraDoPedido.FilaDosProprios)]
    [InlineData(LeituraDoPedido.Detalhe)]
    public async Task LeituraSemAConsulta_GravaPorTitularidade(LeituraDoPedido leitura)
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Paulo);
        RegistroQueGuarda registro = new();

        (HttpStatusCode status, string corpo, Guid pedido) = await LerOProprioPedidoAsync(Paulo, leitura, registro);

        status.Should().Be(HttpStatusCode.OK, corpo);
        RegistroDecisaoAcesso gravado = registro.Gravados.Should().ContainSingle(r => r.ResourceId == pedido).Subject;
        (gravado.AtorSubject, gravado.Permissao, gravado.FonteGrant, gravado.GrantId, gravado.Permitido)
            .Should().Be((Paulo, "autorizacao:pedidos:ler-proprios", "titularidade", (Guid?)null, true));
    }

    /// <summary>As leituras do pedido que quem é parte faz; o anexo pedido não existe, e a visibilidade vem antes dele.</summary>
    public enum LeituraDoPedido
    {
        FilaDosProprios,
        Detalhe,
        Trilha,
        Anexo,
    }

    // O pedido sai antes e depois, para não sobrar aos outros testes da coleção.
    private async Task<(HttpStatusCode Status, string Corpo, Guid Pedido)> LerOProprioPedidoAsync(
        string sub, LeituraDoPedido leitura, IRegistroOperacionalRestrito registro)
    {
        await DescartarPedidosAsync(sub);
        try
        {
            Guid pedido = await PedirParaSiMesmoAsync(sub);
            await using WebApplicationFactory<HostAssemblyMarker> fabrica = ComRegistro(registro);
            using HttpClient cliente = Cliente(fabrica, sub);
            using HttpResponseMessage resposta = await cliente.GetAsync(RotaDa(leitura, pedido));

            return (resposta.StatusCode, await resposta.Content.ReadAsStringAsync(), pedido);
        }
        finally
        {
            await DescartarPedidosAsync(sub);
        }
    }

    private static Uri RotaDa(LeituraDoPedido leitura, Guid pedido) => new(
        leitura switch
        {
            LeituraDoPedido.FilaDosProprios => $"{Rota}?fila=meus",
            LeituraDoPedido.Detalhe => $"{Rota}/{pedido}",
            LeituraDoPedido.Trilha => $"{Rota}/{pedido}/trilha",
            _ => $"{Rota}/{pedido}/anexos/{Guid.CreateVersion7()}",
        },
        UriKind.Relative);

    private async Task<Guid> PedirParaSiMesmoAsync(string sub)
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(_fixture.Factory.Services, "configuracao:tipos-processo:consultar");
        using HttpClient cliente = Cliente(_fixture.Factory, sub);
        using HttpRequestMessage pedido = new(HttpMethod.Post, new Uri(Rota, UriKind.Relative))
        {
            Content = JsonContent.Create(new { perfilId = perfil, justificativa = "Acompanha os tipos de processo seletivo." }),
        };
        pedido.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        using HttpResponseMessage criado = await cliente.SendAsync(pedido);
        string corpo = await criado.Content.ReadAsStringAsync();
        criado.StatusCode.Should().Be(HttpStatusCode.Created, corpo);

        using JsonDocument documento = JsonDocument.Parse(corpo);
        return documento.RootElement.GetProperty("id").GetGuid();
    }

    private async Task DescartarPedidosAsync(string sub)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database
            .ExecuteSqlRawAsync("DELETE FROM autorizacao.pedidos_de_concessao WHERE solicitante_sub = {0}", sub);
    }

    private WebApplicationFactory<HostAssemblyMarker> ComRegistro(IRegistroOperacionalRestrito registro) =>
        _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRegistroOperacionalRestrito>();
                services.AddSingleton<IRegistroOperacionalRestrito>(registro);
            }));

    private static HttpClient Cliente(WebApplicationFactory<HostAssemblyMarker> fabrica, string sub)
    {
        HttpClient client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, $"{sub}@unifesspa.edu.br");
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailVerifiedHeader, "true");

        return client;
    }

    private sealed class RegistroQueFalha : IRegistroOperacionalRestrito
    {
        public Task RegistrarAsync(IReadOnlyList<RegistroDecisaoAcesso> registros, CancellationToken cancellationToken) =>
            throw new RegistroDeAcessoNaoGravadoException(new InvalidOperationException("tópico fora"));
    }

    private sealed class RegistroQueGuarda : IRegistroOperacionalRestrito
    {
        public ConcurrentQueue<RegistroDecisaoAcesso> Gravados { get; } = new();

        public Task RegistrarAsync(IReadOnlyList<RegistroDecisaoAcesso> registros, CancellationToken cancellationToken)
        {
            foreach (RegistroDecisaoAcesso registro in registros)
            {
                Gravados.Enqueue(registro);
            }

            return Task.CompletedTask;
        }
    }
}
