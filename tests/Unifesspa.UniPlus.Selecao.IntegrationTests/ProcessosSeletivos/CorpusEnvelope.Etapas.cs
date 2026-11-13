namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static partial class CorpusEnvelope
{
    // A objetiva carrega o recurso da ETAPA, e carrega os dois casos que o envelope
    // precisava aprender a congelar: a suspensividade declarada nos dois pares — que voltava
    // nula — e a âncora por CIÊNCIA, cujo produto é ausente e cujo Guid vazio fazia o
    // envelope recusar a si mesmo na volta. Sem isto no corpus, o round-trip passava
    // exatamente por não exercitar recurso de etapa nenhum.
    private static EtapaProcesso EtapaObjetiva(int variante, bool permutar)
    {
        EtapaProcesso etapa = NovaEtapaObjetiva(variante, permutar);
        etapa.DefinirJanelaEParecer(null, null, emiteParecerIndividual: true).IsSuccess.Should().BeTrue();
        etapa.DefinirProdutos(ProdutosDaObjetiva(variante, permutar)).IsSuccess.Should().BeTrue();
        etapa.DefinirBancas(BancasDaObjetiva(variante, permutar)).IsSuccess.Should().BeTrue();
        etapa.DefinirRecursos(RecursosDaObjetiva(variante)).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private static EtapaProcesso NovaEtapaObjetiva(int variante, bool permutar) =>
        EtapaProcesso.Reidratar(
            EtapaId(1, variante), "Prova Objetiva", CaraterEtapa.Ambas, TipoEtapaProvaObjetiva(), peso: 3.5000m, notaMinima: 40.0000m, ordem: 1, notaMaxima: 100.0000m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva,
            itensDeAvaliacao: Ordem([
                ItemDeAvaliacao.Reidratar(ItemDeAvaliacaoId(2, variante), "Bloco de Matemática", 40.0000m),
                ItemDeAvaliacao.Reidratar(ItemDeAvaliacaoId(1, variante), "Bloco de Linguagens", 60.0000m),
            ], permutar), faseCodigo: "RESULTADO_PRELIMINAR");

    // A MESMA matéria nos dois papéis: é o par que a ordenação canônica precisa desempatar,
    // e é o único caso em que o código do ato não basta para fixar a posição no envelope.
    //
    // Os dois produtos seguintes existem como PAR, e é o par que importa. O acento em forma
    // decomposta faz a normalização deixar de ser identidade; a escolha do companheiro
    // faz a ordem entre eles VIRAR conforme o texto esteja normalizado ou não — os dois
    // coincidem até `RESULTADO_AN`, e ali o `U` fica entre o `A` base da forma decomposta
    // e o `Á` inteiro da composta.
    //
    // Sem essa inversão, normalizar a chave de ordenação era indistinguível de não
    // normalizar: dois códigos acentuados quaisquer divergem antes de chegar ao acento e
    // saem na mesma ordem das duas formas. Com ela, remover a normalização da chave
    // reordena o documento e a fixture recusa.
    //
    // De quebra, dão ao bloco de recursos uma segunda âncora preliminar de verdade. A etapa fecha
    // o próprio ciclo: cada preliminar tem o definitivo do mesmo tipo nela.
    private static IReadOnlyList<ProdutoDaEtapa> ProdutosDaObjetiva(int variante, bool permutar) =>
        Ordem([
            ProdutoDaEtapa.Reidratar(ProdutoDaObjetivaId(1, variante), "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Reidratar(ProdutoDaObjetivaId(2, variante), "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo),
            ProdutoDaEtapa.Reidratar(ProdutoDaObjetivaId(3, variante), AtoAnaliseDecomposto, PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Reidratar(ProdutoDaObjetivaId(4, variante), AtoAnulacao, PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Reidratar(ProdutoDaObjetivaId(5, variante), AtoAnaliseDecomposto, PapelProdutoFase.Definitivo),
            ProdutoDaEtapa.Reidratar(ProdutoDaObjetivaId(6, variante), AtoAnulacao, PapelProdutoFase.Definitivo),
        ], permutar);

    // A etapa requer banca: é o bloco em que o envelope congela o TIPO e o código, e não a
    // linha — o id dela muda a cada gravação, porque o comando declara a banca pelo tipo.
    // Três bancas, declaradas fora da ordem que o envelope emite: com uma só, a ordenação
    // por código não se manifestava. Duas delas são o mesmo par dos produtos — acento
    // decomposto e um companheiro escolhido para que a ordem entre eles vire com a
    // normalização —, aplicado ao outro campo que a canonicalização ordena por texto.
    private static IReadOnlyList<BancaDaEtapa> BancasDaObjetiva(int variante, bool permutar) =>
        Ordem([
            BancaDaEtapa.Reidratar(BancaDaObjetivaId(2, variante), TipoBancaRecursal, BancaAnaliseDecomposta),
            BancaDaEtapa.Reidratar(BancaDaObjetivaId(3, variante), TipoBancaAnulacao, BancaAnulacao),
            BancaDaEtapa.Reidratar(BancaDaObjetivaId(1, variante), TipoBancaExaminadora, "BANCA_EXAMINADORA"),
        ], permutar);

    private static IReadOnlyList<RecursoDaEtapa> RecursosDaObjetiva(int variante) =>
    [
        RecursoDaObjetivaAncoradoNoPrimeiroProduto(variante),
        RecursoDaObjetivaAncoradoNoSegundoProduto(variante),
        RecursoDaObjetivaPorCiencia(variante),
    ];

    private static RecursoDaEtapa RecursoDaObjetivaAncoradoNoPrimeiroProduto(int variante) =>
        RecursoDaEtapa.Reidratar(
            RecursoDaObjetivaId(1, variante),
            AncoraDoRecurso.AtoPublicado,
            Regra(RegraPrazoRecursoCodigo.AncoradoEmAto, '9'),
            new ArgsRegraPrazoRecurso(
                3.0000m, UnidadePrazo.DiasUteis,
                2.0000m, UnidadePrazo.DiasUteis,
                true,
                1.0000m, UnidadePrazo.DiasUteis),
            ProdutoDaObjetivaId(1, variante)).Value!;

    // Segunda janela ancorada em ato, num produto DIFERENTE do primeiro — é o par que a
    // ordenação das janelas recursais precisa desempatar. Ela ordena pela chave do
    // produto âncora, não pelo id dele: com uma âncora só, trocar um critério pelo outro
    // não mudava nada, e era possível reverter a política sem quebrar fixture nenhuma.
    private static RecursoDaEtapa RecursoDaObjetivaAncoradoNoSegundoProduto(int variante) =>
        RecursoDaEtapa.Reidratar(
            RecursoDaObjetivaId(2, variante),
            AncoraDoRecurso.AtoPublicado,
            Regra(RegraPrazoRecursoCodigo.AncoradoEmAto, '9'),
            // Dia útil, e não dia corrido: `UnidadePrazo.Dias` é recusado pelo domínio, e um
            // corpus que o carregue congela no envelope uma configuração que o caminho de
            // escrita nunca produziria.
            new ArgsRegraPrazoRecurso(
                5.0000m, UnidadePrazo.DiasUteis,
                null, null, false, null, null),
            ProdutoDaObjetivaId(3, variante)).Value!;

    private static RecursoDaEtapa RecursoDaObjetivaPorCiencia(int variante) =>
        RecursoDaEtapa.Reidratar(
            RecursoDaObjetivaId(3, variante),
            AncoraDoRecurso.CienciaIndividual,
            Regra(RegraPrazoRecursoCodigo.AncoradoEmAto, '9'),
            new ArgsRegraPrazoRecurso(48.0000m, UnidadePrazo.Horas, null, null, false, null, null),
            Guid.Empty).Value!;

    private static EtapaProcesso EtapaRedacao(int variante) =>
        EtapaProcesso.Reidratar(EtapaId(2, variante), "Redação", CaraterEtapa.Classificatoria, TipoEtapaRedacao(), peso: 2.2500m, notaMinima: null, ordem: 2, notaMaxima: 100.0000m,
            formaDeAvaliacao: FormaDeAvaliacao.Objetiva, faseCodigo: "RESULTADO_PRELIMINAR");

    // A etapa de avaliação humana exige ao menos uma banca para publicar.
    private static EtapaProcesso EtapaEntrevista(int variante, bool permutar)
    {
        EtapaProcesso etapa = EtapaProcesso.Reidratar(EtapaId(3, variante), "Entrevista", CaraterEtapa.Eliminatoria, TipoEtapaEntrevista(), peso: null, notaMinima: 60.0000m, ordem: 3, notaMaxima: 100.0000m, passoDoLancamento: 0.5000m,
            formaDeAvaliacao: FormaDeAvaliacao.Humana, regraConsolidacao: Regra(RegraConsolidacaoCodigo.AvaliacaoUnica, 'f'),
            itensDeAvaliacao: Ordem([
                ItemDeAvaliacao.Reidratar(ItemDeAvaliacaoId(5, variante), "Primeiro acesso ao ensino superior", 20.5000m),
                ItemDeAvaliacao.Reidratar(ItemDeAvaliacaoId(6, variante), "Domínio do conteúdo", 79.5000m),
            ], permutar), faseCodigo: "RESULTADO_FINAL");
        etapa.DefinirBancas([BancaDaEtapa.Criar(TipoBancaExaminadora, "BANCA_EXAMINADORA")]).IsSuccess.Should().BeTrue();
        return etapa;
    }

    // A banca escolhe o nível de cada critério, sem digitar a nota: é o caso que congela a
    // escala de atendimento e dispensa o passo do lançamento.
    private static EtapaProcesso EtapaDefesa(int variante, bool permutar)
    {
        EtapaProcesso etapa = EtapaProcesso.Reidratar(
            EtapaId(4, variante), "Defesa oral", CaraterEtapa.Eliminatoria, TipoEtapaEntrevista(), peso: null, notaMinima: 50.0000m, ordem: 4, notaMaxima: 100.0000m,
            formaDeAvaliacao: FormaDeAvaliacao.Humana, regraConsolidacao: Regra(RegraConsolidacaoCodigo.CorrecaoIndependente, 'e'),
            itensDeAvaliacao: Ordem([
                ItemDeAvaliacao.Reidratar(ItemDeAvaliacaoId(4, variante), "Coesão da exposição", 30.0000m),
                ItemDeAvaliacao.Reidratar(ItemDeAvaliacaoId(3, variante), "Argumentação", 70.0000m),
            ], permutar),
            escalaDeAtendimento: EscalaDeAtendimentoDaDefesa(permutar), faseCodigo: "RESULTADO_FINAL");
        etapa.DefinirBancas([BancaDaEtapa.Criar(TipoBancaExaminadora, "BANCA_EXAMINADORA")]).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private static IReadOnlyList<NivelDeAtendimento> EscalaDeAtendimentoDaDefesa(bool permutar) =>
        Ordem([
            new NivelDeAtendimento("Atendeu parcialmente", 0.5000m),
            new NivelDeAtendimento("Atendeu", 1.0000m),
            new NivelDeAtendimento("Não atendeu", 0.0000m),
        ], permutar);
}
