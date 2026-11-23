namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static CenarioDeProcesso;

/// <summary>As bancas que o processo declara: quem julga o recurso, a regra de deliberação de cada banca e o mínimo de integrantes.</summary>
public sealed class ConferenciaDasBancasTests
{
    private static readonly ReferenciaRegra AvaliacaoUnica =
        ReferenciaRegra.De(RegraConsolidacaoCodigo.AvaliacaoUnica, "v1", new string('a', 64));

    private static readonly CorretoresDasEtapas TresCorretores = new(new Dictionary<ReferenciaRegra, CorretoresDaRegra> { [AvaliacaoUnica] = CorretoresDaRegra.SemNovoCorretor(3) });

    private static readonly CorretoresDasEtapas TresMaisUmAdicional =
        new(new Dictionary<ReferenciaRegra, CorretoresDaRegra> { [AvaliacaoUnica] = new CorretoresDaRegra(Distintos: 3, EmCampo: 4) });

    private static readonly CorretoresDasEtapas NenhumLido = new(new Dictionary<ReferenciaRegra, CorretoresDaRegra>());

    private static ModeloDeFase FaseComRecurso(string codigo, int ordem, params BancaRequerida[] bancas)
    {
        ProdutoDaFase preliminar = ModeloDeFase.Preliminar("RESULTADO");

        return new ModeloDeFase(codigo, ordem) { Produtos = [preliminar], RegraRecurso = ModeloDeFase.RecursoAncoradoEm(preliminar.Id), Bancas = bancas };
    }

    // A fase que homologa as inscrições, com a banca que julga a homologação, para que os itens novos do painel fiquem julgáveis.
    private static ModeloDeFase QueHomologa(ModeloDeFase fase) => fase.Homologando(RegraDeHomologacaoDasInscricoes.Declarar("RESULTADO", [])) with
    {
        Bancas = [.. fase.Bancas, BancasDeTeste.DaHomologacaoDasInscricoesDaFase()],
    };

