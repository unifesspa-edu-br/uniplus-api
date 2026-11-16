namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.CicloDaRetificacaoApoio;

/// <summary>
/// O <b>fechamento</b> da sessão de retificação (Story #862 — ADR-0110): congela a versão N+1 com a configuração
/// editada, preserva a sessão numa recusa e devolve o atalho atômico quando não há sessão aberta.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class FechamentoDaRetificacaoEndpointTests
{
    private readonly CascadingFixture _fixture;

    public FechamentoDaRetificacaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "CA-01 (#862): FECHAR congela a versão N+1 COM a alteração — e a versão N permanece intacta (append-only)")]
    public async Task Fechar_CongelaVersaoNovaComAAlteracao()
    {
        CicloDaRetificacao c = await PublicarAsync(_fixture, nameof(Fechar_CongelaVersaoNovaComAAlteracao));

        (string hashV1, string bytesV1) = await LerSnapshotVigenteAsync(c);

        string etag = LerETag(await c.AbrirAsync("Correção do peso da prova objetiva"));
        (await c.PutEtapasAsync(peso: 3.0m, ifMatch: etag)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        string etagAtual = LerETag(await c.ObterSessaoAsync());
        Guid documento = await SemearDocumentoConfirmadoAsync(c);

        HttpResponseMessage fechamento = await c.FecharAsync(documento, etagAtual);

        fechamento.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using AsyncServiceScope scope = c.Api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        List<VersaoConfiguracao> versoes = await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == c.ProcessoId)
            .OrderBy(v => v.NumeroVersao)
            .ToListAsync();

        versoes.Should().HaveCount(2, "o fechamento congela a versão N+1");
        versoes[0].HashConfiguracao.Should().Be(hashV1, "a versão N permanece INTACTA — o passado não se muta");
        versoes[1].HashConfiguracao.Should().NotBe(
            hashV1,
            "a versão N+1 congela a configuração EDITADA — antes desta Feature, a retificação recanonicalizava a mesma configuração de sempre e as duas versões saíam idênticas");
        versoes[1].AtoCriadorRetificaId.Should().Be(versoes[0].AtoCriadorId, "o ato novo emenda o ato criador da versão anterior");

        (await ContarRascunhosAsync(c)).Should().Be(0, "a sessão morre com o fechamento");

        // E o peso alterado está de fato dentro dos bytes congelados da versão nova.
        (await LerPesoDaEtapaVivaAsync(c)).Should().Be(3.0m);
        bytesV1.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "CA-06 (#862): fechar sem sessão aberta → 409; com If-Match defasado → 412")]
    public async Task Fechar_SemSessaoOuDefasado_Recusa()
    {
        CicloDaRetificacao c = await PublicarAsync(_fixture, nameof(Fechar_SemSessaoOuDefasado_Recusa));
        Guid doc = await SemearDocumentoConfirmadoAsync(c);

        (await c.FecharAsync(doc, $"\"{Guid.CreateVersion7()}:1\""))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        string etagInicial = LerETag(await c.AbrirAsync("Correção"));
        await c.PutEtapasAsync(peso: 2.0m, ifMatch: etagInicial);

        (await c.FecharAsync(doc, etagInicial))
            .StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        (await ContarRascunhosAsync(c)).Should().Be(1, "uma recusa não destrói a sessão — o administrador corrige e tenta de novo");
    }

    [Fact(DisplayName = "Uma recusa de NEGÓCIO no fechamento não destrói a sessão — o rascunho permanece aberto, com a edição intacta")]
    public async Task Fechar_RecusadoPorNegocio_PreservaASessao()
    {
        CicloDaRetificacao c = await PublicarAsync(_fixture, nameof(Fechar_RecusadoPorNegocio_PreservaASessao));

        string etag = LerETag(await c.AbrirAsync("Correção"));
        (await c.PutEtapasAsync(peso: 3.0m, ifMatch: etag)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        string etagAtual = LerETag(await c.ObterSessaoAsync());

        // Documento inexistente: recusa de negócio (422), depois da precondição ter passado.
        HttpResponseMessage r = await c.FecharAsync(Guid.CreateVersion7(), etagAtual);

        r.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ContarRascunhosAsync(c)).Should().Be(1, "encerrar a sessão numa recusa de negócio faria o administrador perder toda a edição");
        (await LerPesoDaEtapaVivaAsync(c)).Should().Be(3.0m, "e a edição continua lá, esperando a correção");
        (await ContarVersoesAsync(c)).Should().Be(1, "nada foi congelado");
    }

    [Fact(DisplayName = "CA-02 (regressão #862): com a sessão FECHADA, o atalho atômico POST /retificacoes volta a funcionar exatamente como antes")]
    public async Task Atalho_AposFechamento_ContinuaFuncionando()
    {
        CicloDaRetificacao c = await PublicarAsync(_fixture, nameof(Atalho_AposFechamento_ContinuaFuncionando));

        string etag = LerETag(await c.AbrirAsync("Primeira correção"));
        await c.PutEtapasAsync(peso: 3.0m, ifMatch: etag);
        Guid doc1 = await SemearDocumentoConfirmadoAsync(c);
        (await c.FecharAsync(doc1, LerETag(await c.ObterSessaoAsync())))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Sem sessão aberta, o atalho de sempre volta a valer — e sucede a cadeia.
        Guid doc2 = await SemearDocumentoConfirmadoAsync(c);
        HttpResponseMessage atalho = await c.AtalhoAsync(doc2, "Segunda correção, em um ato só");

        atalho.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ContarVersoesAsync(c)).Should().Be(3, "v1 (publicação) → v2 (fechamento da sessão) → v3 (atalho)");
    }
}
