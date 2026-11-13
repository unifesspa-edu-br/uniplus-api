namespace Unifesspa.UniPlus.Selecao.IntegrationTests.InstantesEmMicrossegundos;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Host;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

/// <summary>
/// A pendência acima do limiar na consulta dos atos do processo, com o limiar configurado com um resto de ticks abaixo do
/// microssegundo: o limiar vale em microssegundos inteiros, e a fronteira é a de 30 minutos exatos. Com o relógio de
/// produção sobre uma fonte com a 7ª casa, o instante gravado do ato e o instante que decide a pendência caem na mesma
/// grade de microssegundos.
/// </summary>
[Collection(ColecaoInstantesEmResiduo.Name)]
public sealed class LimiarDePendenciaDosAtosComResiduoEndpointTests : IDisposable
{
    private static readonly TimeSpan Limiar = TimeSpan.FromMinutes(30);

    private readonly InstantesEmResiduoFixture _fixture;

    private readonly WebApplicationFactory<HostAssemblyMarker> _comRelogioDeProducao;

    public LimiarDePendenciaDosAtosComResiduoEndpointTests(InstantesEmResiduoFixture fixture)
    {
        _fixture = fixture;
        _comRelogioDeProducao = Fabrica.WithWebHostBuilder(b => b.ConfigureTestServices(servicos =>
        {
            servicos.RemoveAll<TimeProvider>();
            servicos.AddSingleton<TimeProvider>(new RelogioEmMicrossegundos(Fabrica.Relogio));
        }));
    }

    private InstantesEmResiduoApiFactory Fabrica => _fixture.Factory;

    [Fact(DisplayName = "No limite exato do limiar o ato pendente ainda não está acima dele")]
    public async Task NoLimiteExato_AtoPendenteNaoEstaAcimaDoLimiar()
    {
        (Guid processoId, _) = await SemearAtoPendenteAsync(Fabrica, InstantesDeTeste.EmMicrossegundos);

        bool acima = await ConsultarPendenciaAcimaDoLimiarAsync(Fabrica, processoId, InstantesDeTeste.EmMicrossegundos + Limiar);

        acima.Should().BeFalse();
    }

    [Fact(DisplayName = "Um microssegundo depois do limite o ato pendente está acima do limiar")]
    public async Task UmMicrossegundoDepoisDoLimite_AtoPendenteEstaAcimaDoLimiar()
    {
        (Guid processoId, _) = await SemearAtoPendenteAsync(Fabrica, InstantesDeTeste.EmMicrossegundos);

        bool acima = await ConsultarPendenciaAcimaDoLimiarAsync(
            Fabrica, processoId, InstantesDeTeste.EmMicrossegundos + Limiar + TimeSpan.FromTicks(TimeSpan.TicksPerMicrosecond));

        acima.Should().BeTrue();
    }

    [Fact(DisplayName = "O resto do limiar configurado abaixo do microssegundo não adia a pendência: dois ticks depois do limite já está acima")]
    public async Task RestoDoLimiar_NaoAdiaAPendencia()
    {
        (Guid processoId, _) = await SemearAtoPendenteAsync(Fabrica, InstantesDeTeste.EmMicrossegundos);

        bool acima = await ConsultarPendenciaAcimaDoLimiarAsync(Fabrica, processoId, InstantesDeTeste.EmMicrossegundos + Limiar + TimeSpan.FromTicks(2));

        acima.Should().BeTrue();
    }

    [Fact(DisplayName = "Com a fonte na 7ª casa, o instante gravado do ato é o mesmo em memória e na releitura")]
    public async Task FonteNaSetimaCasa_InstanteDoAtoIgualEmMemoriaENaReleitura()
    {
        (Guid processoId, AtoDoProcesso emMemoria) = await SemearAtoPendenteAsync(_comRelogioDeProducao, InstantesDeTeste.ComResiduoDeTicks);

        emMemoria.CreatedAt.Should().Be(InstantesDeTeste.EmMicrossegundos);
        (await RelerAsync(processoId)).Should().Be(emMemoria.CreatedAt);
    }

    [Fact(DisplayName = "Com a fonte na 7ª casa no registro e na consulta, o limite exato do limiar ainda não deixa o ato acima dele")]
    public async Task FonteNaSetimaCasa_NoLimiteExato_AtoNaoEstaAcimaDoLimiar()
    {
        (Guid processoId, _) = await SemearAtoPendenteAsync(_comRelogioDeProducao, InstantesDeTeste.ComResiduoDeTicks);

        bool acima = await ConsultarPendenciaAcimaDoLimiarAsync(
            _comRelogioDeProducao, processoId, InstantesDeTeste.ComResiduoDeTicks + Limiar);

        acima.Should().BeFalse();
    }

    public void Dispose() => _comRelogioDeProducao.Dispose();

    private async Task<(Guid ProcessoId, AtoDoProcesso EmMemoria)> SemearAtoPendenteAsync(
        WebApplicationFactory<HostAssemblyMarker> host, DateTimeOffset fonteEm)
    {
        await TiposDeAtoSeeder.SemearAsync(host.Services);
        Fabrica.Relogio.ParaNo(fonteEm);
        await using AsyncServiceScope escopo = host.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
            db, $"Limiar de pendência {Guid.CreateVersion7()}");
        AtoDoProcesso ato = AtoDoProcesso.Registrar(
            AvisoPendente(processo.Id, documento.Id), escopo.ServiceProvider.GetRequiredService<TimeProvider>()).Value!;
        db.AtosDoProcesso.Add(ato);
        await db.SaveChangesAsync();
        return (processo.Id, ato);
    }

    private async Task<DateTimeOffset> RelerAsync(Guid processoId)
    {
        await using AsyncServiceScope escopo = Fabrica.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>().AtosDoProcesso.AsNoTracking()
            .Where(a => a.ProcessoSeletivoId == processoId).Select(a => a.CreatedAt).SingleAsync();
    }

    private static NovoAtoDoProcesso AvisoPendente(Guid processoId, Guid documentoId) => new(
        Guid.CreateVersion7(), processoId, DadosDoAtoDeTeste.TipoAviso, "Aviso", "1/2027", new DateOnly(2027, 3, 10),
        "Diretor do CEPS", "ceps", "psm-medicina-2027", documentoId, []);

    private async Task<bool> ConsultarPendenciaAcimaDoLimiarAsync(
        WebApplicationFactory<HostAssemblyMarker> host, Guid processoId, DateTimeOffset agora)
    {
        Fabrica.Relogio.ParaNo(agora);
        using HttpClient client = host.CreateClient();
        using HttpRequestMessage pedido = new(HttpMethod.Get, new Uri($"/api/selecao/processos-seletivos/{processoId}/atos", UriKind.Relative));
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        pedido.Headers.TryAddWithoutValidation("Accept", "application/vnd.uniplus.ato-do-processo.v1+json");

        using HttpResponseMessage resposta = await client.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement ato = corpo.RootElement.EnumerateArray().Single();
        ato.GetProperty("situacao").GetString().Should().Be("pendente");
        return ato.GetProperty("pendenciaAcimaDoLimiar").GetBoolean();
    }
}
