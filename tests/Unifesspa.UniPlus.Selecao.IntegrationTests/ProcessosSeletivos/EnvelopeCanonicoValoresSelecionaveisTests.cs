namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.EnvelopeDeReferencia;

/// <summary>
/// Valores selecionáveis dos fatos coletados no envelope canônico (issue #1059, UNI-REQ-0072):
/// a totalidade exigida do encoder e a ordem canônica dos valores.
/// </summary>
public sealed class EnvelopeCanonicoValoresSelecionaveisTests
{
    // O processo de referência coleta COR_RACA e RENDA, ambos ESCOLHA_UNICA — canonicalizar SEM
    // passar ValoresSelecionaveisCongelados (dicionário nulo) tem de LANÇAR, nunca emitir
    // "valoresSelecionaveis": [] por omissão. É a contraprova de D1-ter: sem esta guarda, um
    // handler que esquecesse de resolver o dicionário publicaria um seletor MUDO, e a prova de
    // round-trip passaria mesmo assim — o round-trip prova preservação do que foi emitido, não
    // completude do que devia ser.
    [Fact(DisplayName = "SerializarFatosColetados lança quando um fato de seleção coletado não tem entrada no dicionário de valores selecionáveis")]
    public void SerializarFatosColetados_SemEntradaParaFatoDeSelecao_Lanca()
    {
        ProcessoSeletivo processo = ProcessoDeReferencia();
        DadosEdital dados = DadosDeReferencia();

        Action canonicalizarSemDicionario = () => Canonicalizer.Canonicalizar(
            EntradaDeReferencia(processo, dados, MetadadosFatosDeReferencia()));

        canonicalizarSemDicionario.Should().Throw<InvalidOperationException>()
            .WithMessage("*NACIONALIDADE*",
                "NACIONALIDADE é o primeiro fato de seleção coletado, na seção do conjunto básico — a ausência de " +
                "entrada para ele é o primeiro erro de programação que o encoder encontra");
    }

    // Diferente do teste acima (entrada AUSENTE): aqui a entrada existe, mas é []. O gate de
    // pré-canonicalização (ProcessoSeletivo.PendenciaDeFatoColetadoSemValoresOfertados, issue
    // #1077, §3, CA-12) deveria ter recusado a publicação antes deste ponto — chegar aqui com
    // lista vazia é erro de programação do handler (esqueceu o gate ou o resolvedor), nunca um
    // seletor legítimo sem opção.
    [Fact(DisplayName = "SerializarFatosColetados lança quando o dicionário traz lista VAZIA para um fato de seleção")]
    public void SerializarFatosColetados_ComListaVaziaParaFatoDeSelecao_Lanca()
    {
        ProcessoSeletivo processo = ProcessoDeReferencia();
        DadosEdital dados = DadosDeReferencia();
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresComListaVazia =
            new(ValoresSelecionaveisDeReferencia(), StringComparer.Ordinal) { ["COR_RACA"] = [] };

        Action canonicalizarComListaVazia = () => Canonicalizer.Canonicalizar(
            EntradaDeReferencia(processo, dados, MetadadosFatosDeReferencia(), valoresComListaVazia));

        canonicalizarComListaVazia.Should().Throw<InvalidOperationException>()
            .WithMessage("*COR_RACA*",
                "um seletor de ESCOLHA_UNICA/ESCOLHA_MULTIPLA publicado sem opção nenhuma não é respondível — " +
                "o canonicalizer nunca emite \"valoresSelecionaveis\": []");
    }

