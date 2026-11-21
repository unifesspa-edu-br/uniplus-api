namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.CorpoDoCronogramaComMarca;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.FaseDoCronogramaPublicavelDeTeste;

/// <summary>
/// A precedência entre fases no painel de conformidade e na retificação, pelo HTTP e contra Postgres real. O painel é previsão sem
/// bloqueio e dá, para os mesmos dados, a resposta da publicação. A retificação só lê o cadastro de precedência quando o cronograma
/// mudou desde a versão publicada, lida do registro congelado; a relação cadastrada depois não recusa a retificação que não toca o
/// cronograma. As relações usadas são as do catálogo de 14 fases.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class PrecedenciaNoPainelENaRetificacaoPeloEndpointTests(CascadingFixture fixture)
{
    private const string CodigoDaRecusa = "uniplus.selecao.processo_seletivo.fases_com_precedencia_no_mesmo_momento";
    private const string Habilitacao = "HABILITACAO";

    [Fact(DisplayName = "Fases ligadas por precedência no mesmo momento: o painel dá o item por pendente e a publicação recusa pelo mesmo motivo")]
    public async Task MesmosDados_PainelPendenteEPublicacaoRecusa()
    {
        ProcessoPublicavelComAMarca processo = await ProcessoPublicavelComAMarca.SemearAsync(fixture, "Painel e publicação recusados", 1, Criar(Habilitacao, 2, 3, noMesmoMomentoDaAnterior: true));

        bool conformeNoPainel = await processo.PrecedenciaConformeNoPainelAsync();
        using HttpResponseMessage publicacao = await processo.PublicarAsync();

        conformeNoPainel.Should().BeFalse();
        (await ErrosDaRecusaAsync(publicacao)).Should().Contain(e => e.Codigo == CodigoDaRecusa);
    }

    [Fact(DisplayName = "Fase marcada sem relação de precedência: o painel dá o item por conforme e a publicação aceita")]
    public async Task MesmosDados_PainelConformeEPublicacaoAceita()
    {
        ProcessoPublicavelComAMarca processo = await ProcessoPublicavelComAMarca.SemearAsync(fixture, "Painel e publicação aceitos", 1, Criar("ENSALAMENTO", 2, 3, noMesmoMomentoDaAnterior: true));

        bool conformeNoPainel = await processo.PrecedenciaConformeNoPainelAsync();
        using HttpResponseMessage publicacao = await processo.PublicarAsync();

        conformeNoPainel.Should().BeTrue();
        publicacao.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicacao.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "A precedência cadastrada depois da publicação não recusa a retificação que não muda o cronograma, e o painel não a lê")]
    public async Task CronogramaIgualAoPublicado_NaoLePrecedenciaNaRetificacaoNemNoPainel()
    {
        ProcessoPublicavelComAMarca processo = await ProcessoPublicavelComAMarca.SemearAsync(fixture, "Cronograma igual ao publicado", 1, Criar("AVALIACAO_BIOPSICOSSOCIAL", 2, 3, noMesmoMomentoDaAnterior: true));
        using HttpResponseMessage publicada = await processo.PublicarAsync();
        publicada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using PrecedenciaCadastradaNoTeste relacao = await PrecedenciaCadastradaNoTeste.CadastrarAsync(fixture.Factory.Services, "RESULTADO_FINAL", "AVALIACAO_BIOPSICOSSOCIAL");

        bool conformeNoPainel = await processo.PrecedenciaConformeNoPainelAsync();
        using HttpResponseMessage retificada = await processo.RetificarAsync();

        conformeNoPainel.Should().BeTrue();
        retificada.StatusCode.Should().Be(HttpStatusCode.NoContent, await retificada.Content.ReadAsStringAsync());
        (await processo.VersoesPublicadasAsync()).Should().Be(2);
    }

    [Fact(DisplayName = "O cronograma que mudou desde a versão publicada tem a precedência lida: o painel o dá por pendente e a retificação recusa sem criar versão")]
    public async Task CronogramaQueMudou_LePrecedenciaNaRetificacaoENoPainel()
    {
        ProcessoPublicavelComAMarca processo = await ProcessoPublicavelComAMarca.SemearAsync(fixture, "Cronograma que mudou", 1, Criar(Habilitacao, 2, 3));
        using HttpResponseMessage publicada = await processo.PublicarAsync();
        publicada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await processo.MarcarAFaseNoBancoAsync(Habilitacao, marca: true);

        bool conformeNoPainel = await processo.PrecedenciaConformeNoPainelAsync();
        using HttpResponseMessage retificada = await processo.RetificarAsync();

        conformeNoPainel.Should().BeFalse();
        ErroDaRecusa erro = (await ErrosDaRecusaAsync(retificada)).Should().ContainSingle(e => e.Codigo == CodigoDaRecusa).Which;
        erro.Campo.Should().Be("/cronograma/fases/1/noMesmoMomentoDaAnterior");
        erro.Identificadores.Should().Equal(new ItemIdentificado("fases", Habilitacao));
        (await processo.VersoesPublicadasAsync()).Should().Be(1);
    }

    [Fact(DisplayName = "A versão publicada que não pode ser lida interrompe a retificação com o erro de leitura: nem aceita nem recusa por precedência")]
    public async Task VersaoPublicadaIlegivel_InterrompeComOErroDeLeituraSemRecusarPorPrecedencia()
    {
        ProcessoPublicavelComAMarca processo = await ProcessoPublicavelComAMarca.SemearAsync(fixture, "Versão ilegível", 1, Criar(Habilitacao, 2, 3));
        using HttpResponseMessage publicada = await processo.PublicarAsync();
        publicada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await processo.SucederComORegistroForaDaFormaAsync();

        using HttpResponseMessage retificada = await processo.RetificarAsync();

        string corpo = await retificada.Content.ReadAsStringAsync();
        retificada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        corpo.Should().Contain("uniplus.selecao.envelope_codec.envelope_malformado").And.NotContain(CodigoDaRecusa);
        (await processo.VersoesPublicadasAsync()).Should().Be(2, "só a versão sucessora que o teste inseriu: a retificação não criou outra");
    }

    [Fact(DisplayName = "O painel com a versão publicada ilegível responde 200 com o item da versão em falha e continua conferindo a precedência: o cronograma que a viola vira pendência")]
    public async Task PainelComVersaoIlegivel_ItemEmFalhaEPrecedenciaConferida()
    {
        ProcessoPublicavelComAMarca processo = await PublicadoComAVersaoIlegivelAsync(marcarAHabilitacao: true);

        IReadOnlyList<ProcessoPublicavelComAMarca.ItemDoPainel> itens = await processo.ItensDoPainelAsync();

        ItemDaVersaoIlegivel(itens).Should().BeEquivalentTo(new { Dimensao = "cronograma", Ok = false });
        ItemDaVersaoIlegivel(itens).Mensagem.Should().Contain("noMesmoMomentoDaAnterior");
        itens.Single(static i => i.Codigo == ProcessoPublicavelComAMarca.ItemDaPrecedenciaNoPainel).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "O painel com a versão publicada ilegível dá a precedência por conforme quando o cronograma não a viola: a leitura da versão não a esconde")]
    public async Task PainelComVersaoIlegivel_PrecedenciaConformeQuandoOCronogramaAEstaRespeitando()
    {
        ProcessoPublicavelComAMarca processo = await PublicadoComAVersaoIlegivelAsync(marcarAHabilitacao: false);

        IReadOnlyList<ProcessoPublicavelComAMarca.ItemDoPainel> itens = await processo.ItensDoPainelAsync();

        ItemDaVersaoIlegivel(itens).Ok.Should().BeFalse();
        itens.Single(static i => i.Codigo == ProcessoPublicavelComAMarca.ItemDaPrecedenciaNoPainel).Ok.Should().BeTrue();
    }

    private async Task<ProcessoPublicavelComAMarca> PublicadoComAVersaoIlegivelAsync(bool marcarAHabilitacao)
    {
        ProcessoPublicavelComAMarca processo = await ProcessoPublicavelComAMarca.SemearAsync(fixture, "Painel com versão ilegível", 1, Criar(Habilitacao, 2, 3));
        using HttpResponseMessage publicada = await processo.PublicarAsync();
        publicada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await processo.SucederComORegistroForaDaFormaAsync();
        if (marcarAHabilitacao)
        {
            await processo.MarcarAFaseNoBancoAsync(Habilitacao, marca: true);
        }

        return processo;
    }

    private static ProcessoPublicavelComAMarca.ItemDoPainel ItemDaVersaoIlegivel(IReadOnlyList<ProcessoPublicavelComAMarca.ItemDoPainel> itens) =>
        itens.Single(static i => i.Codigo == "cronograma_versao_publicada_legivel");
}
