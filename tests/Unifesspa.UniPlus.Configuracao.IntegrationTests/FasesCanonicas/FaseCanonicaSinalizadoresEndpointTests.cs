namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FasesCanonicas;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.FasesCanonicas.ApoioDosEndpointsDeFaseCanonica;

/// <summary>
/// Os sinalizadores da fase canônica pelo HTTP: a ordem das recusas do POST e o carimbo de
/// alteração do PUT, que só muda quando algum sinalizador mudou de fato.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class FaseCanonicaSinalizadoresEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public FaseCanonicaSinalizadoresEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "POST com código fora do conjunto e as duas marcas de coleta devolve 422 com errors[] do código e depois da exclusividade")]
    public async Task Criar_CodigoForaDoConjuntoEAsDuasMarcas_AcumulaCodigoEExclusividade()
    {
        var body = new
        {
            codigo = "ENTREVISTA_FINAL",
            nome = "Entrevista",
            origemData = "NO_EDITAL",
            coletaInscricao = true,
            coletaSolicitacaoIsencao = true,
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CamposComErroAsync(response)).Should().Equal("codigo", "coletaSolicitacaoIsencao");
    }

    [Fact(DisplayName = "POST da fase de solicitação de isenção com a marca grava e o GET devolve a marca ligada e a coleta de inscrição desligada")]
    public async Task Criar_FaseDeIsencaoComAMarca_GetDevolveAMarca()
    {
        await ApoioDosEndpointsDeFaseCanonica.LiberarSlotDoSeedAsync(_fixture.ConnectionString, "SOLICITACAO_ISENCAO");
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage criar = await EnviarFaseDeIsencaoComAMarcaAsync(client);

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement fase = await ObterAsync(client, await criar.Content.ReadFromJsonAsync<Guid>());
        fase.GetProperty("coletaSolicitacaoIsencao").GetBoolean().Should().BeTrue();
        fase.GetProperty("coletaInscricao").GetBoolean().Should().BeFalse();
    }

    [Fact(DisplayName = "PUT sem mudança nos dados não carimba a alteração da fase, e o PUT que muda um sinalizador carimba")]
    public async Task Atualizar_SemMudanca_NaoCarimbaAAlteracao()
    {
        await ApoioDosEndpointsDeFaseCanonica.LiberarSlotDoSeedAsync(_fixture.ConnectionString, "HOMOLOGACAO_INSCRICOES");
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarHomologacaoAsync(client);
        object semMudanca = new { id, nome = "Homologação", origemData = "NO_EDITAL", permiteComplementacao = true };

        (await EnviarPutAdmin(client, id, semMudanca)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AtualizadaEmAsync(id)).Should().BeNull("o PUT repetiu o que a fase já tinha");

        object mudandoASinalizador = new { id, nome = "Homologação", origemData = "NO_EDITAL", permiteComplementacao = false };

        (await EnviarPutAdmin(client, id, mudandoASinalizador)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AtualizadaEmAsync(id)).Should().NotBeNull("o PUT desligou a complementação");
    }

    private static async Task<string?[]> CamposComErroAsync(HttpResponseMessage response)
    {
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("errors").EnumerateArray()
            .Select(e => e.GetProperty("field").GetString())
            .ToArray();
    }

    private static Task<HttpResponseMessage> EnviarFaseDeIsencaoComAMarcaAsync(HttpClient client) =>
        EnviarPostAdmin(client, new
        {
            codigo = "SOLICITACAO_ISENCAO",
            nome = "Solicitação de isenção",
            origemData = "NO_EDITAL",
            coletaSolicitacaoIsencao = true,
        });

    private static Task<Guid> CriarHomologacaoAsync(HttpClient client) =>
        CriarAsync(client, new
        {
            codigo = "HOMOLOGACAO_INSCRICOES",
            nome = "Homologação",
            origemData = "NO_EDITAL",
            permiteComplementacao = true,
        });

    private async Task<DateTimeOffset?> AtualizadaEmAsync(Guid id)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext dbContext = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        FaseCanonica fase = await dbContext.FasesCanonicas.AsNoTracking().SingleAsync(f => f.Id == id);
        return fase.UpdatedAt;
    }
}
