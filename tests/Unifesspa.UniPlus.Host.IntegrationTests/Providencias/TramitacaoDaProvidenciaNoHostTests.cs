namespace Unifesspa.UniPlus.Host.IntegrationTests.Providencias;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A tramitação pelo host real: as permissões de cada rota, os impedimentos, a fila em ordem de abertura, o registro de
/// cada providência exibida e a trilha da concessão (UNI-REQ-0375, UNI-REQ-0376).
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class TramitacaoDaProvidenciaNoHostTests : ProvidenciasNoHostTestBase
{
    public TramitacaoDaProvidenciaNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Cada rota exige a sua permissão: o gestor não solicita, o Encarregado não responde nem atende, e quem não tem nenhuma não lê")]
    public async Task Permissoes_PorRota()
    {
        Governanca g = await GovernancaAsync();
        Guid concessao = await ConcessaoDeLeandroAsync();
        Guid providencia = await SolicitarAsync(g.Cecilia, concessao);

        (await SolicitarBrutoAsync(g.Diogo, concessao, "revisao")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ResponderAsync(g.Cecilia, providencia)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AtenderAsync(g.Cecilia, providencia, Leandro.Email)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Cliente(Leandro).GetAsync(new Uri($"{RotaDasProvidencias}/{providencia}", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ProvidenciaAsync(g.Cecilia, providencia)).GetProperty("impedidoDeResponder").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "O gestor que também é Encarregado não responde a providência que solicitou, e o beneficiário não responde sobre a própria concessão")]
    public async Task Impedimentos_DeQuemSolicitouEDoBeneficiario()
    {
        Governanca g = await GovernancaAsync();
        await ConcessoesDeTeste.ConcederGovernancaAsync(Fixture.Factory.Services, Betina.Sub.ToString(), TipoDeGovernanca.EncarregadoDeDados, Betina.Email);
        Guid providencia = await SolicitarAsync(g.Betina, await ConcessaoDeLeandroAsync());
        Guid concessaoDeDiogo = await ConcessoesDeTeste.ConcederAsync(
            Fixture.Factory.Services, Diogo.Sub.ToString(), await SemearPerfilInternoAsync(), EscopoDaConcessao.DaUnidade(await SemearUnidadeAsync()), email: Diogo.Email);
        Guid sobreDiogo = await SolicitarAsync(g.Cecilia, concessaoDeDiogo);

        await ConferirCodigoAsync(await ResponderAsync(g.Betina, providencia), HttpStatusCode.UnprocessableEntity, "uniplus.autorizacao.providencia.solicitante_nao_responde");
        await ConferirCodigoAsync(await ResponderAsync(g.Diogo, sobreDiogo), HttpStatusCode.UnprocessableEntity, "uniplus.autorizacao.providencia.beneficiario_nao_responde");
    }

    [Fact(DisplayName = "A fila prioritária traz as abertas com concessão ativa, a mais antiga primeiro, sem o fundamento, e registra cada uma exibida")]
    public async Task Fila_EmOrdemDeAberturaERegistrada()
    {
        Governanca g = await GovernancaAsync();
        Guid primeira = await SolicitarAsync(g.Cecilia, await ConcessaoDeLeandroAsync());
        Guid segunda = await SolicitarAsync(g.Cecilia, await ConcessaoDeLeandroAsync());
        int antes = Registro.Registrados.Count;

        JsonElement pagina = await LerAsync(g.Diogo, RotaDasProvidencias);

        JsonElement[] itens = [.. (pagina.ValueKind == JsonValueKind.Array ? pagina : pagina.GetProperty("items")).EnumerateArray()];
        itens.Select(static i => i.GetProperty("id").GetGuid()).Should().Equal(primeira, segunda);
        itens.Where(static i => i.TryGetProperty("fundamento", out _)).Should().BeEmpty();
        Registro.Registrados.Skip(antes).Where(static r => r.ResourceTipo == "providencia-sobre-concessao").Select(static r => r.ResourceId)
            .Should().Contain([primeira, segunda]);
    }

    [Fact(DisplayName = "A trilha da concessão mostra a solicitação e a resposta, com autor e texto")]
    public async Task Trilha_SolicitacaoEResposta()
    {
        Governanca g = await GovernancaAsync();
        Guid concessao = await ConcessaoDeLeandroAsync();
        Guid providencia = await SolicitarAsync(g.Cecilia, concessao);
        (await ResponderAsync(g.Diogo, providencia)).StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement[] eventos = [.. (await LerAsync(g.Isabella, $"/api/autorizacao/concessoes/{concessao}/trilha")).EnumerateArray()];

        JsonElement solicitada = eventos.Single(static e => e.GetProperty("tipo").GetString() == "providencia_solicitada");
        (solicitada.GetProperty("autor").GetProperty("sub").GetString(), solicitada.GetProperty("detalhe").GetString()).Should().Be((Cecilia.Sub.ToString(), $"Retirada do acesso. Fundamento: {Fundamento}"));
        eventos.Single(static e => e.GetProperty("tipo").GetString() == "providencia_respondida").GetProperty("autor").GetProperty("sub").GetString().Should().Be(Diogo.Sub.ToString());
    }

    private RegistroDeDecisoesDeTeste Registro => Fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>();
}
