namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelope;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelopeCronograma;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelopeDadosDoEdital;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelopeDocumentos;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelopeEntrada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelopeProcessoRico;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelopeVagas;

internal static class CorpusEnvelopeEtapas
{
    // A objetiva carrega o recurso da ETAPA com os dois casos que o envelope congela: o prazo da 2ª
    // instância declarado e o evento que inicia o prazo por CIÊNCIA, cujo produto é ausente. Sem isto no corpus, o
    // round-trip não exercitaria recurso de etapa nenhum.
    internal static EtapaProcesso EtapaObjetiva(int variante, bool permutar)
    {
        EtapaProcesso etapa = NovaEtapaObjetiva(variante, permutar);
        etapa.DefinirJanelaEParecer(null, null, emiteParecerIndividual: true).IsSuccess.Should().BeTrue();
        etapa.DefinirProdutos(ProdutosDaObjetiva(variante, permutar)).IsSuccess.Should().BeTrue();
        etapa.DefinirBancas(BancasDaObjetiva(variante, permutar));
        etapa.DefinirRecursos(RecursosDaObjetiva(variante)).IsSuccess.Should().BeTrue();
        return etapa;
    }

    internal static EtapaProcesso NovaEtapaObjetiva(int variante, bool permutar) =>
        EtapaProcesso.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            EtapaId(1, variante),
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Ambas, TipoEtapa: TipoEtapaProvaObjetiva(), Ordem: 1, FaseCodigo: "RESULTADO_PRELIMINAR"),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 3.5000m, NotaMinima: 40.0000m, NotaMaxima: 100.0000m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null),
            itens: [
                ItemDeAvaliacao.Reidratar(ChaveLocal.Criar("item_matematica").Value, ItemDeAvaliacaoId(2, variante), "Bloco de Matemática", 40.0000m),
                ItemDeAvaliacao.Reidratar(ChaveLocal.Criar("item_linguagens").Value, ItemDeAvaliacaoId(1, variante), "Bloco de Linguagens", 60.0000m),
            ]);

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
    internal static IReadOnlyList<ProdutoDaEtapa> ProdutosDaObjetiva(int variante, bool permutar) =>
        Ordem([
            ProdutoDaEtapa.Reidratar(ProdutoDaObjetivaId(1, variante), "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Reidratar(ProdutoDaObjetivaId(2, variante), "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo),
            ProdutoDaEtapa.Reidratar(ProdutoDaObjetivaId(3, variante), AtoAnaliseDecomposto, PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Reidratar(ProdutoDaObjetivaId(4, variante), AtoAnulacao, PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Reidratar(ProdutoDaObjetivaId(5, variante), AtoAnaliseDecomposto, PapelProdutoFase.Definitivo),
            ProdutoDaEtapa.Reidratar(ProdutoDaObjetivaId(6, variante), AtoAnulacao, PapelProdutoFase.Definitivo),
        ], permutar);

    // A etapa requer banca: é o bloco em que o envelope congela a identidade da banca, a posição
    // dela no tipo, a cópia do tipo.
    // Três bancas de tipos diferentes, declaradas fora da ordem que o envelope emite: com uma só,
    // a ordenação pelo código do tipo não se manifestava.
    internal static IReadOnlyList<BancaDaEtapa> BancasDaObjetiva(int variante, bool permutar) =>
        Ordem([
            BancaDaEtapa.ReconstruirDosDadosSalvos(BancaDaObjetivaId(2, variante), TiposDeBancaCopiadosDeTeste.DeRecursos(TipoBancaRecursal), BancasDeTeste.Regra()),
            BancaDaEtapa.ReconstruirDosDadosSalvos(BancaDaObjetivaId(3, variante), TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.Heteroidentificacao, TipoBancaAnulacao), BancasDeTeste.Regra()),
            BancaDaEtapa.ReconstruirDosDadosSalvos(BancaDaObjetivaId(1, variante), TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, TipoBancaExaminadora), BancasDeTeste.Regra()),
        ], permutar);

    internal static IReadOnlyList<RecursoDaEtapa> RecursosDaObjetiva(int variante) =>
    [
        RecursoDaObjetivaAncoradoNoPrimeiroProduto(variante),
        RecursoDaObjetivaAncoradoNoSegundoProduto(variante),
        RecursoDaObjetivaPorCiencia(variante),
    ];

    internal static RecursoDaEtapa RecursoDaObjetivaAncoradoNoPrimeiroProduto(int variante) =>
        RecursoDaEtapa.Reidratar(
            RecursoDaObjetivaId(1, variante),
            AncoraDoRecurso.AtoPublicado,
            Regra(RegraPrazoRecursoCodigo.AncoradoEmAto, '9'),
            ParametrosDoRecursoDeTeste.Declarados(
                3m, UnidadePrazo.DiasUteis, admiteInstanciaSuperior: true, segundaInstancia: new PrazoDoRecurso(1m, UnidadePrazo.DiasUteis)),
            ProdutoDaObjetivaId(1, variante),
            ProcedimentosDoCorpusSemAlvo()).Value!;

    // Segunda janela ancorada em ato, num produto DIFERENTE do primeiro — é o par que a
    // ordenação das janelas recursais precisa desempatar. Ela ordena pela chave do
    // produto âncora, não pelo id dele: com uma âncora só, trocar um critério pelo outro
    // não mudava nada, e era possível reverter a política sem quebrar fixture nenhuma.
    internal static RecursoDaEtapa RecursoDaObjetivaAncoradoNoSegundoProduto(int variante) =>
        RecursoDaEtapa.Reidratar(
            RecursoDaObjetivaId(2, variante),
            AncoraDoRecurso.AtoPublicado,
            Regra(RegraPrazoRecursoCodigo.AncoradoEmAto, '9'),
            ParametrosDoRecursoDeTeste.Declarados(5.0000m, UnidadePrazo.DiasUteis),
            ProdutoDaObjetivaId(3, variante),
            ProcedimentosDoCorpusComAlvos()).Value!;

    private static ProcedimentosDeRecurso ProcedimentosDoCorpusSemAlvo() => ProcedimentosDeRecurso.De(
    [
        ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.Classificacao, null, Regra(RegraProcedimentoRecursoCodigo.Recalculo, '7')),
    ]);

    // Fora da ordem canônica de propósito: a coleção a normaliza, e o hash não pode depender da ordem de entrada.
    private static ProcedimentosDeRecurso ProcedimentosDoCorpusComAlvos() => ProcedimentosDeRecurso.De(
    [
        ProcedimentoDeRecursoDeclarado.De(
            TipoMotivoDeDecisao.Classificacao, AlvoDoProcedimentoDeRecurso.DaArea(AreaDoEnem.Matematica), Regra(RegraProcedimentoRecursoCodigo.Recalculo, '7')),
        ProcedimentoDeRecursoDeclarado.De(
            TipoMotivoDeDecisao.AnaliseDocumental,
            AlvoDoProcedimentoDeRecurso.DoItem(Guid.Parse("00000000-0000-7000-8000-0000000000a1")),
            Regra(RegraProcedimentoRecursoCodigo.Conferencia, '8')),
        ProcedimentoDeRecursoDeclarado.De(
            TipoMotivoDeDecisao.Classificacao, AlvoDoProcedimentoDeRecurso.DaArea(AreaDoEnem.Linguagens), Regra(RegraProcedimentoRecursoCodigo.Recalculo, '7')),
    ]);

    internal static RecursoDaEtapa RecursoDaObjetivaPorCiencia(int variante) =>
        RecursoDaEtapa.Reidratar(
            RecursoDaObjetivaId(3, variante),
            AncoraDoRecurso.CienciaIndividual,
            Regra(RegraPrazoRecursoCodigo.AncoradoEmAto, '9'),
            ParametrosDoRecursoDeTeste.Declarados(48.0000m, UnidadePrazo.Horas),
            Guid.Empty,
            ProcedimentosDoCorpusSemAlvo()).Value!;

    internal static EtapaProcesso EtapaRedacao(int variante) =>
        EtapaProcesso.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            EtapaId(2, variante),
            new IdentificacaoDaEtapa(Nome: "Redação", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaRedacao(), Ordem: 2, FaseCodigo: "RESULTADO_PRELIMINAR"),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 2.2500m, NotaMinima: null, NotaMaxima: 100.0000m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null),
            itens: null);

    // A etapa de avaliação humana exige ao menos uma banca para publicar.
    internal static EtapaProcesso EtapaEntrevista(int variante, bool permutar)
    {
        EtapaProcesso etapa = EtapaProcesso.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            EtapaId(3, variante),
            new IdentificacaoDaEtapa(Nome: "Entrevista", Carater: CaraterEtapa.Eliminatoria, TipoEtapa: TipoEtapaEntrevista(), Ordem: 3, FaseCodigo: "RESULTADO_FINAL"),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: null, NotaMinima: 60.0000m, NotaMaxima: 100.0000m, PassoDoLancamento: 0.5000m, FormaDeAvaliacao: FormaDeAvaliacao.Humana, RegraConsolidacao: Regra(RegraConsolidacaoCodigo.AvaliacaoUnica, 'f'), EscalaDeAtendimento: null),
            itens: [
                ItemDeAvaliacao.Reidratar(ChaveLocal.Criar("item_primeiro_acesso").Value, ItemDeAvaliacaoId(5, variante), "Primeiro acesso ao ensino superior", 20.5000m),
                ItemDeAvaliacao.Reidratar(ChaveLocal.Criar("item_dominio").Value, ItemDeAvaliacaoId(6, variante), "Domínio do conteúdo", 79.5000m),
            ]);
        etapa.DefinirBancas([BancaDaEtapa.ReconstruirDosDadosSalvos(BancaDaEtapaId(3, variante), TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, TipoBancaExaminadora), BancasDeTeste.Regra())]);
        etapa.DefinirTiposDeDecisao([TipoMotivoDeDecisao.NotaDaEtapa.Codigo(), TipoMotivoDeDecisao.Heteroidentificacao.Codigo()]).IsSuccess.Should().BeTrue();
        return etapa;
    }

    // A banca escolhe o nível de cada critério, sem digitar a nota: é o caso que congela a
    // escala de atendimento e dispensa o passo do lançamento.
    internal static EtapaProcesso EtapaDefesa(int variante, bool permutar)
    {
        EtapaProcesso etapa = EtapaProcesso.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            EtapaId(4, variante),
            new IdentificacaoDaEtapa(Nome: "Defesa oral", Carater: CaraterEtapa.Eliminatoria, TipoEtapa: TipoEtapaEntrevista(), Ordem: 4, FaseCodigo: "RESULTADO_FINAL"),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: null, NotaMinima: 50.0000m, NotaMaxima: 100.0000m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Humana, RegraConsolidacao: Regra(RegraConsolidacaoCodigo.CorrecaoIndependente, 'e'), EscalaDeAtendimento: EscalaDeAtendimentoDaDefesa()),
            itens: [
                ItemDeAvaliacao.Reidratar(ChaveLocal.Criar("item_coesao").Value, ItemDeAvaliacaoId(4, variante), "Coesão da exposição", 30.0000m),
                ItemDeAvaliacao.Reidratar(ChaveLocal.Criar("item_argumentacao").Value, ItemDeAvaliacaoId(3, variante), "Argumentação", 70.0000m),
            ]);
        etapa.DefinirBancas([BancaDaEtapa.ReconstruirDosDadosSalvos(BancaDaEtapaId(4, variante), TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, TipoBancaExaminadora), BancasDeTeste.Regra())]);
        return etapa;
    }

    internal static IReadOnlyList<NivelDeAtendimento> EscalaDeAtendimentoDaDefesa() =>
        [
            new NivelDeAtendimento("Atendeu parcialmente", 0.5000m),
            new NivelDeAtendimento("Atendeu", 1.0000m),
            new NivelDeAtendimento("Não atendeu", 0.0000m),
        ];
}
