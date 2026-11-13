namespace Unifesspa.UniPlus.Host.IntegrationTests.Governanca;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// A designação de Representante do controlador não tem prazo (UNI-REQ-0200, UNI-REQ-0394): a sugestão no pedido e o prazo
/// na aprovação do gestor são recusados. O pedido anterior à regra, que já tem o prazo do gestor, é recusado na aprovação
/// da segunda etapa, antes de virar concessão, sem nada gravado e continua decidível: a proteção de dados o recusa. O cancelamento só cabe antes da primeira
/// decisão, quando o pedido ainda não tem prazo.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class PrazoDoRepresentanteNoHostTests : UltimoRepresentanteNoHostTestBase
{
    private const string ValidadeDoPedido = "uniplus.autorizacao.pedido.validade_invalida";

    public PrazoDoRepresentanteNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "O pedido de Representante com fim de vigência sugerido é recusado no campo da sugestão")]
    public async Task Criar_ComSugestao_Recusa()
    {
        await GovernancaAsync();

        using HttpResponseMessage resposta = await EnviarAsync(
            Cliente(Betina), HttpMethod.Post, Rota,
            new { beneficiarioEmail = Leandro.Email, perfilId = Representante, justificativa = Justificativa, anexaraDocumento = true, validaAteSugerida = DateTimeOffset.UtcNow.AddDays(30) });

        await ConferirCodigoAsync(resposta, HttpStatusCode.UnprocessableEntity, ValidadeDoPedido);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("validaAteSugerida");
    }

    [Fact(DisplayName = "O gestor não aprova a designação de Representante com prazo, e o pedido segue aguardando o gestor")]
    public async Task Aprovar_ComPrazo_Recusa()
    {
        Governanca g = await GovernancaAsync();
        Guid pedido = await DesignacaoAceitaAsync(Leandro);

        await ConferirCodigoAsync(
            await EnviarAsync(g.Diogo, HttpMethod.Post, $"{Rota}/{pedido}/aprovacoes", new { validaAte = DateTimeOffset.UtcNow.AddDays(30) }),
            HttpStatusCode.UnprocessableEntity,
            ValidadeDoPedido);

        (await SituacaoAsync(g.Diogo, pedido)).Should().Be("aguardando_gestor");
    }

    [Fact(DisplayName = "O pedido anterior à regra, com prazo do gestor, é recusado na aprovação da segunda etapa, antes de virar concessão, e a proteção de dados o recusa")]
    public async Task PedidoAnteriorComPrazo_RecusadoNaSegundaEtapa()
    {
        Governanca g = await GovernancaAsync();
        Guid pedido = await NaSegundaEtapaComPrazoAnteriorAsync(g.Diogo);

        await ConferirCodigoAsync(
            await EnviarAsync(g.Marlene, HttpMethod.Post, $"{Rota}/{pedido}/aprovacoes"), HttpStatusCode.UnprocessableEntity, ValidadeDoPedido);

        (await SituacaoAsync(g.Diogo, pedido)).Should().Be("aguardando_protecao_de_dados");
        (await VigentesAsync()).Should().Equal(Marlene.Sub.ToString());
        (await EnviarAsync(g.Marlene, HttpMethod.Post, $"{Rota}/{pedido}/recusa", new { motivo = "O prazo não é admitido." })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SituacaoAsync(g.Diogo, pedido)).Should().Be("recusado");
    }

    // A designação de Leandro, aprovada pelo gestor sem prazo, à espera de Marlene; o prazo entra direto no banco, como
    // ficou gravado no pedido aprovado antes da regra.
    private async Task<Guid> NaSegundaEtapaComPrazoAnteriorAsync(HttpClient gestor)
    {
        Guid pedido = await DesignacaoAceitaAsync(Leandro);
        (await EnviarAsync(gestor, HttpMethod.Post, $"{Rota}/{pedido}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);
        await EstadoAnteriorAoRepresentanteSemPrazo.GravarPrazoNoPedidoAsync(Fixture.Factory.Services, pedido, DateTimeOffset.UtcNow.AddDays(30));

        return pedido;
    }
}
