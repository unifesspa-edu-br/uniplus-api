namespace Unifesspa.UniPlus.Selecao.IntegrationTests.InstantesEmMicrossegundos;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

/// <summary>
/// A pendência acima do limiar na consulta dos atos do processo, com o limiar configurado com um resto de ticks abaixo do
/// microssegundo: o limiar vale em microssegundos inteiros, e a fronteira é a de 30 minutos exatos.
/// </summary>
[Collection(ColecaoInstantesEmResiduo.Name)]
public sealed class LimiarDePendenciaDosAtosComResiduoEndpointTests
{
    private static readonly TimeSpan Limiar = TimeSpan.FromMinutes(30);

    private readonly InstantesEmResiduoFixture _fixture;

    public LimiarDePendenciaDosAtosComResiduoEndpointTests(InstantesEmResiduoFixture fixture) => _fixture = fixture;

    private InstantesEmResiduoApiFactory Fabrica => _fixture.Factory;

    [Fact(DisplayName = "No limite exato do limiar o ato pendente ainda não está acima dele")]
    public async Task NoLimiteExato_AtoPendenteNaoEstaAcimaDoLimiar()
    {
        Guid processoId = await SemearAtoPendenteGravadoEmAsync(InstantesDeTeste.EmMicrossegundos);

        bool acima = await ConsultarPendenciaAcimaDoLimiarAsync(processoId, InstantesDeTeste.EmMicrossegundos + Limiar);

        acima.Should().BeFalse();
    }

    [Fact(DisplayName = "Um microssegundo depois do limite o ato pendente está acima do limiar")]
    public async Task UmMicrossegundoDepoisDoLimite_AtoPendenteEstaAcimaDoLimiar()
    {
        Guid processoId = await SemearAtoPendenteGravadoEmAsync(InstantesDeTeste.EmMicrossegundos);

        bool acima = await ConsultarPendenciaAcimaDoLimiarAsync(
            processoId, InstantesDeTeste.EmMicrossegundos + Limiar + TimeSpan.FromTicks(TimeSpan.TicksPerMicrosecond));

        acima.Should().BeTrue();
    }

    [Fact(DisplayName = "O resto do limiar configurado abaixo do microssegundo não adia a pendência: dois ticks depois do limite já está acima")]
    public async Task RestoDoLimiar_NaoAdiaAPendencia()
    {
        Guid processoId = await SemearAtoPendenteGravadoEmAsync(InstantesDeTeste.EmMicrossegundos);

        bool acima = await ConsultarPendenciaAcimaDoLimiarAsync(processoId, InstantesDeTeste.EmMicrossegundos + Limiar + TimeSpan.FromTicks(2));

        acima.Should().BeTrue();
    }

    private async Task<Guid> SemearAtoPendenteGravadoEmAsync(DateTimeOffset gravadoEm)
    {
        await TiposDeAtoSeeder.SemearAsync(Fabrica.Services);
        Fabrica.Relogio.ParaNo(gravadoEm);
        await using AsyncServiceScope escopo = Fabrica.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
            db, $"Limiar de pendência {Guid.CreateVersion7()}");
        AtoDoProcesso ato = AtoDoProcesso.Registrar(AvisoPendente(processo.Id, documento.Id), Fabrica.Relogio).Value!;
        db.AtosDoProcesso.Add(ato);
        await db.SaveChangesAsync();

        (await db.AtosDoProcesso.AsNoTracking().SingleAsync(a => a.Id == ato.Id)).CreatedAt.Should().Be(gravadoEm);
        return processo.Id;
    }

    private static NovoAtoDoProcesso AvisoPendente(Guid processoId, Guid documentoId) => new(
        Guid.CreateVersion7(), processoId, DadosDoAtoDeTeste.TipoAviso, "Aviso", "1/2027", new DateOnly(2027, 3, 10),
        "Diretor do CEPS", "ceps", "psm-medicina-2027", documentoId, []);

    private async Task<bool> ConsultarPendenciaAcimaDoLimiarAsync(Guid processoId, DateTimeOffset agora)
    {
        Fabrica.Relogio.ParaNo(agora);
        using HttpClient client = Fabrica.CreateClient();
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
