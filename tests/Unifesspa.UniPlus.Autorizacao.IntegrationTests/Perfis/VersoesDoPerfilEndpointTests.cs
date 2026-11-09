namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;

/// <summary>Criação e cancelamento de versões do perfil pelo endpoint real, com as recusas que dependem das concessões do perfil.</summary>
[Collection(AutorizacaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class VersoesDoPerfilEndpointTests : PerfilEndpointTestBase
{
    public VersoesDoPerfilEndpointTests(AutorizacaoEndpointFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Perfil em vigor aceita uma versão pendente e mantém a composição em vigor até a verificação")]
    public async Task CriarVersao_PerfilVigente_MantemAVigente()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);

        using HttpResponseMessage resposta = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [ConsultarCatalogo, ManterMotivos]));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        using JsonDocument perfil = await LerAsync(diogo, id);
        perfil.RootElement.GetProperty("composicaoEmVigor").GetProperty("numero").GetInt32().Should().Be(1);
        perfil.RootElement.GetProperty("composicaoEmVigor").GetProperty("permissoes").GetArrayLength().Should().Be(1);
        perfil.RootElement.GetProperty("versaoPendente").GetProperty("numero").GetInt32().Should().Be(2);
    }

    [Fact(DisplayName = "Com versão pendente, outra versão é recusada com 422 até a pendente ser cancelada")]
    public async Task CriarVersao_ComPendente_Devolve422()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        using HttpResponseMessage primeira = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [ConsultarCatalogo]));
        Guid versaoId = LerEnvio(await primeira.Content.ReadAsStringAsync()).VersaoId;

        using HttpResponseMessage segunda = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [ConsultarCatalogo]));
        using HttpResponseMessage cancelada = await CancelarAsync(diogo, id, versaoId);
        using HttpResponseMessage terceira = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [ConsultarCatalogo]));

        segunda.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await segunda.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.versao_pendente_existente");
        cancelada.StatusCode.Should().Be(HttpStatusCode.OK);
        terceira.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "Cancelar versão que não está pendente ou que não existe devolve 422 e 404")]
    public async Task CancelarVersao_NaoPendenteOuInexistente_Recusa()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        Guid vigente = await VersaoVigenteAsync(id);

        using HttpResponseMessage naoPendente = await CancelarAsync(diogo, id, vigente);
        using HttpResponseMessage inexistente = await CancelarAsync(diogo, id, Guid.NewGuid());

        naoPendente.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await naoPendente.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.versao_nao_pendente");
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await inexistente.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.versao_nao_encontrada");
    }

    [Fact(DisplayName = "Perfil sem concessões aceita versão que amplia a sensibilidade e muda o alcance")]
    public async Task CriarVersao_SemConcessoes_AceitaAmpliarEMudarOAlcance()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(OperarInscricoes);

        using HttpResponseMessage ampliada = await EnviarAsync(
            diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [OperarInscricoes, AnalisarAtendimento]));
        Guid pendente = LerEnvio(await ampliada.Content.ReadAsStringAsync()).VersaoId;
        (await CancelarAsync(diogo, id, pendente)).Dispose();
        using HttpResponseMessage outroAlcance = await EnviarAsync(
            diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [ManterMotivos]));

        ampliada.StatusCode.Should().Be(HttpStatusCode.Created, await ampliada.Content.ReadAsStringAsync());
        outroAlcance.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "Perfil com concessão aceita versão que acrescenta permissão sensível: as concessões migram na janela da vigência")]
    public async Task CriarVersao_ComConcessao_AmpliarASensibilidade_Aceita()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(OperarInscricoes);
        await SemearConcessaoAsync(id, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [OperarInscricoes, AnalisarAtendimento]));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        (await LerAsync(diogo, id)).RootElement.GetProperty("versaoPendente").ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact(DisplayName = "Perfil já sensível, com concessão, aceita acrescentar outra permissão sensível")]
    public async Task CriarVersao_ComConcessao_OutraSensivelNoPerfilJaSensivel_Aceita()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(AnalisarAtendimento);
        await SemearConcessaoAsync(id, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [AnalisarAtendimento, "teste:laudos:consultar"]));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "Perfil com concessão recusa versão que muda de alcance")]
    public async Task CriarVersao_ComConcessao_MudarOAlcance_Recusa()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(OperarInscricoes);
        await SemearConcessaoAsync(id, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", ComMotivoDaRemocao(Corpo(Nome(), [ManterMotivos])));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.autorizacao.perfil.alcance_alterado");
    }

    [Fact(DisplayName = "Perfil com concessão aceita versão que renomeia e mantém a sensibilidade")]
    public async Task CriarVersao_ComConcessao_SemAmpliar_Aceita()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(OperarInscricoes);
        await SemearConcessaoAsync(id, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));

        using HttpResponseMessage resposta = await EnviarAsync(
            diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [OperarInscricoes]));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "Perfil desativado recusa versão nova, e a desativação preserva a pendente")]
    public async Task CriarVersao_PerfilInativo_Devolve422()
    {
        using HttpClient diogo = await ClienteDoGestorAsync();
        Guid id = await SemearPerfilVigenteAsync(ConsultarCatalogo);
        await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [ConsultarCatalogo]));
        await EnviarAsync(diogo, HttpMethod.Delete, $"{Rota}/{id}/ativacao");

        using HttpResponseMessage resposta = await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/versoes", Corpo(Nome(), [ConsultarCatalogo]));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerAsync(diogo, id)).RootElement.GetProperty("versaoPendente").ValueKind.Should().Be(JsonValueKind.Object);
    }

    private static Task<HttpResponseMessage> CancelarAsync(HttpClient cliente, Guid perfilId, Guid versaoId) =>
        EnviarAsync(cliente, HttpMethod.Post, $"{Rota}/{perfilId}/versoes/{versaoId}/cancelamento", new { motivo = "Submetida por engano." });
}
