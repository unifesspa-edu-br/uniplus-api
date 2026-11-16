namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.CicloDaRetificacaoApoio;

/// <summary>
/// O identificador legível na sessão de retificação: a sessão decide pelo que a versão vigente congelou, e não pelo
/// valor vivo da raiz. O caminho da versão congelada sem identificador (declarar, fechar, descartar) está em
/// <c>IdentificadorLegivelNaRetificacaoPersistenciaTests</c>.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class IdentificadorNaSessaoDaRetificacaoEndpointTests
{
    private readonly CascadingFixture _fixture;

    public IdentificadorNaSessaoDaRetificacaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Versão vigente com identificador recusa a declaração na sessão, mesmo com a raiz viva sem ele")]
    public async Task Identificador_VersaoComValor_RaizVivaSem_Recusa()
    {
        CicloDaRetificacao c = await PublicarAsync(_fixture, nameof(Identificador_VersaoComValor_RaizVivaSem_Recusa));
        await RetirarIdentificadorDaRaizAsync(c);

        string etag = LerETag(await c.AbrirAsync("Tenta declarar outro endereço"));
        HttpResponseMessage resposta = await c.PutIdentificadorAsync(IdentificadoresDeTeste.NovoValor(), etag);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "a versão vigente congelou o identificador — é contra ela, e não contra a raiz viva, que a sessão decide");
        (await LerIdentificadorVivoAsync(c)).Should().BeNull("a recusa não altera a raiz");
    }

    [Fact(DisplayName = "If-Match defasado com identificador malformado devolve 412, e não 422")]
    public async Task Identificador_IfMatchDefasado_FormatoInvalido_412()
    {
        CicloDaRetificacao c = await PublicarAsync(_fixture, nameof(Identificador_IfMatchDefasado_FormatoInvalido_412));
        string etagInicial = LerETag(await c.AbrirAsync("Correção"));
        (await c.PutEtapasAsync(peso: 2.0m, ifMatch: etagInicial)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // O mesmo ETag, agora defasado: o PUT acima incrementou a revisão.
        HttpResponseMessage resposta = await c.PutIdentificadorAsync("PSIQ 2026", etagInicial);

        resposta.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed,
            "quem edita sobre estado desatualizado precisa reler antes de corrigir o valor");
    }
}
