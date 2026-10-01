namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O grupo repetível pelo HTTP, contra o Postgres (UNI-REQ-0146): o PUT dos itens grava o grupo com
/// os campos de membro, e o GET do processo o devolve no formulário.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class GrupoRepetivelEndpointTests
{
    private readonly CascadingFixture _fixture;

    public GrupoRepetivelEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "O PUT dos itens grava o grupo com os campos de membro, e o GET do processo o devolve")]
    public async Task DefinirItens_ComGrupo_GravaEDevolveNoFormulario()
    {
        string campo = await SemearFatoDeMembroAsync();
        Guid processoId = await SemearProcessoAsync();

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage put = Requisicao(HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{processoId}/formularios/INSCRICAO/itens");
        put.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        put.Content = JsonContent.Create(new
        {
            itens = Array.Empty<object>(),
            grupos = new[]
            {
                new
                {
                    codigo = "COMPOSICAO_FAMILIAR", ordem = 0, rotulo = "Composição familiar", etapaCodigo = "DADOS",
                    minimo = 0, maximo = 10, obrigatoriedade = "NUNCA",
                    subitens = new[] { new { fatoCodigo = campo, ordem = 0, rotulo = "Trabalha no campo", tipoRenderizacao = "BOOLEANO", obrigatoriedade = "SEMPRE" } },
                },
            },
        });
        HttpResponseMessage gravado = await client.SendAsync(put);
        gravado.StatusCode.Should().Be(HttpStatusCode.NoContent, await gravado.Content.ReadAsStringAsync());

        using HttpRequestMessage get = Requisicao(HttpMethod.Get, $"/api/selecao/processos-seletivos/{processoId}");
        get.Headers.Accept.ParseAdd("application/vnd.uniplus.processo-seletivo.v1+json");
        HttpResponseMessage lido = await client.SendAsync(get);
        lido.StatusCode.Should().Be(HttpStatusCode.OK, await lido.Content.ReadAsStringAsync());

        using JsonDocument corpo = JsonDocument.Parse(await lido.Content.ReadAsStringAsync());
        JsonElement inscricao = corpo.RootElement.GetProperty("formularios").EnumerateArray()
            .Single(static f => f.GetProperty("finalidade").GetString() == "INSCRICAO");
        JsonElement grupo = inscricao.GetProperty("grupos").EnumerateArray().Single();
        grupo.GetProperty("codigo").GetString().Should().Be("COMPOSICAO_FAMILIAR");
        grupo.GetProperty("subitens").EnumerateArray().Single().GetProperty("fatoCodigo").GetString().Should().Be(campo);
        inscricao.GetProperty("fatosColetados").GetArrayLength().Should().Be(0, "o campo do grupo não é item do formulário");
    }

    private async Task<string> SemearFatoDeMembroAsync()
    {
        string codigo = $"MEMBRO_{Guid.NewGuid():N}"[..30].ToUpperInvariant();
        FatoCandidato fato = FatoCandidato.CriarDoAdministrador(
            codigo, "Trabalha no campo", null, DominioFato.Booleano, CardinalidadeFato.Escalar, fonteValores: null, formato: null,
            "INSCRICAO", EscopoFato.MembroGrupo, ClassificacaoProtecaoDado.Pessoal, "Composição familiar",
            HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext db = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        db.Add(fato);
        await db.SaveChangesAsync();
        return codigo;
    }

    private async Task<Guid> SemearProcessoAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, $"Grupo repetível {Guid.CreateVersion7()}");
        return processo.Id;
    }

    private static HttpRequestMessage Requisicao(HttpMethod metodo, string rota)
    {
        HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        return request;
    }
}
