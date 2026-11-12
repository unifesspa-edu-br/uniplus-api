namespace Unifesspa.UniPlus.Host.IntegrationTests.Governanca;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

/// <summary>
/// O conflito entre Representante do controlador e Encarregado de dados nas duas ordens, pelo host real (ADR-0162): o papel
/// vigente recusa o outro na criação do pedido próprio e no aceite do pedido feito por terceiro; o papel vencido não conta
/// como vigente, e o outro segue pelo fluxo comum.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class PapeisIncompativeisNasDuasOrdensNoHostTests : PedidosDeConcessaoTestBase
{
    public PapeisIncompativeisNasDuasOrdensNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    public static TheoryData<Guid, Guid> Ordens => new()
    {
        { PerfisDeSistema.RepresentanteDoControladorId, PerfisDeSistema.EncarregadoDeDadosId },
        { PerfisDeSistema.EncarregadoDeDadosId, PerfisDeSistema.RepresentanteDoControladorId },
    };

    [Theory(DisplayName = "Com um papel vigente, o pedido próprio do outro papel é recusado na criação com 409, nas duas ordens")]
    [MemberData(nameof(Ordens))]
    public async Task PapelVigente_PedidoProprioDoOutro_409(Guid exercido, Guid pedido)
    {
        await ResetarAsync();
        await SemearConcessaoAsync(Leandro, exercido, EscopoDaConcessao.DaInstituicao());

        using HttpResponseMessage resposta = await CriarBrutoAsync(Cliente(Leandro), pedido, null, null);

        await ConferirRecusaAsync(resposta, HttpStatusCode.Conflict, "papeis_incompativeis");
    }

    [Theory(DisplayName = "Com um papel vigente, o aceite do pedido do outro papel feito por terceiro é recusado com 409, nas duas ordens")]
    [MemberData(nameof(Ordens))]
    public async Task PapelVigente_AceiteDoOutro_409(Guid exercido, Guid pedido)
    {
        await ResetarAsync();
        await SemearConcessaoAsync(Leandro, exercido, EscopoDaConcessao.DaInstituicao());
        Guid id = await PedidoComDocumentoAsync(pedido);

        using HttpResponseMessage aceite = await EnviarAsync(Cliente(Leandro), HttpMethod.Post, $"{Rota}/{id}/aceite");

        await ConferirRecusaAsync(aceite, HttpStatusCode.Conflict, "papeis_incompativeis");
    }

    [Theory(DisplayName = "Com o papel vencido, o pedido próprio do outro papel é criado, nas duas ordens")]
    [MemberData(nameof(Ordens))]
    public async Task PapelVencido_PedidoProprioDoOutro_Criado(Guid vencido, Guid pedido)
    {
        await ResetarAsync();
        await SemearVencidaAsync(Leandro, vencido);

        using HttpResponseMessage resposta = await CriarBrutoAsync(Cliente(Leandro), pedido, null, null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
    }

    [Theory(DisplayName = "Com o papel vencido, o aceite do pedido do outro papel feito por terceiro segue, nas duas ordens")]
    [MemberData(nameof(Ordens))]
    public async Task PapelVencido_AceiteDoOutro_Segue(Guid vencido, Guid pedido)
    {
        await ResetarAsync();
        await SemearVencidaAsync(Leandro, vencido);
        Guid id = await PedidoComDocumentoAsync(pedido);

        using HttpResponseMessage aceite = await EnviarAsync(Cliente(Leandro), HttpMethod.Post, $"{Rota}/{id}/aceite");

        aceite.StatusCode.Should().Be(HttpStatusCode.OK, await aceite.Content.ReadAsStringAsync());
    }

    // O pedido de Betina para Leandro, com o documento verificado, à espera do aceite dele.
    private async Task<Guid> PedidoComDocumentoAsync(Guid perfilId)
    {
        using HttpClient betina = Cliente(Betina);
        Guid id = await CriarAsync(betina, perfilId, null, Leandro);
        await AnexarVerificadoAsync(betina, id);

        return id;
    }

    private async Task SemearVencidaAsync(Persona pessoa, Guid perfilId)
    {
        await SemearConcessaoAsync(pessoa, perfilId, EscopoDaConcessao.DaInstituicao());
        await ExecutarSqlAsync(
            "UPDATE autorizacao.concessoes SET concedida_em = now() - interval '3 days', valida_ate = now() - interval '1 minute' WHERE beneficiario_sub = {0} AND perfil_id = {1}",
            pessoa.Sub.ToString(),
            perfilId);
    }
}
