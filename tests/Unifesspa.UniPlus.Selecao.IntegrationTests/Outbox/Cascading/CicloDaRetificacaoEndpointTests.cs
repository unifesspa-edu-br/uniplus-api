namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.CicloDaRetificacaoApoio;

/// <summary>
/// O <b>ciclo fechado</b> da retificação (Stories #860, #861, #862 — ADR-0110): abrir →
/// editar → <b>descartar</b> ou <b>fechar</b>.
/// </summary>
/// <remarks>
/// <para>
/// A prova central é a <b>imunidade do certame em curso</b> (CA-01 da #861), e ela só existe
/// se as <b>duas</b> leituras forem olhadas ao mesmo tempo: enquanto a sessão está aberta e a
/// configuração já foi alterada, o <c>GET /{id}</c> (configuração <b>viva</b>) <b>enxerga</b>
/// a alteração, e o <c>GET /{id}/snapshot-vigente</c> (versão <b>congelada</b>) <b>não</b>.
/// Um teste que olhasse só uma das duas não provaria nada.
/// </para>
/// <para>
/// É o que garante que o candidato continua vendo o que o edital publicou, enquanto o
/// administrador edita.
/// </para>
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class CicloDaRetificacaoEndpointTests
{
    private readonly CascadingFixture _fixture;

    public CicloDaRetificacaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    // ══════════════════════════════════════════════════════════════════════════════
    // CA-01 da #861 — A IMUNIDADE. É a razão de a Feature poder existir sem quebrar nada.
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "CA-01 (imunidade): com a sessão aberta e a configuração ALTERADA, o snapshot vigente não muda um byte — o candidato continua vendo o que o edital publicou")]
    public async Task SessaoAberta_ConfiguracaoAlterada_SnapshotVigenteImune()
    {
        CicloDaRetificacao c = await PublicarAsync(_fixture, nameof(SessaoAberta_ConfiguracaoAlterada_SnapshotVigenteImune));

        (string hashAntes, string bytesAntes) = await LerSnapshotVigenteAsync(c);

        string etag = LerETag(await c.AbrirAsync("Correção do peso da prova"));

        // Altera uma dimensão REAL — o peso da etapa vai de 1 para 3.
        (await c.PutEtapasAsync(peso: 3.0m, ifMatch: etag)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A configuração VIVA enxerga a alteração...
        decimal pesoVivo = await LerPesoDaEtapaVivaAsync(c);
        pesoVivo.Should().Be(3.0m, "o GET do recurso serve a configuração viva, que é o que o administrador está editando");

        // ...e a versão CONGELADA não enxerga nada.
        (string hashDepois, string bytesDepois) = await LerSnapshotVigenteAsync(c);

        hashDepois.Should().Be(
            hashAntes,
            "o snapshot vigente é a versão CONGELADA — se a edição vazasse para ele, um certame publicado passaria a exibir configuração que NÃO foi publicada, e o hash do documento deixaria de valer");
        bytesDepois.Should().Be(bytesAntes, "byte a byte: nem o hash nem o conteúdo se movem durante a sessão");
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // #861 — DESCARTAR
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "CA-05 (E2E): publicar → abrir → alterar → DESCARTAR devolve o agregado ao congelado, e a versão vigente nunca mudou")]
    public async Task Descartar_DevolveAoCongelado()
    {
        CicloDaRetificacao c = await PublicarAsync(_fixture, nameof(Descartar_DevolveAoCongelado));

        (string hashAntes, _) = await LerSnapshotVigenteAsync(c);
        int versoesAntes = await ContarVersoesAsync(c);

        string etag = LerETag(await c.AbrirAsync("Correção do peso"));
        (await c.PutEtapasAsync(peso: 3.0m, ifMatch: etag)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerPesoDaEtapaVivaAsync(c)).Should().Be(3.0m);

        // Relê o ETag (a mutação incrementou a revisão) e descarta.
        string etagAtual = LerETag(await c.ObterSessaoAsync());
        HttpResponseMessage descarte = await c.DescartarAsync(etagAtual);

        descarte.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await LerPesoDaEtapaVivaAsync(c)).Should().Be(
            1.0m,
            "o descarte REIDRATA a partir da versão congelada — não é um 'desfazer' incremental, que seria impossível: todo Definir* substitui a coleção inteira");

        (await ContarRascunhosAsync(c)).Should().Be(0, "a sessão deixou de existir");
        (await ContarVersoesAsync(c)).Should().Be(versoesAntes, "CA-03: descartar produz ZERO versão");
        (await LerSnapshotVigenteAsync(c)).Hash.Should().Be(hashAntes, "a versão vigente nunca se moveu");
    }

    [Fact(DisplayName = "CA-04: descartar sem sessão aberta devolve 409")]
    public async Task Descartar_SemSessao_Conflito()
    {
        CicloDaRetificacao c = await PublicarAsync(_fixture, nameof(Descartar_SemSessao_Conflito));

        HttpResponseMessage r = await c.DescartarAsync($"\"{Guid.CreateVersion7()}:1\"");

        r.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "Descartar sem If-Match devolve 428 — a rota existe PARA a sessão, e a precondição é incondicional")]
    public async Task Descartar_SemIfMatch_428()
    {
        CicloDaRetificacao c = await PublicarAsync(_fixture, nameof(Descartar_SemIfMatch_428));
        await c.AbrirAsync("Correção");

        HttpResponseMessage r = await c.DescartarAsync(ifMatch: null);

        r.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
        (await ContarRascunhosAsync(c)).Should().Be(1, "uma recusa de protocolo não destrói a sessão");
    }

    [Fact(DisplayName = "Descartar com If-Match defasado devolve 412 e NÃO descarta")]
    public async Task Descartar_IfMatchDefasado_412()
    {
        CicloDaRetificacao c = await PublicarAsync(_fixture, nameof(Descartar_IfMatchDefasado_412));
        string etagInicial = LerETag(await c.AbrirAsync("Correção"));
        (await c.PutEtapasAsync(peso: 2.0m, ifMatch: etagInicial)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // O mesmo ETag, agora defasado: o PUT acima incrementou a revisão.
        HttpResponseMessage r = await c.DescartarAsync(etagInicial);

        r.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        (await ContarRascunhosAsync(c)).Should().Be(1, "o descarte foi recusado — a sessão continua de pé");
        (await LerPesoDaEtapaVivaAsync(c)).Should().Be(2.0m, "e a edição também");
    }
}
