namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Base das suítes do termo de responsabilidade pelo host real: a publicação pela rota, a leitura da versão fixada no
/// pedido e o pedido protegido de cada caso da ordem documento, aceite e gestor.
/// </summary>
public abstract class TermoNoHostTestBase : PedidosDeConcessaoTestBase, IAsyncLifetime
{
    internal const string RotaDoTermo = "/api/autorizacao/termos-de-responsabilidade";

    private protected TermoNoHostTestBase(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    public Task InitializeAsync() => ResetarAsync();

    /// <summary>Nada do teste fica para o próximo: pedidos, concessões, aceites e versões publicadas saem.</summary>
    public async Task DisposeAsync()
    {
        await ResetarAsync();
        await TermoNoHostDeTeste.ReiniciarAsync(Fixture.Factory.Services);
    }

    /// <summary>O SHA-256, em hexadecimal minúsculo, dos bytes UTF-8 do texto.</summary>
    internal static string Sha256(string texto) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));

    internal static Task<HttpResponseMessage> PublicarAsync(HttpClient publicador, string marca, Guid? esperada, string? chave = null) =>
        EnviarAsync(
            publicador, HttpMethod.Post, $"{RotaDoTermo}/versoes",
            new { texto = TermoNoHostDeTeste.Texto(marca), ato = TermoNoHostDeTeste.Ato, versaoVigenteEsperadaId = esperada }, chave);

    /// <summary>O corpo da criação como a tela o envia, com a versão apresentada explícita.</summary>
    internal static Task<HttpResponseMessage> PedirAsync(
        HttpClient solicitante, Guid perfilId, Guid unidadeId, Guid? apresentada, bool anexara = false, string? chave = null) =>
        EnviarAsync(
            solicitante, HttpMethod.Post, Rota,
            new { perfilId, unidadeId, justificativa = Justificativa, anexaraDocumento = anexara, termoVersaoApresentadaId = apresentada }, chave);

    internal static async Task<JsonElement> TermoDoPedidoAsync(HttpClient leitor, Guid pedidoId) =>
        (await LerAsync(leitor, $"{Rota}/{pedidoId}")).GetProperty("termo");

    /// <summary>O gestor de acessos, que publica o termo.</summary>
    private protected Task<HttpClient> PublicadorAsync() => ClienteComPermissoesAsync(Diogo, Administrar);

    private protected async Task<int> ContarPedidosAsync()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Pedidos.CountAsync();
    }

    private protected async Task<int> ContarVersoesAsync()
    {
        await using AsyncServiceScope escopo = Fixture.Factory.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().VersoesDoTermo.CountAsync();
    }

    /// <summary>Um perfil sensível e uma unidade novos; a vigência do termo foi reiniciada com uma versão publicada.</summary>
    private protected async Task<(Guid Perfil, Guid Unidade)> CenarioAsync() =>
        (await SemearPerfilSensivelAsync(), await SemearUnidadeAsync());

    internal static async Task ConferirAsync(HttpResponseMessage resposta, HttpStatusCode status, string tipo)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(status, corpo);
        corpo.Should().Contain($"uniplus.autorizacao.{tipo}");
    }
}
