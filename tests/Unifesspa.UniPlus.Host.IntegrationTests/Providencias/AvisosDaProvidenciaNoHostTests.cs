namespace Unifesspa.UniPlus.Host.IntegrationTests.Providencias;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Os avisos da providência entregues pelo outbox até o servidor de e-mail de teste (UNI-REQ-0375): quem recebe cada um e
/// o que o aviso ao beneficiário não pode trazer.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class AvisosDaProvidenciaNoHostTests : ProvidenciasNoHostTestBase
{
    private const string AssuntoDaAbertura = "Solicitação de providência sobre concessão de acesso";
    private const string AssuntoDaRevogacao = "Concessão de acesso revogada";

    public AvisosDaProvidenciaNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A abertura avisa os gestores e o Representante do controlador, inclusive o gestor que solicitou, e não avisa o beneficiário que também é gestor")]
    public async Task Abertura_AvisaAdministracaoMenosOBeneficiario()
    {
        Governanca g = await GovernancaAsync();
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Betina.Sub.ToString(), TipoDeGovernanca.EncarregadoDeDados, Betina.Email);
        await ClienteComPermissoesAsync(Leandro, Administrar);
        Guid providencia = await SolicitarAsync(g.Betina, await ConcessaoDeLeandroAsync());

        JsonElement[] avisos = [.. await Task.WhenAll(new[] { Diogo, Isabella, Betina, Marlene }.Select(p => AguardarAvisoAsync(p, providencia, AssuntoDaAbertura)))];

        avisos.Should().AllSatisfy(static a => a.GetProperty("Text").GetString().Should().NotContain(Fundamento).And.NotContain(Betina.Nome).And.NotContain(Leandro.Nome));
        (await MensagensAsync(Leandro, providencia)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A resposta avisa quem solicitou mesmo depois de ele perder o perfil de Encarregado")]
    public async Task Resposta_AvisaQuemSolicitouSemOPerfil()
    {
        Governanca g = await GovernancaAsync();
        Guid providencia = await SolicitarAsync(g.Cecilia, await ConcessaoDeLeandroAsync());
        Guid perfilDeCecilia = await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Cecilia.Sub.ToString(), TipoDeGovernanca.EncarregadoDeDados, Cecilia.Email);
        (await RevogarAsync(g.Diogo, perfilDeCecilia, "Deixou a função.", Cecilia.Email)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ResponderAsync(g.Isabella, providencia)).StatusCode.Should().Be(HttpStatusCode.OK);

        await AguardarAvisoAsync(Cecilia, providencia, "Providência respondida");
    }

    [Fact(DisplayName = "O atendimento avisa quem solicitou e o beneficiário, e o aviso ao beneficiário não identifica quem solicitou nem quem atendeu")]
    public async Task Atendimento_AvisoAoBeneficiarioSemIdentificar()
    {
        Governanca g = await GovernancaAsync();
        Guid providencia = await SolicitarAsync(g.Cecilia, await ConcessaoDeLeandroAsync());
        (await AtenderAsync(g.Diogo, providencia, Leandro.Email)).StatusCode.Should().Be(HttpStatusCode.OK);

        await AguardarAvisoAsync(Cecilia, providencia, "Providência atendida");
        JsonElement[] aoBeneficiario = await AvisosDeRevogacaoAoBeneficiarioAsync();

        string[] proibidos = [Cecilia.Nome, Cecilia.Sub.ToString(), Cecilia.Email, Diogo.Nome, Diogo.Sub.ToString(), Diogo.Email, Fundamento, Motivo, "providência", "Providência"];
        aoBeneficiario.Should().AllSatisfy(m => new[] { "Subject", "Text", "HTML" }.Select(campo => m.GetProperty(campo).GetString()!)
            .Should().AllSatisfy(texto => proibidos.Should().AllSatisfy(p => texto.Should().NotContain(p))));
        aoBeneficiario.Should().AllSatisfy(static m => m.GetProperty("Text").GetString().Should().Contain("nova solicitação comprovando a necessidade"));
    }

    // O aviso ao beneficiário leva ao pedido de acesso, não à providência: é localizado pelo assunto, e todos os que
    // existirem para ele precisam cumprir a regra.
    private async Task<JsonElement[]> AvisosDeRevogacaoAoBeneficiarioAsync()
    {
        JsonElement[] mensagens = [];
        await AguardarAsync(
            async () => (mensagens = await MensagensComAssuntoAsync(Leandro.Email, AssuntoDaRevogacao)).Length > 0,
            "o aviso da revogação pelo atendimento não chegou ao beneficiário");
        return mensagens;
    }

    private async Task<JsonElement[]> MensagensComAssuntoAsync(string destinatario, string assunto)
    {
        using HttpClient mailpit = new() { BaseAddress = Fixture.ApiDoMailpit };
        JsonElement lista = await mailpit.GetFromJsonAsync<JsonElement>("/api/v1/messages?limit=500");
        List<JsonElement> completas = [];
        foreach (JsonElement resumo in lista.GetProperty("messages").EnumerateArray()
            .Where(r => r.GetProperty("Subject").GetString() == assunto
                && r.GetProperty("To").EnumerateArray().Any(t => string.Equals(t.GetProperty("Address").GetString(), destinatario, StringComparison.OrdinalIgnoreCase))))
        {
            completas.Add(await mailpit.GetFromJsonAsync<JsonElement>($"/api/v1/message/{resumo.GetProperty("ID").GetString()}"));
        }

        return [.. completas];
    }
}
