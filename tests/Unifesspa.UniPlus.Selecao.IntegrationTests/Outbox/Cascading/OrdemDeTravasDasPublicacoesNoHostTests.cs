namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// A ordem de travas da ADR-0170 nas operações reais que gravam em mais de um módulo: recurso compartilhado, processo,
/// divulgação, ato. Cada operação roda pelo endpoint do host real contra o banco real, e o teste a faz esperar numa trava
/// do processo mantida por outra sessão.
/// </summary>
/// <remarks>
/// <para><b>O que garante.</b> Enquanto espera pela linha do processo, a operação ainda não escreveu nem travou linha em
/// nenhuma tabela de Publicações, nem na divulgação do certame: o banco não mostra, para as sessões que esperam, nada além
/// de leitura nessas tabelas. Quem esperasse pelo processo já segurando o ato ou a divulgação fecharia, com outra operação
/// que trava o processo primeiro, o ciclo que o banco resolve com deadlock. Liberada a trava, a operação conclui. As quatro
/// operações são a publicação do processo, a retificação, o ato avulso e a publicação de item do edital.</para>
/// <para><b>O limite.</b> Nenhuma operação do produto trava hoje um recurso compartilhado (catálogo ou configuração); a
/// ordem entre ele e o processo passa a ser conferida na primeira operação que o travar. A ordem entre a divulgação e o
/// ato, dentro da gravação única, é do EF e das chaves das tabelas, e não de um ponto do código.</para>
/// </remarks>
[Collection(CascadingCollection.Name)]
public sealed class OrdemDeTravasDasPublicacoesNoHostTests
{
    private readonly CascadingFixture _fixture;

    public OrdemDeTravasDasPublicacoesNoHostTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A publicação do processo espera a linha do processo sem ter escrito em Publicações nem na divulgação")]
    public async Task PublicarProcesso_TravaOProcessoAntesDeEscreverEmPublicacoesENaDivulgacao()
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        using HttpClient client = _fixture.Factory.CreateClient();
        (Guid processoId, Guid documentoId) = await PublicacaoPeloEndpointDeTeste.SemearProcessoAsync(_fixture.Factory, "Ordem de travas da publicação");

        await ConferirEsperaPeloProcessoAsync(
            processoId, () => PublicacaoPeloEndpointDeTeste.PublicarAsync(client, processoId, documentoId), HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "A retificação espera a linha do processo sem ter escrito em Publicações nem na divulgação")]
    public async Task Retificar_TravaOProcessoAntesDeEscreverEmPublicacoesENaDivulgacao()
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        using HttpClient client = _fixture.Factory.CreateClient();
        (Guid processoId, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(_fixture.Factory, client);
        Guid documentoId = await PublicacaoPeloEndpointDeTeste.SemearDocumentoConfirmadoAsync(_fixture.Factory, processoId);

        await ConferirEsperaPeloProcessoAsync(
            processoId, () => PublicacaoPeloEndpointDeTeste.RetificarAsync(client, processoId, documentoId), HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "A publicação de ato avulso espera a linha do processo sem ter escrito em Publicações nem na divulgação")]
    public async Task PublicarAtoAvulso_TravaOProcessoAntesDeEscreverEmPublicacoesENaDivulgacao()
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        using HttpClient client = _fixture.Factory.CreateClient();
        (Guid processoId, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(_fixture.Factory, client);
        Guid documentoId = await PublicarAtoAvulsoEndpointTests.SemearDocumentoAsync(_fixture.Factory, processoId, TiposDeMidiaDeDocumento.Pdf, "aviso.pdf");

        await ConferirEsperaPeloProcessoAsync(
            processoId, () => PublicarAtoAvulsoEndpointTests.PublicarAvisoAsync(client, processoId, documentoId), HttpStatusCode.Created);
    }

    [Fact(DisplayName = "A publicação de item do edital espera a linha do processo sem ter escrito em Publicações nem na divulgação")]
    public async Task PublicarItemDoEdital_TravaOProcessoAntesDeEscreverEmPublicacoesENaDivulgacao()
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        using HttpClient client = _fixture.Factory.CreateClient();
        (Guid processoId, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(
            _fixture.Factory, client, PublicarProdutoDoEditalEndpointTests.ProdutosDaHomologacao());
        Guid itemId = await ItemPreliminarAsync(client, processoId);
        Guid documentoId = await PublicarAtoAvulsoEndpointTests.SemearDocumentoAsync(_fixture.Factory, processoId, TiposDeMidiaDeDocumento.Pdf, "lista.pdf");

        await ConferirEsperaPeloProcessoAsync(
            processoId, () => PublicarProdutoDoEditalEndpointTests.PublicarAsync(client, processoId, itemId, documentoId), HttpStatusCode.Created);
    }

    private static async Task<Guid> ItemPreliminarAsync(HttpClient client, Guid processoId) =>
        (await PublicarProdutoDoEditalEndpointTests.ConsultarItensAsync(client, processoId)).GetProperty("fases").EnumerateArray()
            .SelectMany(static f => f.GetProperty("produtos").EnumerateArray())
            .Single(static p => p.GetProperty("papel").GetString() == "PRELIMINAR").GetProperty("id").GetGuid();

    /// <summary>
    /// Uma sessão do teste trava o processo; a operação é enviada e espera por ela. Enquanto espera, não pode segurar escrita nem
    /// trava de linha em Publicações ou na divulgação; liberada a trava, ela conclui com o status esperado.
    /// </summary>
    private async Task ConferirEsperaPeloProcessoAsync(Guid processoId, Func<Task<HttpResponseMessage>> operacao, HttpStatusCode statusEsperado)
    {
        await using SessaoQueRetemOProcesso retida = await SessaoQueRetemOProcesso.AbrirAsync(_fixture.ConnectionString, processoId);
        Task<HttpResponseMessage> envio = Task.Run(operacao);

        await retida.AguardarOperacaoEsperandoAsync();

        envio.IsCompleted.Should().BeFalse("a operação não passa enquanto outra sessão trava o processo");
        (await retida.TravasDeEscritaDaOperacaoEmEsperaAsync()).Should().BeEmpty(
            "a operação trava o processo antes de tocar o ato e a divulgação (ADR-0170)");

        await retida.LiberarAsync();
        using HttpResponseMessage resposta = await envio;
        resposta.StatusCode.Should().Be(statusEsperado, await resposta.Content.ReadAsStringAsync());
    }
}
