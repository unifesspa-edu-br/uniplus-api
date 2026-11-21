namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.CorpoDoCronogramaComMarca;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.FaseDoCronogramaPublicavelDeTeste;

/// <summary>
/// A conferência que a publicação refaz sobre o cronograma, pelo HTTP e contra Postgres real: a marca na primeira fase e as fases
/// ligadas por precedência no mesmo momento, com o campo no documento congelado e o código da fase em <c>identificadores</c>, e a
/// ordem invertida e a sobreposição não permitida, que a publicação passa a conferir. A precedência é a do cadastro vivo no momento
/// da publicação, e as relações usadas são as do catálogo de 14 fases.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class MarcaNaPublicacaoPeloEndpointTests(CascadingFixture fixture)
{
    private const string Prefixo = "uniplus.selecao.processo_seletivo.";
    private const string ResultadoFinal = "RESULTADO_FINAL";
    private const string Habilitacao = "HABILITACAO";

    [Fact(DisplayName = "A publicação recusa a marca na primeira fase no campo da marca, com o código da fase em identificadores e sem citá-lo na mensagem")]
    public async Task MarcaNaPrimeiraFase_RecusaComCampoEIdentificadores()
    {
        ProcessoPublicavelComAMarca processo = await ProcessoPublicavelComAMarca.SemearAsync(fixture, "Primeira fase marcada", ordemDoResultadoFinal: 1);
        await processo.MarcarAFaseNoBancoAsync(ResultadoFinal, marca: true);

        using HttpResponseMessage recusada = await processo.PublicarAsync();

        ErroDaRecusa erro = (await ErrosDaRecusaAsync(recusada)).Should().ContainSingle(e => e.Codigo == Prefixo + "primeira_fase_no_mesmo_momento_da_anterior").Which;
        erro.Campo.Should().Be("/cronograma/fases/0/noMesmoMomentoDaAnterior");
        erro.Identificadores.Should().Equal(new ItemIdentificado("fases", ResultadoFinal));
        erro.Mensagem.Should().NotContain(ResultadoFinal);
    }

    [Fact(DisplayName = "A publicação recusa fases ligadas por precedência no mesmo momento na fase marcada, com o código dela em identificadores")]
    public async Task FasesLigadasPorPrecedenciaNoMesmoMomento_RecusaNaFaseMarcada()
    {
        ProcessoPublicavelComAMarca processo = await ProcessoPublicavelComAMarca.SemearAsync(
            fixture, "Precedência no mesmo momento", 1, Criar(Habilitacao, 2, 3, noMesmoMomentoDaAnterior: true));

        using HttpResponseMessage recusada = await processo.PublicarAsync();

        ErroDaRecusa erro = (await ErrosDaRecusaAsync(recusada)).Should().ContainSingle(e => e.Codigo == Prefixo + "fases_com_precedencia_no_mesmo_momento").Which;
        erro.Campo.Should().Be("/cronograma/fases/1/noMesmoMomentoDaAnterior");
        erro.Identificadores.Should().Equal(new ItemIdentificado("fases", Habilitacao));
        erro.Mensagem.Should().Contain(ResultadoFinal).And.Contain(Habilitacao);
    }

    [Fact(DisplayName = "A precedência cadastrada depois da gravação recusa a publicação, e retirada do cadastro a publicação segue")]
    public async Task PrecedenciaCadastradaDepoisDaGravacao_RecusaAPublicacao()
    {
        ProcessoPublicavelComAMarca processo = await ProcessoPublicavelComAMarca.SemearAsync(
            fixture, "Precedência cadastrada depois", 1, Criar("AVALIACAO_BIOPSICOSSOCIAL", 2, 3, noMesmoMomentoDaAnterior: true));
        await using PrecedenciaCadastradaNoTeste relacao = await PrecedenciaCadastradaNoTeste.CadastrarAsync(fixture.Factory.Services, ResultadoFinal, "AVALIACAO_BIOPSICOSSOCIAL");

        using HttpResponseMessage recusada = await processo.PublicarAsync();
        await relacao.RetirarAsync();
        using HttpResponseMessage publicada = await processo.PublicarAsync();

        (await ErrosDaRecusaAsync(recusada)).Should().Contain(e => e.Codigo == Prefixo + "fases_com_precedencia_no_mesmo_momento");
        publicada.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicada.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "A publicação confere a ordem invertida entre fases ligadas por precedência e aponta a fase na lista do documento")]
    public async Task OrdemInvertida_RecusaNaPublicacao()
    {
        ProcessoPublicavelComAMarca processo = await ProcessoPublicavelComAMarca.SemearAsync(
            fixture, "Precedência invertida", 2, Criar(Habilitacao, 1, 3));

        using HttpResponseMessage recusada = await processo.PublicarAsync();

        ErroDaRecusa erro = (await ErrosDaRecusaAsync(recusada)).Should().ContainSingle(e => e.Codigo == Prefixo + "precedencia_fase_violada").Which;
        erro.Campo.Should().Be("/cronograma/fases/0");
        erro.Identificadores.Should().Equal(new ItemIdentificado("fases", Habilitacao));
    }

    [Fact(DisplayName = "A publicação confere a sobreposição que o cadastro não permite entre fases ligadas por precedência e aponta o início da fase")]
    public async Task SobreposicaoNaoPermitida_RecusaNaPublicacao()
    {
        DateTimeOffset inicio = new(2026, 1, 20, 0, 0, 0, TimeSpan.Zero);
        ProcessoPublicavelComAMarca processo = await ProcessoPublicavelComAMarca.SemearAsync(
            fixture, "Períodos que se sobrepõem", 1, Criar(Habilitacao, 2, inicio, inicio.AddDays(20)));

        using HttpResponseMessage recusada = await processo.PublicarAsync();

        ErroDaRecusa erro = (await ErrosDaRecusaAsync(recusada)).Should().ContainSingle(e => e.Codigo == Prefixo + "sobreposicao_de_janelas_nao_permitida").Which;
        erro.Campo.Should().Be("/cronograma/fases/1/inicio");
        erro.Identificadores.Should().Equal(new ItemIdentificado("fases", Habilitacao));
    }

    [Fact(DisplayName = "A fase marcada sem relação de precedência no cadastro é publicada")]
    public async Task FaseMarcadaSemPrecedencia_Publica()
    {
        ProcessoPublicavelComAMarca processo = await ProcessoPublicavelComAMarca.SemearAsync(
            fixture, "Marca sem precedência", 1, Criar("ENSALAMENTO", 2, 3, noMesmoMomentoDaAnterior: true));

        using HttpResponseMessage publicada = await processo.PublicarAsync();

        publicada.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicada.Content.ReadAsStringAsync());
    }
}