    private static BancaRequerida DeRecursos(int integrantes, int quorum) =>
        BancaRequerida.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.DeRecursos(Guid.CreateVersion7()), [], BancasDeTeste.Regra(integrantes, quorum));

    private static BancaRequerida SemRegraNaFase() =>
        BancaRequerida.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, Guid.CreateVersion7()), []);

    private static ModeloDeEtapa ComRecurso(string codigoDaFase, params BancaDaEtapa[] bancas) =>
        new ModeloDeEtapa("Redação") with
        {
            FaseCodigo = codigoDaFase,
            Forma = FormaDeAvaliacao.Humana,
            RegraConsolidacao = AvaliacaoUnica,
            Passo = 0.5m,
            EmiteParecerIndividual = true,
            Recursos = [RecursoModelo.NaCiencia()],
            Bancas = bancas,
        };

    private static BancaDaEtapa DeRecursosDaEtapa(int integrantes, int quorum) =>
        BancasDeTeste.DeRecursosDaEtapa(BancasDeTeste.Regra(integrantes, quorum));

    [Fact(DisplayName = "A fase que admite recurso sem banca de recursos é recusada")]
    public void RecursosSemBanca_FaseSemBanca_Recusa()
    {
        CronogramaDoProcesso cronograma = Fases(FaseComRecurso("RESULTADO", 1));

        Causas(QuemJulgaORecurso.SemBanca(cronograma, Etapas()))
            .Should().Equal(("fases[0].bancasRequeridas", "Cronograma.RecursoSemBanca"));
    }

    [Fact(DisplayName = "A fase que admite recurso com a banca de recursos, ou sem recurso, não é recusada")]
    public void RecursosSemBanca_FaseComBancaOuSemRecurso_SemCausa()
    {
        QuemJulgaORecurso.SemBanca(Fases(FaseComRecurso("RESULTADO", 1, BancasDeTeste.DeRecursosDaFase())), Etapas()).Should().BeEmpty();
        QuemJulgaORecurso.SemBanca(Fases(new ModeloDeFase("INSCRICAO", 1)), Etapas()).Should().BeEmpty();
    }

    [Fact(DisplayName = "A etapa que abre recurso sem banca própria e sem banca de recursos na fase é recusada")]
    public void RecursosSemBanca_EtapaSemQuemJulgue_Recusa()
    {
        CronogramaDoProcesso cronograma = Fases(new ModeloDeFase("AVALIACAO", 1));
        EtapasDoProcesso etapas = Etapas(ComRecurso("AVALIACAO"));

        Causas(QuemJulgaORecurso.SemBanca(cronograma, etapas)).Should().Equal(("etapas[0].bancas", "Cronograma.RecursoSemBanca"));
    }

    [Fact(DisplayName = "A etapa que abre recurso é julgada pela banca própria ou pela banca de recursos da fase")]
    public void RecursosSemBanca_EtapaComBancaPropriaOuDaFase_SemCausa()
    {
        CronogramaDoProcesso semBanca = Fases(new ModeloDeFase("AVALIACAO", 1));
        CronogramaDoProcesso comBancaNaFase = Fases(new ModeloDeFase("AVALIACAO", 1) with { Bancas = [BancasDeTeste.DeRecursosDaFase()] });

        QuemJulgaORecurso.SemBanca(semBanca, Etapas(ComRecurso("AVALIACAO", BancasDeTeste.DeRecursosDaEtapa()))).Should().BeEmpty();
        QuemJulgaORecurso.SemBanca(comBancaNaFase, Etapas(ComRecurso("AVALIACAO"))).Should().BeEmpty();
        QuemJulgaORecurso.SemBanca(semBanca, Etapas(EtapaNaFase("AVALIACAO"))).Should().BeEmpty();
    }

    [Fact(DisplayName = "As fases são recusadas antes das etapas")]
    public void RecursosSemBanca_FasesAntesDasEtapas()
    {
        CronogramaDoProcesso cronograma = Fases(FaseComRecurso("AVALIACAO", 1));
        EtapasDoProcesso etapas = Etapas(ComRecurso("AVALIACAO"));

        Causas(QuemJulgaORecurso.SemBanca(cronograma, etapas)).Select(static causa => causa.Campo)
            .Should().Equal("fases[0].bancasRequeridas", "etapas[0].bancas");
    }

    [Fact(DisplayName = "A banca sem regra de deliberação é recusada na posição dela, as das fases antes das das etapas")]
    public void RegrasDeDeliberacao_BancaSemRegra_Recusa()
    {
        CronogramaDoProcesso cronograma = Fases(new ModeloDeFase("AVALIACAO", 1) with { Bancas = [BancasDeTeste.DeRecursosDaFase(), SemRegraNaFase()] });
        EtapasDoProcesso etapas = Etapas(EtapaNaFase("AVALIACAO") with { Bancas = [BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, Guid.CreateVersion7()))] });

        Causas(ConferenciaDasBancas.ConferirRegrasDeDeliberacao(cronograma, etapas)).Should().Equal(
            ("fases[0].bancasRequeridas[1]", "Banca.RegraDeDeliberacaoObrigatoria"),
            ("etapas[0].bancas[0]", "Banca.RegraDeDeliberacaoObrigatoria"));
        ConferenciaDasBancas.ConferirRegrasDeDeliberacao(cronograma, etapas)[0].Error.Message
            .Should().Be("A banca 'Banca de correção de redações 1' da fase 'AVALIACAO' não declara a regra de deliberação.");
    }

    [Fact(DisplayName = "A banca de recursos da fase com menos integrantes do que os corretores da etapa que julga é recusada")]
    public void Minimo_BancaDaFaseAbaixoDosCorretores_Recusa()
    {
        CronogramaDoProcesso cronograma = Fases(new ModeloDeFase("AVALIACAO", 1) with { Bancas = [DeRecursos(integrantes: 2, quorum: 2)] });
        EtapasDoProcesso etapas = Etapas(ComRecurso("AVALIACAO"));

        Causas(ConferenciaDasBancas.ConferirMinimoDeIntegrantes(cronograma, etapas, TresCorretores))
            .Should().Equal(("fases[0].bancasRequeridas[0]", "Banca.IntegrantesAbaixoDoMinimo"));
    }

    [Fact(DisplayName = "A regra que declara o novo corretor eleva o mínimo da banca ao número de corretores em campo")]
    public void Minimo_RegraComCorretorAdicional_ExigeOsCorretoresEmCampo()
    {
        CronogramaDoProcesso comTres = Fases(new ModeloDeFase("AVALIACAO", 1) with { Bancas = [DeRecursos(integrantes: 3, quorum: 2)] });
        CronogramaDoProcesso comQuatro = Fases(new ModeloDeFase("AVALIACAO", 1) with { Bancas = [DeRecursos(integrantes: 4, quorum: 2)] });
        EtapasDoProcesso etapas = Etapas(ComRecurso("AVALIACAO"));

        Causas(ConferenciaDasBancas.ConferirMinimoDeIntegrantes(comTres, etapas, TresMaisUmAdicional))
            .Should().Equal(("fases[0].bancasRequeridas[0]", "Banca.IntegrantesAbaixoDoMinimo"));
        ConferenciaDasBancas.ConferirMinimoDeIntegrantes(comQuatro, etapas, TresMaisUmAdicional).Should().BeEmpty();
    }

    [Fact(DisplayName = "A banca de recursos da própria etapa com menos integrantes do que os corretores dela é recusada")]
    public void Minimo_BancaDaEtapaAbaixoDosCorretores_Recusa()
    {
        CronogramaDoProcesso cronograma = Fases(new ModeloDeFase("AVALIACAO", 1));
        EtapasDoProcesso etapas = Etapas(ComRecurso("AVALIACAO", DeRecursosDaEtapa(integrantes: 2, quorum: 2)));

        Causas(ConferenciaDasBancas.ConferirMinimoDeIntegrantes(cronograma, etapas, TresCorretores))
            .Should().Equal(("etapas[0].bancas[0]", "Banca.IntegrantesAbaixoDoMinimo"));
    }

    [Fact(DisplayName = "O mínimo é o quórum quando a banca não julga etapa, e a banca com integrantes suficientes não é recusada")]
    public void Minimo_SemEtapaJulgadaOuComIntegrantesSuficientes_SemCausa()
    {
        CronogramaDoProcesso semJulgadas = Fases(new ModeloDeFase("AVALIACAO", 1) with { Bancas = [DeRecursos(integrantes: 2, quorum: 2)] });
        CronogramaDoProcesso suficiente = Fases(new ModeloDeFase("AVALIACAO", 1) with { Bancas = [DeRecursos(integrantes: 3, quorum: 2)] });
        EtapasDoProcesso comRecurso = Etapas(ComRecurso("AVALIACAO"));

        ConferenciaDasBancas.ConferirMinimoDeIntegrantes(semJulgadas, Etapas(), NenhumLido).Should().BeEmpty();
        ConferenciaDasBancas.ConferirMinimoDeIntegrantes(suficiente, comRecurso, TresCorretores).Should().BeEmpty();
    }

    [Fact(DisplayName = "A banca que não é de recursos não julga etapa: o mínimo dela é o quórum")]
    public void Minimo_BancaQueNaoEDeRecursos_NaoJulgaEtapa()
    {
        BancaRequerida avaliadora = BancaRequerida.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, Guid.CreateVersion7()), [], BancasDeTeste.Regra(integrantes: 2, quorum: 2));
        CronogramaDoProcesso cronograma = Fases(new ModeloDeFase("AVALIACAO", 1) with { Bancas = [avaliadora] });

        ConferenciaDasBancas.ConferirMinimoDeIntegrantes(cronograma, Etapas(ComRecurso("AVALIACAO")), TresCorretores).Should().BeEmpty();
    }

    [Fact(DisplayName = "A etapa julgada cuja regra de consolidação não foi lida é recusada, depois das bancas abaixo do mínimo")]
    public void Minimo_RegraNaoResolvida_DepoisDasBancasAbaixoDoMinimo()
    {
        CronogramaDoProcesso cronograma = Fases(new ModeloDeFase("AVALIACAO", 1) with { Bancas = [DeRecursos(integrantes: 2, quorum: 2)] });
        EtapasDoProcesso etapas = Etapas(ComRecurso("AVALIACAO", DeRecursosDaEtapa(integrantes: 2, quorum: 2)), ComRecurso("AVALIACAO") with { Id = Guid.CreateVersion7(), Nome = "Entrevista", RegraConsolidacao = ReferenciaRegra.De(RegraConsolidacaoCodigo.CorrecaoIndependente, "v1", new string('b', 64)) });

        Causas(ConferenciaDasBancas.ConferirMinimoDeIntegrantes(cronograma, etapas, TresCorretores)).Should().Equal(
            ("etapas[0].bancas[0]", "Banca.IntegrantesAbaixoDoMinimo"),
            ("fases[0].bancasRequeridas[0]", "EtapaProcesso.RegraDeConsolidacaoNaoResolvida"));
    }

    [Fact(DisplayName = "A banca sem regra de deliberação não entra no mínimo")]
    public void Minimo_BancaSemRegra_NaoEntra()
    {
        CronogramaDoProcesso cronograma = Fases(new ModeloDeFase("AVALIACAO", 1) with { Bancas = [SemRegraNaFase()] });

        ConferenciaDasBancas.ConferirMinimoDeIntegrantes(cronograma, Etapas(ComRecurso("AVALIACAO")), NenhumLido).Should().BeEmpty();
    }

    private static readonly string[] OsDozeItens =
    [
        "cronograma_banca_de_recursos_sem_candidatos_atendidos",
        "cronograma_recurso_com_banca_que_julga",
        "cronograma_instancia_superior_admitida_pelo_tipo_de_banca",
        "cronograma_instancia_superior_com_banca_da_segunda_instancia",
        "cronograma_banca_da_segunda_instancia_com_recurso_para_julgar",
        "cronograma_uma_banca_da_segunda_instancia_por_fase_ou_etapa",
        "cronograma_tipo_de_banca_com_o_minimo_de_bancas",
        "cronograma_homologacao_das_inscricoes_com_banca",
        "cronograma_nota_do_enem_com_banca_de_importacao_das_notas",
        "cronograma_importacao_externa_com_banca_de_importacao_de_candidatos",
        "cronograma_banca_com_regra_de_deliberacao",
        "cronograma_banca_com_o_minimo_de_integrantes",
    ];

    [Fact(DisplayName = "Os doze itens saem nesta ordem, verdes quando a configuração das bancas é conforme")]
    public void Itens_Conformes_DozeVerdesNaOrdem()
    {
        CronogramaDoProcesso cronograma = Fases(QueHomologa(new ModeloDeFase("AVALIACAO", 1) with { Bancas = [BancasDeTeste.DeRecursosDaFase()] }));

        IReadOnlyList<ItemConformidade> itens = ConferenciaDasBancas.Itens(OrigemCandidatos.InscricaoPropria, cronograma, Etapas(), NenhumLido);

        itens.Select(static item => item.Codigo).Should().Equal(OsDozeItens);
        itens.Should().OnlyContain(static item => item.Ok);
    }

    [Fact(DisplayName = "A banca de recursos com candidatos atendidos deixa vermelho só o item dos candidatos atendidos")]
    public void Itens_CandidatosAtendidosEmBancaDeRecursos_SoOItemDosCandidatosAtendidosVermelho()
    {
        BancaRequerida comCandidatosAtendidos = BancaRequerida.Criar(ChaveLocal.Criar("banca").Value,
            TiposDeBancaCopiadosDeTeste.DeRecursos(Guid.CreateVersion7()), [CategoriaJulgada.Criar(Guid.CreateVersion7(), "RENDA")], BancasDeTeste.Regra());
        CronogramaDoProcesso cronograma = Fases(QueHomologa(new ModeloDeFase("AVALIACAO", 1) with { Bancas = [comCandidatosAtendidos] }));

        ConferenciaDasBancas.Itens(OrigemCandidatos.InscricaoPropria, cronograma, Etapas(), NenhumLido).Where(static item => !item.Ok).Select(static item => item.Codigo)
            .Should().Equal("cronograma_banca_de_recursos_sem_candidatos_atendidos");
    }

    [Fact(DisplayName = "Cada item fica vermelho com a causa que projeta")]
    public void Itens_CadaCausa_DeixaVermelhoOSeuItem()
    {
        CronogramaDoProcesso cronograma = Fases(QueHomologa(FaseComRecurso("AVALIACAO", 1, DeRecursos(integrantes: 2, quorum: 2), SemRegraNaFase())));
        EtapasDoProcesso etapas = Etapas(ComRecurso("AVALIACAO"));

        ConferenciaDasBancas.Itens(OrigemCandidatos.InscricaoPropria, cronograma, etapas, TresCorretores).Where(static item => !item.Ok).Select(static item => item.Codigo)
            .Should().Equal("cronograma_banca_com_regra_de_deliberacao", "cronograma_banca_com_o_minimo_de_integrantes");
        ConferenciaDasBancas.Itens(OrigemCandidatos.InscricaoPropria, Fases(FaseComRecurso("AVALIACAO", 1)), Etapas(), NenhumLido)[1].Ok.Should().BeFalse();
    }
}
