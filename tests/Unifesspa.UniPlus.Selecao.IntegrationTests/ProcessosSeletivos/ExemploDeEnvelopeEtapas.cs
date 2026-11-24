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

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelope;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeCronograma;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeDadosDoEdital;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeDocumentos;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeEntrada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeProcessoRico;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeEnvelopeVagas;

internal static class ExemploDeEnvelopeEtapas
{
    // A objetiva carrega o recurso da ETAPA com os dois casos que o envelope congela: o prazo da 2ª
    // instância declarado e o evento que inicia o prazo por CIÊNCIA, cujo produto é ausente. Sem isto no exemplo, o
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
        EtapaProcesso.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa_prova_objetiva").Value!,
            EtapaId(1, variante),
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Ambas, TipoEtapa: TipoEtapaProvaObjetiva(), Ordem: 1, FaseCodigo: "RESULTADO_PRELIMINAR"),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 3.5000m, NotaMinima: 40.0000m, NotaMaxima: 100.0000m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null),
            itens: [
                ItemDeAvaliacao.Reidratar(ChaveLocal.Criar("item_matematica").Value, ItemDeAvaliacaoId(2, variante), "Bloco de Matemática", 40.0000m),
                ItemDeAvaliacao.Reidratar(ChaveLocal.Criar("item_linguagens").Value, ItemDeAvaliacaoId(1, variante), "Bloco de Linguagens", 60.0000m),
            ]);

    // O código e o papel determinam a ordem das publicações previstas.
    internal static IReadOnlyList<ProdutoDaEtapa> ProdutosDaObjetiva(int variante, bool permutar) =>
        Ordem([
            ProdutoDaEtapa.Reidratar(ProdutoDaObjetivaId(1, variante), "RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Preliminar),
            ProdutoDaEtapa.Reidratar(ProdutoDaObjetivaId(2, variante), "RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo),
            ProdutoDaEtapa.Reidratar(ProdutoDaObjetivaId(3, variante), AtoAnalise, PapelDaPublicacaoPrevista.Preliminar),
            ProdutoDaEtapa.Reidratar(ProdutoDaObjetivaId(4, variante), AtoAnulacao, PapelDaPublicacaoPrevista.Preliminar),
            ProdutoDaEtapa.Reidratar(ProdutoDaObjetivaId(5, variante), AtoAnalise, PapelDaPublicacaoPrevista.Definitivo),
            ProdutoDaEtapa.Reidratar(ProdutoDaObjetivaId(6, variante), AtoAnulacao, PapelDaPublicacaoPrevista.Definitivo),
        ], permutar);

    // A etapa requer banca: é o bloco em que o envelope congela a identidade da banca, a posição
    // dela no tipo, a cópia do tipo.
    // Três bancas de tipos diferentes, declaradas fora da ordem que o envelope emite: com uma só,
    // a ordenação pelo código do tipo não se manifestava.
    internal static IReadOnlyList<BancaDaEtapa> BancasDaObjetiva(int variante, bool permutar) =>
        Ordem([
            BancaDaEtapa.ReconstruirDosDadosSalvos(ChaveLocal.Criar("banca_objetiva_2").Value, BancaDaObjetivaId(2, variante), TiposDeBancaCopiadosDeTeste.DeRecursos(TipoBancaRecursal), BancasDeTeste.Regra()),
            BancaDaEtapa.ReconstruirDosDadosSalvos(ChaveLocal.Criar("banca_objetiva_3").Value, BancaDaObjetivaId(3, variante), TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.Heteroidentificacao, TipoBancaAnulacao), BancasDeTeste.Regra()),
            BancaDaEtapa.ReconstruirDosDadosSalvos(ChaveLocal.Criar("banca_objetiva_1").Value, BancaDaObjetivaId(1, variante), TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, TipoBancaExaminadora), BancasDeTeste.Regra()),
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
            EventoInicialDoPrazo.PublicacaoDoAto,
            Regra(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), '9'),
            ParametrosDoRecursoDeTeste.Declarados(
                3m, UnidadePrazo.DiasUteis, admiteInstanciaSuperior: true, segundaInstancia: new PrazoDoRecurso(1m, UnidadePrazo.DiasUteis)),
            ProdutoDaObjetivaId(1, variante),
            ProcedimentosDoExemploSemAlvo()).Value!;

    // Segunda janela ancorada em ato, num produto DIFERENTE do primeiro — é o par que a
    // ordenação das janelas recursais precisa desempatar. Ela ordena pela chave do
    // produto âncora, não pelo id dele: com uma âncora só, trocar um critério pelo outro
    // não mudava nada, e era possível reverter a política sem quebrar fixture nenhuma.
    internal static RecursoDaEtapa RecursoDaObjetivaAncoradoNoSegundoProduto(int variante) =>
        RecursoDaEtapa.Reidratar(
            RecursoDaObjetivaId(2, variante),
            EventoInicialDoPrazo.PublicacaoDoAto,
            Regra(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), '9'),
            ParametrosDoRecursoDeTeste.Declarados(5.0000m, UnidadePrazo.DiasUteis),
            ProdutoDaObjetivaId(3, variante),
            ProcedimentosDoExemploComAlvos()).Value!;

    private static ProcedimentosDeRecurso ProcedimentosDoExemploSemAlvo() => ProcedimentosDeRecurso.De(
    [
        ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.Classificacao, null, Regra(RegraDeProcedimentoDoRecurso.Recalculo.Codigo(), '7')),
    ]);

    // Fora da ordem canônica de propósito: a coleção a normaliza, e o hash não pode depender da ordem de entrada.
    private static ProcedimentosDeRecurso ProcedimentosDoExemploComAlvos() => ProcedimentosDeRecurso.De(
    [
        ProcedimentoDeRecursoDeclarado.De(
            TipoMotivoDeDecisao.Classificacao, AlvoDoProcedimentoDeRecurso.DaArea(AreaDoEnem.Matematica), Regra(RegraDeProcedimentoDoRecurso.Recalculo.Codigo(), '7')),
        ProcedimentoDeRecursoDeclarado.De(
            TipoMotivoDeDecisao.AnaliseDocumental,
            AlvoDoProcedimentoDeRecurso.DoItem(Guid.Parse("00000000-0000-7000-8000-0000000000a1")),
            Regra(RegraDeProcedimentoDoRecurso.Conferencia.Codigo(), '8')),
        ProcedimentoDeRecursoDeclarado.De(
            TipoMotivoDeDecisao.Classificacao, AlvoDoProcedimentoDeRecurso.DaArea(AreaDoEnem.Linguagens), Regra(RegraDeProcedimentoDoRecurso.Recalculo.Codigo(), '7')),
    ]);

    internal static RecursoDaEtapa RecursoDaObjetivaPorCiencia(int variante) =>
        RecursoDaEtapa.Reidratar(
            RecursoDaObjetivaId(3, variante),
            EventoInicialDoPrazo.CienciaIndividual,
            Regra(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), '9'),
            ParametrosDoRecursoDeTeste.Declarados(48.0000m, UnidadePrazo.Horas),
            Guid.Empty,
            ProcedimentosDoExemploSemAlvo()).Value!;

    internal static EtapaProcesso EtapaRedacao(int variante) =>
        EtapaProcesso.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa_redacao").Value!,
            EtapaId(2, variante),
            new IdentificacaoDaEtapa(Nome: "Redação", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaRedacao(), Ordem: 2, FaseCodigo: "RESULTADO_PRELIMINAR"),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 2.2500m, NotaMinima: null, NotaMaxima: 100.0000m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null),
            itens: null);

    // A etapa de avaliação humana exige ao menos uma banca para publicar.
    internal static EtapaProcesso EtapaEntrevista(int variante, bool permutar)
    {
        EtapaProcesso etapa = EtapaProcesso.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa_entrevista").Value!,
            EtapaId(3, variante),
            new IdentificacaoDaEtapa(Nome: "Entrevista", Carater: CaraterEtapa.Eliminatoria, TipoEtapa: TipoEtapaEntrevista(), Ordem: 3, FaseCodigo: "RESULTADO_FINAL"),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: null, NotaMinima: 60.0000m, NotaMaxima: 100.0000m, PassoDoLancamento: 0.5000m, FormaDeAvaliacao: FormaDeAvaliacao.Humana, RegraConsolidacao: Regra(RegraDeApuracaoDaAvaliacao.AvaliacaoUnica.Codigo(), 'f'), EscalaDeAtendimento: null),
            itens: [
                ItemDeAvaliacao.Reidratar(ChaveLocal.Criar("item_primeiro_acesso").Value, ItemDeAvaliacaoId(5, variante), "Primeiro acesso ao ensino superior", 20.5000m),
                ItemDeAvaliacao.Reidratar(ChaveLocal.Criar("item_dominio").Value, ItemDeAvaliacaoId(6, variante), "Domínio do conteúdo", 79.5000m),
            ]);
        etapa.DefinirBancas([BancaDaEtapa.ReconstruirDosDadosSalvos(ChaveLocal.Criar("banca_etapa_3").Value, BancaDaEtapaId(3, variante), TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, TipoBancaExaminadora), BancasDeTeste.Regra())]);
        etapa.DefinirTiposDeDecisao([TipoMotivoDeDecisao.NotaDaEtapa.Codigo(), TipoMotivoDeDecisao.Heteroidentificacao.Codigo()]).IsSuccess.Should().BeTrue();
        return etapa;
    }

    // A banca escolhe o nível de cada critério, sem digitar a nota: é o caso que congela a
    // escala de atendimento e dispensa o passo do lançamento.
    internal static EtapaProcesso EtapaDefesa(int variante, bool permutar)
    {
        EtapaProcesso etapa = EtapaProcesso.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa_defesa").Value!,
            EtapaId(4, variante),
            new IdentificacaoDaEtapa(Nome: "Defesa oral", Carater: CaraterEtapa.Eliminatoria, TipoEtapa: TipoEtapaEntrevista(), Ordem: 4, FaseCodigo: "RESULTADO_FINAL"),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: null, NotaMinima: 50.0000m, NotaMaxima: 100.0000m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Humana, RegraConsolidacao: Regra(RegraDeApuracaoDaAvaliacao.CorrecaoIndependente.Codigo(), 'e'), EscalaDeAtendimento: EscalaDeAtendimentoDaDefesa()),
            itens: [
                ItemDeAvaliacao.Reidratar(ChaveLocal.Criar("item_coesao").Value, ItemDeAvaliacaoId(4, variante), "Coesão da exposição", 30.0000m),
                ItemDeAvaliacao.Reidratar(ChaveLocal.Criar("item_argumentacao").Value, ItemDeAvaliacaoId(3, variante), "Argumentação", 70.0000m),
            ]);
        etapa.DefinirBancas([BancaDaEtapa.ReconstruirDosDadosSalvos(ChaveLocal.Criar("banca_etapa_4").Value, BancaDaEtapaId(4, variante), TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, TipoBancaExaminadora), BancasDeTeste.Regra())]);
        return etapa;
    }

    internal static IReadOnlyList<NivelDeAtendimento> EscalaDeAtendimentoDaDefesa() =>
        [
            NivelDeAtendimento.Criar(ChaveLocal.Criar("nivel_atendeu_parcialmente").Value!, "Atendeu parcialmente", 0.5000m),
            NivelDeAtendimento.Criar(ChaveLocal.Criar("nivel_atendeu").Value!, "Atendeu", 1.0000m),
            NivelDeAtendimento.Criar(ChaveLocal.Criar("nivel_nao_atendeu").Value!, "Não atendeu", 0.0000m),
        ];
}
