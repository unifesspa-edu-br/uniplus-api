namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ProjecaoDaEtapaDoProcessoTests
{
    private static readonly Guid EtapaId = new("01a0d640-0000-7000-8000-0000000000f1");
    private static readonly Guid TipoEtapaId = new("01a0d640-0000-7000-8000-0000000000f9");
    private static readonly Guid ItemDeRedacaoId = new("01a0d640-0000-7000-8000-0000000000a1");
    private static readonly Guid ItemDeOralidadeId = new("01a0d640-0000-7000-8000-0000000000a2");
    private static readonly Guid ProdutoDoResultadoId = new("01a0d640-0000-7000-8000-0000000000f4");
    private static readonly Guid TipoBancaId = new("01a0d640-0000-7000-8000-0000000000f5");
    private static readonly DateTimeOffset Inicio = new(2027, 1, 20, 11, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Fim = new(2027, 1, 20, 15, 0, 0, TimeSpan.Zero);
    private static readonly string HashDaRegra = new('b', 64);

    private static IdentificacaoDaEtapa Identificacao(string? faseCodigo) => new(
        Nome: "Prova discursiva",
        Carater: CaraterEtapa.Eliminatoria,
        TipoEtapa: TipoEtapaSnapshot.Criar(TipoEtapaId, "PROVA_DISCURSIVA", "Prova discursiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: true).Value!,
        Ordem: 4,
        FaseCodigo: faseCodigo);

    private static DeclaracaoDaAvaliacaoDaEtapa AvaliacaoHumana() => new(
        Peso: 2m,
        NotaMinima: 3m,
        NotaMaxima: 100m,
        PassoDoLancamento: 0.5m,
        FormaDeAvaliacao: FormaDeAvaliacao.Humana,
        RegraConsolidacao: ReferenciaRegra.De(RegraConsolidacaoCodigo.CorrecaoIndependente, "v1", HashDaRegra),
        EscalaDeAtendimento: [NivelDeAtendimento.Criar(ChaveLocal.Criar("nivel_atende").Value, "Atende", 1m), NivelDeAtendimento.Criar(ChaveLocal.Criar("nivel_parcial").Value, "Atende em parte", 0.5m)]);

    private static ItemDeAvaliacao[] Itens() =>
    [
        ItemDeAvaliacao.Reidratar(ChaveLocal.Criar("item_redacao").Value, ItemDeRedacaoId, "Redação", 60m),
        ItemDeAvaliacao.Reidratar(ChaveLocal.Criar("item_oralidade").Value, ItemDeOralidadeId, "Oralidade", 40m),
    ];

    private static EtapaProcesso EtapaCompleta()
    {
        EtapaProcesso etapa = EtapaProcesso.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!, EtapaId, Identificacao("AVALIACAO"), AvaliacaoHumana(), Itens());
        etapa.DefinirJanelaEParecer(Inicio, Fim, emiteParecerIndividual: true).IsSuccess.Should().BeTrue();
        etapa.DefinirTiposDeDecisao([TipoMotivoDeDecisao.IsencaoDaTaxa.Codigo(), TipoMotivoDeDecisao.Classificacao.Codigo()]).IsSuccess.Should().BeTrue();
        etapa.DefinirProdutos([ProdutoDaEtapa.Reidratar(ProdutoDoResultadoId, "RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar)]).IsSuccess.Should().BeTrue();
        etapa.DefinirBancas([BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.Nomeada(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, TipoBancaId), "Banca geral"))]);
        etapa.DefinirRecursos([RecursoDaEtapa.Criar(AncoraDoRecurso.AtoPublicado, RegraDoPrazo(), ParametrosDoPrazo(), ProdutoDoResultadoId).Value!])
            .IsSuccess.Should().BeTrue();
        return etapa;
    }

    private static EtapaProcesso EtapaSemJanelaNemEscala()
    {
        DeclaracaoDaAvaliacaoDaEtapa semAvaliacao = new(
            Peso: null,
            NotaMinima: null,
            NotaMaxima: null,
            PassoDoLancamento: null,
            FormaDeAvaliacao: null,
            RegraConsolidacao: null,
            EscalaDeAtendimento: null);

        return EtapaProcesso.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!, EtapaId, Identificacao(faseCodigo: null), semAvaliacao, itens: null);
    }

    private static ReferenciaRegra RegraDoPrazo() => ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "1", HashDaRegra);

    private static ParametrosDeclaradosDoRecurso ParametrosDoPrazo() => ParametrosDoRecursoDeTeste.ComInstanciaSuperior(2m, UnidadePrazo.DiasUteis);

    private static BancaDaEtapaDto BancaGeralEsperada(Guid id) => new(
        id,
        "banca",
        TipoBancaId,
        CodigoComRotulo<TipoDeBanca>.De(TipoDeBanca.CorrecaoDeRedacoes),
        1,
        "Banca geral",
        CodigoComRotulo<PapelDaBanca>.De(PapelDaBanca.AvaliaOMerito),
        false,
        false,
        [CodigoComRotulo<TipoMotivoDeDecisao>.De(TipoMotivoDeDecisao.NotaDaEtapa)],
        null);

    [Fact(DisplayName = "A etapa projeta o id e as bancas no topo")]
    public void ProjetarEtapa_IdEBancas_FicamNoTopo()
    {
        EtapaProcesso etapa = EtapaCompleta();

        EtapaProcessoDto dto = ProjecaoDaEtapaDoProcesso.ProjetarEtapa(etapa);

        dto.Id.Should().Be(EtapaId);
        dto.Bancas.Should().BeEquivalentTo([BancaGeralEsperada(etapa.Bancas.Single().Id)]);
    }

    [Fact(DisplayName = "A identificação projeta o nome, o caráter, o tipo, a ordem e o código da fase")]
    public void ProjetarEtapa_Identificacao_ProjetaCadaCampo()
    {
        IdentificacaoDaEtapaDto grupo = ProjecaoDaEtapaDoProcesso.ProjetarEtapa(EtapaCompleta()).Identificacao;

        grupo.Should().BeEquivalentTo(new IdentificacaoDaEtapaDto(
            Nome: "Prova discursiva",
            Carater: CaraterEtapa.Eliminatoria,
            TipoEtapa: new TipoEtapaSnapshotDto(TipoEtapaId, "PROVA_DISCURSIVA", "Prova discursiva", NotaDeOrigemNoEnem: true, ExigeCorrecaoIndependente: false),
            Ordem: 4,
            FaseCodigo: "AVALIACAO"));
    }

    [Fact(DisplayName = "A nota projeta o peso, a nota mínima, a nota máxima e o passo, cada um no seu campo")]
    public void ProjetarEtapa_Nota_ProjetaCadaDecimalNoSeuCampo()
    {
        NotaDaEtapaDto grupo = ProjecaoDaEtapaDoProcesso.ProjetarEtapa(EtapaCompleta()).Nota;

        grupo.Should().BeEquivalentTo(new NotaDaEtapaDto(Peso: 2m, NotaMinima: 3m, NotaMaxima: 100m, PassoDoLancamento: 0.5m));
    }

    [Fact(DisplayName = "A avaliação projeta a forma, a regra de consolidação, os itens e a escala de atendimento")]
    public void ProjetarEtapa_Avaliacao_ProjetaCadaCampo()
    {
        AvaliacaoDaEtapaDto grupo = ProjecaoDaEtapaDoProcesso.ProjetarEtapa(EtapaCompleta()).Avaliacao;

        grupo.Should().BeEquivalentTo(new AvaliacaoDaEtapaDto(
            FormaDeAvaliacao: "HUMANA",
            RegraConsolidacao: new ReferenciaRegraDto(RegraConsolidacaoCodigo.CorrecaoIndependente, "v1", HashDaRegra),
            ItensDeAvaliacao:
            [
                new ItemDeAvaliacaoDto(ItemDeRedacaoId, "Redação", 60m, 1, "item_redacao"),
                new ItemDeAvaliacaoDto(ItemDeOralidadeId, "Oralidade", 40m, 2, "item_oralidade"),
            ],
            EscalaDeAtendimento: [new NivelDeAtendimentoDto("Atende", 1m, 1, "nivel_atende"), new NivelDeAtendimentoDto("Atende em parte", 0.5m, 2, "nivel_parcial")]));
    }

    [Fact(DisplayName = "A janela projeta o início e o fim, cada um no seu campo")]
    public void ProjetarEtapa_Janela_ProjetaInicioEFim()
    {
        JanelaDaEtapaDto grupo = ProjecaoDaEtapaDoProcesso.ProjetarEtapa(EtapaCompleta()).Janela;

        grupo.Should().BeEquivalentTo(new JanelaDaEtapaDto(Inicio: Inicio, Fim: Fim));
    }

    [Fact(DisplayName = "As publicações projetam os produtos, o parecer, os recursos e os tipos de decisão")]
    public void ProjetarEtapa_Publicacoes_ProjetaCadaCampo()
    {
        EtapaProcesso etapa = EtapaCompleta();

        PublicacoesDaEtapaDto grupo = ProjecaoDaEtapaDoProcesso.ProjetarEtapa(etapa).Publicacoes;

        grupo.Should().BeEquivalentTo(PublicacoesEsperadas(etapa.Recursos.Single()));
    }

    [Fact(DisplayName = "Os tipos de decisão da etapa saem com código e rótulo, em ordem alfabética do código")]
    public void ProjetarEtapa_TiposDeDecisao_SaemComCodigoERotulo()
    {
        IReadOnlyList<CodigoComRotulo<TipoMotivoDeDecisao>> tipos = ProjecaoDaEtapaDoProcesso.ProjetarEtapa(EtapaCompleta()).Publicacoes.TiposDeDecisao;

        tipos.Select(static t => t.Codigo).Should().Equal(
            TipoMotivoDeDecisao.Classificacao.Codigo(), TipoMotivoDeDecisao.IsencaoDaTaxa.Codigo());
        tipos.Select(static t => t.Rotulo).Should().Equal(
            TipoMotivoDeDecisao.Classificacao.Rotulo(), TipoMotivoDeDecisao.IsencaoDaTaxa.Rotulo());
    }

    [Fact(DisplayName = "A etapa sem janela, sem avaliação e sem escala mantém os grupos e anula só o que é ausente")]
    public void ProjetarEtapa_SemJanelaNemEscala_GruposNuncaNulos()
    {
        EtapaProcessoDto dto = ProjecaoDaEtapaDoProcesso.ProjetarEtapa(EtapaSemJanelaNemEscala());

        dto.Janela.Should().BeEquivalentTo(new JanelaDaEtapaDto(Inicio: null, Fim: null));
        dto.Nota.Should().BeEquivalentTo(new NotaDaEtapaDto(null, null, null, null));
        dto.Avaliacao.EscalaDeAtendimento.Should().BeNull("escala ausente é diferente de escala vazia");
        dto.Avaliacao.FormaDeAvaliacao.Should().BeNull();
        dto.Avaliacao.RegraConsolidacao.Should().BeNull();
        dto.Identificacao.FaseCodigo.Should().BeNull();
        dto.Publicacoes.Produtos.Should().BeEmpty();
    }

    private static PublicacoesDaEtapaDto PublicacoesEsperadas(RecursoDaEtapa recurso) => new(
        Produtos: [new ProdutoDaEtapaDto(ProdutoDoResultadoId, "RESULTADO_PRELIMINAR", "PRELIMINAR")],
        EmiteParecerIndividual: true,
        Recursos:
        [
            new RecursoDaEtapaDto(
                recurso.Id,
                AncoraDoRecurso.AtoPublicado,
                new ReferenciaRegraDto(RegraPrazoRecursoCodigo.AncoradoEmAto, "1", HashDaRegra),
                ParametrosEsperados(),
                ProdutoDoResultadoId,
                "RESULTADO_PRELIMINAR",
                []),
        ],
        TiposDeDecisao: [CodigoComRotulo<TipoMotivoDeDecisao>.De(TipoMotivoDeDecisao.Classificacao), CodigoComRotulo<TipoMotivoDeDecisao>.De(TipoMotivoDeDecisao.IsencaoDaTaxa)]);

    private static ParametrosRegraPrazoRecursoDto ParametrosEsperados() => new(
        2m,
        CodigoComRotulo<UnidadePrazo>.De(UnidadePrazo.DiasUteis),
        true,
        3m,
        CodigoComRotulo<UnidadePrazo>.De(UnidadePrazo.DiasUteis),
        CodigoComRotulo<EfeitoSuspensivo>.De(EfeitoSuspensivo.SemEfeitoSuspensivo),
        CodigoComRotulo<GuardaDaVaga>.De(GuardaDaVaga.AteOResultadoDefinitivo));
}