    // A entrada de teste insere PRETA (ordem 1) ANTES de BRANCA (ordem 0): se o encoder dependesse
    // da ordem de inserção do dicionário, o array sairia [PRETA, BRANCA]. Ele sai
    // [BRANCA, PRETA, PARDA] — por Ordem ascendente, não por ordem de chegada.
    [Fact(DisplayName = "valoresSelecionaveis[].ordem e definicoesDosDados[].valoresDominioDeclarados[].ordem batem com a Ordem de origem, ordenados por Ordem/Codigo — independente da ordem de inserção no dicionário")]
    public void Ordem_BateComAOrigemEEhCanonica_NosDoisBlocos()
    {
        JsonObject envelope = EnvelopeComoObjeto();
        JsonArray selecionaveis = ValoresSelecionaveisDoFato(envelope, "COR_RACA");

        CodigosDosValores(selecionaveis).Should().Equal(["BRANCA", "PRETA", "PARDA"]);
        OrdensDosValores(selecionaveis)
            .Should().Equal([0, 1, 2], "ordem bate com a Ordem de origem de cada valor — 0 para BRANCA, 1 para PRETA, 2 para PARDA");

        JsonArray declarados = ValoresDeclaradosNosMetadadosDoFato(envelope, "COR_RACA");

        CodigosDosValores(declarados).Should().Equal(
            ["BRANCA", "PRETA", "PARDA"],
            "o mesmo vocabulário do MESMO fato do catálogo, congelado nos DOIS blocos, na MESMA ordem canônica");
        OrdensDosValores(declarados).Should().Equal([0, 1, 2]);
    }

    [Fact(DisplayName = "Dois valores com a MESMA Ordem desempatam por Codigo de forma determinística — nos dois blocos")]
    public void Ordem_ComEmpate_DesempataPorCodigoDeFormaDeterministica()
    {
        IReadOnlyList<ValorDominioDeclaradoCongelado> empatados =
        [
            new ValorDominioDeclaradoCongelado("ZETA", "Zeta.", 0),
            new ValorDominioDeclaradoCongelado("ALFA", "Alfa.", 0),
        ];

        SnapshotCanonico canonico = CanonicalizarComValoresDaCorRaca(empatados);

        JsonArray valoresCorRaca = ValoresSelecionaveisDoFato(ApoioDoEnvelopeCodec.Envelope(canonico), "COR_RACA");

        CodigosDosValores(valoresCorRaca).Should().Equal(
            ["ALFA", "ZETA"],
            "empate de Ordem (ambos 0) desempata por Codigo ordinal — 'ALFA' antes de 'ZETA', " +
            "independente da ordem em que os dois entraram na lista");
    }

    private static SnapshotCanonico CanonicalizarComValoresDaCorRaca(IReadOnlyList<ValorDominioDeclaradoCongelado> valores)
    {
        ProcessoSeletivo processo = ProcessoDeReferencia();
        DadosEdital dados = DadosDeReferencia();
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis =
            new(ValoresSelecionaveisDeReferencia()) { ["COR_RACA"] = valores };

        return Canonicalizer.Canonicalizar(EntradaDeReferencia(
            processo, dados, MetadadosFatosDeReferencia(), CatalogoDoConjuntoBasico.ComValoresCongelados(valoresSelecionaveis)));
    }

    private static JsonArray ValoresSelecionaveisDoFato(JsonObject envelope, string fatoCodigo) =>
        envelope["dadosDosFormularios"]!.AsArray()
            .Single(f => f!["codigoDoDado"]!.GetValue<string>() == fatoCodigo)!["valoresSelecionaveis"]!.AsArray();

    private static JsonArray ValoresDeclaradosNosMetadadosDoFato(JsonObject envelope, string fatoCodigo) =>
        envelope["documentosExigidos"]!["definicoesDosDados"]!.AsArray()
            .Single(m => m!["codigoDoDado"]!.GetValue<string>() == fatoCodigo)!["opcoesDeclaradas"]!.AsArray();

    private static IEnumerable<string> CodigosDosValores(JsonArray valores) =>
        valores.Select(v => v!["valorCodigo"]!.GetValue<string>());

    private static IEnumerable<int> OrdensDosValores(JsonArray valores) =>
        valores.Select(v => v!["ordem"]!.GetValue<int>());
}
