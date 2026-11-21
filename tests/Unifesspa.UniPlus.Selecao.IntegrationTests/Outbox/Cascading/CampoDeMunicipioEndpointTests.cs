namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// A UF e o município de nascimento pelo HTTP, contra o Postgres (UNI-REQ-0145): o formulário de
/// inscrição os coleta na seção do conjunto básico, e o município, no campo de município, cita a
/// UF respondida antes.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class CampoDeMunicipioEndpointTests
{
    private readonly CascadingFixture _fixture;

    public CampoDeMunicipioEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "O PUT dos itens da inscrição grava a seção do conjunto básico, e o GET devolve o município com a UF citada")]
    public async Task DefinirItens_Inscricao_GravaOMunicipioDeNascimento()
    {
        Guid processoId = await SemearProcessoAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage gravado = await ProcessoSeletivoHttp.DefinirItensAsync(client, processoId, new { itens = Array.Empty<object>(), grupos = Array.Empty<object>() });
        gravado.StatusCode.Should().Be(HttpStatusCode.NoContent, await gravado.Content.ReadAsStringAsync());

        using JsonDocument corpo = await ProcessoSeletivoHttp.ObterAsync(client, processoId);
        ConferirMunicipioDeNascimento(FormularioNaRespostaDoProcesso.Item(FormularioNaRespostaDoProcesso.DeInscricao(corpo), "NATURALIDADE_MUNICIPIO"));
    }

    private static void ConferirMunicipioDeNascimento(JsonElement municipio)
    {
        municipio.GetProperty("etapaCodigo").GetString().Should().Be("DADOS_BASICOS");
        municipio.GetProperty("apresentacao").GetProperty("tipoRenderizacao").GetString().Should().Be("MUNICIPIO");
        JsonElement restricao = municipio.GetProperty("regras").GetProperty("restricoes").EnumerateArray().Single();
        restricao.GetProperty("tipo").GetString().Should().Be("MUNICIPIOS_DA_UF");
        restricao.GetProperty("fatos").EnumerateArray().Single().GetString().Should().Be("NATURALIDADE_UF");
    }

    private async Task<Guid> SemearProcessoAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"Campo de município {Guid.CreateVersion7()}" });
        return processo.Id;
    }
}
