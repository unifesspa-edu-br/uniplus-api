namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.CorpoDoCronogramaComMarca;

/// <summary>
/// A marca "no mesmo momento da anterior" na gravação do cronograma pelo HTTP, contra Postgres real: gravada, lida de volta,
/// substituída por inteiro, e recusada com o campo posicional quando a primeira fase a leva ou quando duas fases ligadas por
/// precedência no cadastro ficam no mesmo momento. As relações usadas são as do catálogo de 14 fases, semeadas pela migration
/// (resultado final antes de habilitação).
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class MarcaNoCronogramaPeloEndpointTests(CascadingFixture ambienteDeTeste)
{
    private const string CodigoDaPrimeira = "uniplus.selecao.processo_seletivo.primeira_fase_no_mesmo_momento_da_anterior";
    private const string CodigoDaPrecedencia = "uniplus.selecao.processo_seletivo.fases_com_precedencia_no_mesmo_momento";

    [Fact(DisplayName = "A marca gravada na segunda fase é lida de volta na posição dela, e as fases sem marca ou com marca falsa voltam sem marca")]
    public async Task MarcaGravada_ELidaNaPosicaoDaFase()
    {
        ProcessoEmRascunhoParaOCronograma processo = await ProcessoEmRascunhoParaOCronograma.PrepararAsync(ambienteDeTeste, "ENSALAMENTO", "AVALIACAO_BIOPSICOSSOCIAL", "CLASSIFICACAO");

        using HttpResponseMessage gravada = await processo.GravarAsync(
            processo.Fase("ENSALAMENTO", 1), processo.Fase("AVALIACAO_BIOPSICOSSOCIAL", 2, marca: true), processo.Fase("CLASSIFICACAO", 3, marca: false));

        gravada.StatusCode.Should().Be(HttpStatusCode.NoContent, await gravada.Content.ReadAsStringAsync());
        (await processo.MarcasLidasAsync()).Should().Equal(false, true, false);
    }

    [Fact(DisplayName = "Gravar de novo sem a propriedade da marca a retira: a gravação substitui o cronograma inteiro")]
    public async Task GravarSemAPropriedade_RetiraAMarca()
    {
        ProcessoEmRascunhoParaOCronograma processo = await ProcessoEmRascunhoParaOCronograma.PrepararAsync(ambienteDeTeste, "ENSALAMENTO", "AVALIACAO_BIOPSICOSSOCIAL");
        using HttpResponseMessage comMarca = await processo.GravarAsync(processo.Fase("ENSALAMENTO", 1), processo.Fase("AVALIACAO_BIOPSICOSSOCIAL", 2, marca: true));
        comMarca.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpResponseMessage semMarca = await processo.GravarAsync(processo.Fase("ENSALAMENTO", 1), processo.Fase("AVALIACAO_BIOPSICOSSOCIAL", 2));

        semMarca.StatusCode.Should().Be(HttpStatusCode.NoContent, await semMarca.Content.ReadAsStringAsync());
        (await processo.MarcasLidasAsync()).Should().Equal(false, false);
    }

    [Fact(DisplayName = "A marca na fase de menor ordem é recusada na posição que ela tem no corpo enviado, sem identificadores")]
    public async Task MarcaNaPrimeiraFase_RecusaNoCampoPosicionalSemIdentificadores()
    {
        ProcessoEmRascunhoParaOCronograma processo = await ProcessoEmRascunhoParaOCronograma.PrepararAsync(ambienteDeTeste, "ENSALAMENTO", "AVALIACAO_BIOPSICOSSOCIAL");

        using HttpResponseMessage recusada = await processo.GravarAsync(
            processo.Fase("AVALIACAO_BIOPSICOSSOCIAL", 2), processo.Fase("ENSALAMENTO", 1, marca: true));

        ErroDaRecusa erro = (await ErrosDaRecusaAsync(recusada)).Should().ContainSingle().Which;
        (erro.Campo, erro.Codigo).Should().Be((CampoDaMarca(1), CodigoDaPrimeira));
        erro.Identificadores.Should().BeNull("na gravação a posição enviada basta, e a identificação só existe na recusa da publicação");
        erro.Mensagem.Should().NotContain("ENSALAMENTO", "a recusa da primeira fase não cita código de fase");
    }

    [Fact(DisplayName = "Duas fases vizinhas ligadas por precedência no cadastro e no mesmo momento são recusadas na fase marcada")]
    public async Task FasesVizinhasLigadasPorPrecedencia_RecusaNaFaseMarcada()
    {
        ProcessoEmRascunhoParaOCronograma processo = await ProcessoEmRascunhoParaOCronograma.PrepararAsync(ambienteDeTeste, "RESULTADO_FINAL", "HABILITACAO");

        using HttpResponseMessage recusada = await processo.GravarAsync(processo.Fase("RESULTADO_FINAL", 1), processo.Fase("HABILITACAO", 2, marca: true));

        ErroDaRecusa erro = (await ErrosDaRecusaAsync(recusada)).Should().ContainSingle().Which;
        (erro.Campo, erro.Codigo).Should().Be((CampoDaMarca(1), CodigoDaPrecedencia));
        erro.Identificadores.Should().BeNull();
        erro.Mensagem.Should().Contain("'Resultado final'").And.Contain("'Habilitação'").And.NotContain("RESULTADO_FINAL").And.NotContain("HABILITACAO", "a mensagem cita o nome da fase, nunca o código");
    }

    [Fact(DisplayName = "A primeira e a terceira fase de um grupo no mesmo momento, ligadas por precedência, são recusadas mesmo sem serem vizinhas")]
    public async Task PrimeiraETerceiraDoGrupoLigadasPorPrecedencia_RecusaNaTerceira()
    {
        ProcessoEmRascunhoParaOCronograma processo = await ProcessoEmRascunhoParaOCronograma.PrepararAsync(ambienteDeTeste, "RESULTADO_FINAL", "ENSALAMENTO", "HABILITACAO");

        using HttpResponseMessage recusada = await processo.GravarAsync(
            processo.Fase("RESULTADO_FINAL", 1), processo.Fase("ENSALAMENTO", 2, marca: true), processo.Fase("HABILITACAO", 3, marca: true));

        ErroDaRecusa erro = (await ErrosDaRecusaAsync(recusada)).Should().ContainSingle().Which;
        (erro.Campo, erro.Codigo).Should().Be((CampoDaMarca(2), CodigoDaPrecedencia));
    }

    [Fact(DisplayName = "As fases ligadas por precedência sem a marca são aceitas: a recusa vem do mesmo momento, não da relação")]
    public async Task FasesLigadasPorPrecedenciaSemAMarca_Aceitas()
    {
        ProcessoEmRascunhoParaOCronograma processo = await ProcessoEmRascunhoParaOCronograma.PrepararAsync(ambienteDeTeste, "RESULTADO_FINAL", "HABILITACAO");

        using HttpResponseMessage gravada = await processo.GravarAsync(processo.Fase("RESULTADO_FINAL", 1), processo.Fase("HABILITACAO", 2));

        gravada.StatusCode.Should().Be(HttpStatusCode.NoContent, await gravada.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "A ordem invertida entre fases ligadas por precedência continua recusada como precedência violada, sem a recusa da marca")]
    public async Task OrdemInvertida_ContinuaPrecedenciaFaseViolada()
    {
        ProcessoEmRascunhoParaOCronograma processo = await ProcessoEmRascunhoParaOCronograma.PrepararAsync(ambienteDeTeste, "RESULTADO_FINAL", "HABILITACAO");

        using HttpResponseMessage recusada = await processo.GravarAsync(processo.Fase("HABILITACAO", 1), processo.Fase("RESULTADO_FINAL", 2));

        ErroDaRecusa erro = (await ErrosDaRecusaAsync(recusada)).Should().ContainSingle().Which;
        (erro.Campo, erro.Codigo).Should().Be(("fases[0].ordem", "uniplus.selecao.processo_seletivo.precedencia_fase_violada"));
    }

    [Fact(DisplayName = "Ordens repetidas recusam só a ordem repetida e a precedência, sem emitir as recusas da marca")]
    public async Task OrdensRepetidas_NaoEmitemAsRecusasDaMarca()
    {
        ProcessoEmRascunhoParaOCronograma processo = await ProcessoEmRascunhoParaOCronograma.PrepararAsync(ambienteDeTeste, "RESULTADO_FINAL", "HABILITACAO");

        using HttpResponseMessage recusada = await processo.GravarAsync(
            processo.Fase("RESULTADO_FINAL", 1, mes: 1), processo.Fase("HABILITACAO", 1, marca: true, mes: 2));

        string[] codigos = [.. (await ErrosDaRecusaAsync(recusada)).Select(static erro => erro.Codigo)];
        codigos.Should().Contain("uniplus.selecao.processo_seletivo.ordem_fase_duplicada")
            .And.NotContain([CodigoDaPrimeira, CodigoDaPrecedencia]);
    }

    private static string CampoDaMarca(int posicao) =>
        string.Create(CultureInfo.InvariantCulture, $"fases[{posicao}].noMesmoMomentoDaAnterior");
}
