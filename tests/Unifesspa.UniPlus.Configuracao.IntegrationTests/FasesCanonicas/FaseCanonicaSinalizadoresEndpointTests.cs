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
        var corpo = new CorpoDoTipoDeFase
        {
            Codigo = "ENTREVISTA_FINAL",
            Nome = "Entrevista",
            ColetaInscricao = true,
            ColetaSolicitacaoIsencao = true,
        };

        using HttpClient cliente = _fixture.Factory.CreateClient();
        HttpResponseMessage resposta = await EnviarPostAdmin(cliente, corpo);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CamposComErroAsync(resposta)).Should().Equal("codigo", "coletaSolicitacaoIsencao");
    }

    [Fact(DisplayName = "POST da fase de solicitação de isenção com a marca grava e o GET devolve a marca ligada e a coleta de inscrição desligada")]
    public async Task Criar_FaseDeIsencaoComAMarca_GetDevolveAMarca()
    {
        await ApoioDosEndpointsDeFaseCanonica.LiberarSlotDoSeedAsync(_fixture.ConnectionString, "SOLICITACAO_ISENCAO");
        using HttpClient cliente = _fixture.Factory.CreateClient();

        HttpResponseMessage criar = await EnviarFaseDeIsencaoComAMarcaAsync(cliente);

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement fase = await ObterAsync(cliente, await criar.Content.ReadFromJsonAsync<Guid>());
        fase.GetProperty("coletaSolicitacaoIsencao").GetBoolean().Should().BeTrue();
        fase.GetProperty("coletaInscricao").GetBoolean().Should().BeFalse();
    }

    [Fact(DisplayName = "PUT sem mudança nos dados não carimba a alteração da fase, e o PUT que muda um sinalizador carimba")]
    public async Task Atualizar_SemMudanca_NaoCarimbaAAlteracao()
    {
        await ApoioDosEndpointsDeFaseCanonica.LiberarSlotDoSeedAsync(_fixture.ConnectionString, "HOMOLOGACAO_INSCRICOES");
        using HttpClient cliente = _fixture.Factory.CreateClient();
        Guid id = await CriarHomologacaoAsync(cliente);
        object semMudanca = new CorpoDoTipoDeFase { Id = id, Nome = "Homologação", PermiteComplementacao = true };

        (await EnviarPutAdmin(cliente, id, semMudanca)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AtualizadaEmAsync(id)).Should().BeNull("o PUT repetiu o que a fase já tinha");

        object mudandoASinalizador = new CorpoDoTipoDeFase { Id = id, Nome = "Homologação", PermiteComplementacao = false };

        (await EnviarPutAdmin(cliente, id, mudandoASinalizador)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AtualizadaEmAsync(id)).Should().NotBeNull("o PUT desligou a complementação");
    }

    [Theory(DisplayName = "POST sem organizadaEmChamadas, omitido ou nulo, devolve 422 no campo do sinalizador")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Criar_OrganizadaEmChamadasAusente_Retorna422NoCampo(bool enviarNulo)
    {
        object corpo = enviarNulo
            ? new { codigo = "HABILITACAO", nome = "Habilitação", origemData = "NO_EDITAL", agrupaEtapas = false, permiteComplementacao = false, coletaInscricao = false, coletaSolicitacaoIsencao = false, organizadaEmChamadas = (bool?)null }
            : new { codigo = "HABILITACAO", nome = "Habilitação", origemData = "NO_EDITAL", agrupaEtapas = false, permiteComplementacao = false, coletaInscricao = false, coletaSolicitacaoIsencao = false };

        using HttpClient cliente = _fixture.Factory.CreateClient();
        HttpResponseMessage resposta = await EnviarPostAdmin(cliente, corpo);

        await ConferirSinalizadorObrigatorioAsync(resposta, "organizadaEmChamadas");
    }

    [Theory(DisplayName = "PUT sem organizadaEmChamadas, omitido ou nulo, devolve 422 no campo do sinalizador mesmo com id inexistente")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Atualizar_OrganizadaEmChamadasAusente_Retorna422NoCampo(bool enviarNulo)
    {
        Guid id = Guid.NewGuid();
        object corpo = enviarNulo
            ? new { id, nome = "Habilitação", origemData = "NO_EDITAL", agrupaEtapas = false, permiteComplementacao = false, coletaInscricao = false, coletaSolicitacaoIsencao = false, organizadaEmChamadas = (bool?)null }
            : new { id, nome = "Habilitação", origemData = "NO_EDITAL", agrupaEtapas = false, permiteComplementacao = false, coletaInscricao = false, coletaSolicitacaoIsencao = false };

        using HttpClient cliente = _fixture.Factory.CreateClient();
        HttpResponseMessage resposta = await EnviarPutAdmin(cliente, id, corpo);

        await ConferirSinalizadorObrigatorioAsync(resposta, "organizadaEmChamadas");
    }

    private static async Task ConferirSinalizadorObrigatorioAsync(HttpResponseMessage resposta, string campo)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        documento.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.validacao.obrigatorio");
        documento.RootElement.GetProperty("errors")[0].GetProperty("field").GetString().Should().Be(campo);
    }

    private static async Task<string?[]> CamposComErroAsync(HttpResponseMessage resposta)
    {
        using JsonDocument documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return documento.RootElement.GetProperty("errors").EnumerateArray()
            .Select(erro => erro.GetProperty("field").GetString())
            .ToArray();
    }

    private static Task<HttpResponseMessage> EnviarFaseDeIsencaoComAMarcaAsync(HttpClient cliente) =>
        EnviarPostAdmin(cliente, new CorpoDoTipoDeFase
        {
            Codigo = "SOLICITACAO_ISENCAO",
            Nome = "Solicitação de isenção",
            ColetaSolicitacaoIsencao = true,
        });

    private static Task<Guid> CriarHomologacaoAsync(HttpClient cliente) =>
        CriarAsync(cliente, new CorpoDoTipoDeFase
        {
            Codigo = "HOMOLOGACAO_INSCRICOES",
            Nome = "Homologação",
            PermiteComplementacao = true,
        });

    private async Task<DateTimeOffset?> AtualizadaEmAsync(Guid id)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext dbContext = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        FaseCanonica fase = await dbContext.FasesCanonicas.AsNoTracking().SingleAsync(candidata => candidata.Id == id);
        return fase.UpdatedAt;
    }
}
