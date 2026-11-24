namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.CenarioDeProcesso;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;

/// <summary>
/// O tipo de banca que exige duas ou mais bancas tem ao menos duas no lugar e, na fase, em cada grupo de candidatos atendidos.
/// A exigência é da cópia que cada banca guarda, e só se cobra onde a leitura aceitou as bancas e os candidatos atendidos delas.
/// </summary>
public sealed class MinimoDeBancasPorTipoNaConferenciaTests
{
    private const string Item = "cronograma_tipo_de_banca_com_o_minimo_de_bancas";
    private const string Codigo = "Banca.TipoExigeMaisDeUmaBanca";
    private const string Renda = "RENDA";
    private const string EtnicoRacial = "ETNICO_RACIAL";

    private static readonly CorretoresDasEtapas NenhumLido = new(new Dictionary<ReferenciaRegra, CorretoresDaRegra>());

    private static BancaRequerida DaFase(bool exige, TipoDeBanca tipo, params string[] categorias) =>
        BancaRequerida.Criar(ChaveLocal.Criar("banca").Value,
            TiposDeBancaCopiadosDeTeste.Com(tipo, PapelDaBanca.AvaliaOMerito, exigeDuasOuMaisBancas: exige),
            [.. categorias.Select(static categoria => CategoriaJulgada.Criar(Guid.CreateVersion7(), categoria, categoria))],
            BancasDeTeste.Regra());

    private static BancaRequerida Heteroidentificacao(params string[] categorias) => DaFase(true, TipoDeBanca.Heteroidentificacao, categorias);

    private static BancaDaEtapa DaEtapa(bool exige, TipoDeBanca tipo) =>
        BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.Com(tipo, PapelDaBanca.AvaliaOMerito, exigeDuasOuMaisBancas: exige), BancasDeTeste.Regra());

    private static ModeloDeFase Fase(params BancaRequerida[] bancas) => new ModeloDeFase("AVALIACAO", 1) with { Bancas = bancas };

    private static ModeloDeEtapa Etapa(params BancaDaEtapa[] bancas) => EtapaNaFase("AVALIACAO") with { Bancas = bancas };

    private static IReadOnlyList<FieldError> Faltas(ModeloDeFase fase) =>
        ConferenciaDasBancas.ConferirMinimoDeBancasPorTipo(Fases(fase), Etapas());

    private static IReadOnlyList<FieldError> Faltas(ModeloDeEtapa etapa) =>
        ConferenciaDasBancas.ConferirMinimoDeBancasPorTipo(Fases(new ModeloDeFase("AVALIACAO", 1)), Etapas(etapa));

    private static bool ItemAprovado(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        ConferenciaDasBancas.Itens(OrigemCandidatos.InscricaoPropria, cronograma, etapas, NenhumLido).Single(static item => item.Codigo == Item).Ok;

    [Fact(DisplayName = "A fase com uma só banca do tipo que exige duas ou mais é recusada, citando a banca pelo rótulo e pela posição")]
    public void Fase_UmaSoBancaDoTipoQueExige_Recusa()
    {
        IReadOnlyList<FieldError> faltas = Faltas(Fase(Heteroidentificacao()));

        FieldError falta = faltas.Should().ContainSingle().Subject;
        falta.Field.Should().Be("fases[0].bancasRequeridas[0]");
        falta.Error.Code.Should().Be(Codigo);
        falta.Error.Message.Should().Be(
            "A fase 'AVALIACAO' prevê só a banca 'Banca de heteroidentificação 1' desse tipo, e este tipo de banca exige duas ou mais bancas. Acrescente outra banca do mesmo tipo.");
    }

    [Fact(DisplayName = "A etapa com uma só banca do tipo que exige duas ou mais é recusada, citando a etapa pela posição")]
    public void Etapa_UmaSoBancaDoTipoQueExige_Recusa()
    {
        IReadOnlyList<FieldError> faltas = Faltas(Etapa(DaEtapa(true, TipoDeBanca.AvaliacaoBiopsicossocial)));

        FieldError falta = faltas.Should().ContainSingle().Subject;
        falta.Field.Should().Be("etapas[0].bancas[0]");
        falta.Error.Message.Should().Be(
            "A etapa na posição 1 prevê só a banca 'Banca de avaliação biopsicossocial 1' desse tipo, e este tipo de banca exige duas ou mais bancas. Acrescente outra banca do mesmo tipo.");
    }

    [Fact(DisplayName = "Duas bancas do tipo, na fase (as duas sem candidatos atendidos, num grupo só) ou na etapa, cumprem o mínimo")]
    public void DuasBancasDoTipo_CumpremOMinimo()
    {
        Faltas(Fase(Heteroidentificacao(), Heteroidentificacao())).Should().BeEmpty();
        Faltas(Etapa(DaEtapa(true, TipoDeBanca.Heteroidentificacao), DaEtapa(true, TipoDeBanca.Heteroidentificacao))).Should().BeEmpty();
    }

    [Fact(DisplayName = "A banca única de um tipo que não exige duas ou mais não é recusada")]
    public void UmaSoBancaDoTipoQueNaoExige_SemCausa()
    {
        Faltas(Fase(DaFase(false, TipoDeBanca.Entrevista))).Should().BeEmpty();
        Faltas(Etapa(DaEtapa(false, TipoDeBanca.Entrevista))).Should().BeEmpty();
    }

    [Fact(DisplayName = "Duas cópias do mesmo tipo, com só a nova exigindo duas ou mais, cumprem o mínimo")]
    public void CopiasDiferentesDoMesmoTipo_ContamTodas()
    {
        Faltas(Fase(DaFase(false, TipoDeBanca.Heteroidentificacao), Heteroidentificacao())).Should().BeEmpty();
    }

    [Fact(DisplayName = "A banca de outro tipo não conta para o mínimo do tipo")]
    public void BancaDeOutroTipo_NaoConta()
    {
        Faltas(Fase(Heteroidentificacao(), DaFase(false, TipoDeBanca.Entrevista)))
            .Should().ContainSingle().Which.Field.Should().Be("fases[0].bancasRequeridas[0]");
    }

    [Fact(DisplayName = "As bancas com os mesmos candidatos atendidos, em qualquer ordem, formam um grupo e cumprem o mínimo")]
    public void BancasComOsMesmosCandidatosAtendidos_FormamUmGrupo()
    {
        Faltas(Fase(Heteroidentificacao(Renda, EtnicoRacial), Heteroidentificacao(EtnicoRacial, Renda))).Should().BeEmpty();
    }

    [Fact(DisplayName = "Cada grupo de candidatos atendidos com uma só banca é recusado, citando a banca do grupo")]
    public void GruposComUmaSoBancaCada_RecusaCadaGrupo()
    {
        IReadOnlyList<FieldError> faltas = Faltas(Fase(Heteroidentificacao(Renda), Heteroidentificacao(EtnicoRacial)));

        Causas(faltas).Should().Equal(("fases[0].bancasRequeridas[0]", Codigo), ("fases[0].bancasRequeridas[1]", Codigo));
        faltas[1].Error.Message.Should().Be(
            "Na fase 'AVALIACAO', a banca 'Banca de heteroidentificação 2' é a única do tipo no seu grupo de candidatos atendidos, e este tipo de banca exige duas ou mais bancas em cada grupo. Acrescente outra banca do mesmo tipo com as mesmas categorias de documento.");
    }

    [Fact(DisplayName = "O grupo com duas bancas cumpre o mínimo, e o grupo que ficou com uma só é recusado")]
    public void UmGrupoCompletoEOutroIncompleto_RecusaSoOIncompleto()
    {
        Causas(Faltas(Fase(Heteroidentificacao(Renda), Heteroidentificacao(Renda), Heteroidentificacao(EtnicoRacial))))
            .Should().Equal(("fases[0].bancasRequeridas[2]", Codigo));
    }

    [Fact(DisplayName = "A falta do lugar vem no lugar da falta do grupo: a banca única é recusada uma vez só")]
    public void BancaUnicaDoLugar_RecusadaSoPeloLugar()
    {
        IReadOnlyList<FieldError> faltas = Faltas(Fase(Heteroidentificacao(Renda)));

        faltas.Should().ContainSingle().Which.Error.Message.Should().StartWith("A fase 'AVALIACAO' prevê só a banca");
    }

    [Fact(DisplayName = "As faltas saem na ordem das fases e das etapas, e dentro do lugar pela primeira posição de cada tipo")]
    public void Ordem_FasesAntesDasEtapas_TiposPelaPrimeiraPosicao()
    {
        CronogramaDoProcesso cronograma = Fases(Fase(Heteroidentificacao(), DaFase(true, TipoDeBanca.AvaliacaoBiopsicossocial)));
        EtapasDoProcesso etapas = Etapas(Etapa(DaEtapa(true, TipoDeBanca.Heteroidentificacao)));

        Causas(ConferenciaDasBancas.ConferirMinimoDeBancasPorTipo(cronograma, etapas)).Should().Equal(
            ("fases[0].bancasRequeridas[0]", Codigo),
            ("fases[0].bancasRequeridas[1]", Codigo),
            ("etapas[0].bancas[0]", Codigo));
    }

    [Fact(DisplayName = "A lista de bancas com parte recusada na leitura deixa o item pendente e sem causa, no lugar que a perdeu")]
    public void ListaDeBancasComParteRecusada_ItemPendenteSemCausa()
    {
        FaseCronograma fase = Fase(Heteroidentificacao()).Adotada();
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([fase], ComPartesRecusadas("fases[0].bancasRequeridas"));
        EtapasDoProcesso etapas = EtapasDoProcesso.Das(
            [Etapa(DaEtapa(true, TipoDeBanca.Heteroidentificacao)).Adotada()], ComPartesRecusadas("etapas[0].bancas"));

        ConferenciaDasBancas.ConferirMinimoDeBancasPorTipo(cronograma, etapas).Should().BeEmpty();
        ItemAprovado(cronograma, etapas).Should().BeFalse();
    }

    [Fact(DisplayName = "Os candidatos atendidos recusados de um tipo deixam o item pendente e sem a falta do grupo, e os outros tipos seguem conferidos")]
    public void CandidatosAtendidosRecusados_ItemPendenteSemFaltaDoGrupoDoTipo()
    {
        CronogramaDoProcesso cronograma = Fases(Fase(
            Heteroidentificacao(Renda),
            Heteroidentificacao(),
            DaFase(true, TipoDeBanca.AvaliacaoBiopsicossocial)));

        Causas(ConferenciaDasBancas.ConferirMinimoDeBancasPorTipo(cronograma, Etapas()))
            .Should().Equal(("fases[0].bancasRequeridas[2]", Codigo));
        ItemAprovado(cronograma, Etapas()).Should().BeFalse();
    }

    [Fact(DisplayName = "A categoria repetida na única banca do tipo deixa só a falta do lugar, sem a do grupo, e o item não é aprovado")]
    public void CategoriaRepetidaNaUnicaBancaDoTipo_SoAFaltaDoLugar()
    {
        CronogramaDoProcesso cronograma = Fases(Fase(Heteroidentificacao(Renda, Renda)));

        IReadOnlyList<FieldError> faltas = ConferenciaDasBancas.ConferirMinimoDeBancasPorTipo(cronograma, Etapas());

        Causas(faltas).Should().Equal(("fases[0].bancasRequeridas[0]", Codigo));
        faltas.Single().Error.Message.Should().StartWith("A fase 'AVALIACAO' prevê só a banca");
        ItemAprovado(cronograma, Etapas()).Should().BeFalse();
    }

    [Fact(DisplayName = "Os candidatos atendidos recusados numa fase não mudam a falta do lugar na outra fase")]
    public void CandidatosAtendidosRecusadosNaPrimeiraFase_FaltaDoLugarDaSegundaFase()
    {
        ModeloDeFase segunda = new ModeloDeFase("SEGUNDA_FASE", 2) with { Bancas = [Heteroidentificacao()] };
        CronogramaDoProcesso cronograma = Fases(Fase(Heteroidentificacao(Renda), Heteroidentificacao()), segunda);

        IReadOnlyList<FieldError> faltas = ConferenciaDasBancas.ConferirMinimoDeBancasPorTipo(cronograma, Etapas());

        Causas(faltas).Should().Equal(("fases[1].bancasRequeridas[0]", Codigo));
        faltas.Single().Error.Message.Should().StartWith("A fase 'SEGUNDA_FASE' prevê só a banca");
        ItemAprovado(cronograma, Etapas()).Should().BeFalse();
    }

    [Fact(DisplayName = "Duas bancas sem categoria e uma com categoria no mesmo tipo recusam as duas sem categoria, cada uma na sua posição")]
    public void DuasSemCategoriaEUmaComCategoria_RecusaAsSemCategoria()
    {
        CronogramaDoProcesso cronograma = Fases(Fase(Heteroidentificacao(), Heteroidentificacao(Renda), Heteroidentificacao()));

        Causas(cronograma.RecusasDasDeclaracoes(CandidatosAtendidosPelasBancas.Codigos)).Should().Equal(
            ("fases[0].bancasRequeridas[0].categoriasDocumentoIds", "BancaRequerida.CandidatosAtendidosObrigatorios"),
            ("fases[0].bancasRequeridas[2].categoriasDocumentoIds", "BancaRequerida.CandidatosAtendidosObrigatorios"));
        ConferenciaDasBancas.ConferirMinimoDeBancasPorTipo(cronograma, Etapas()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Banca sem categoria de um tipo ao lado de banca com categoria de outro tipo não é recusada")]
    public void SemCategoriaNumTipoEComCategoriaNoOutro_SemRecusa()
    {
        CronogramaDoProcesso cronograma = Fases(Fase(
            Heteroidentificacao(),
            Heteroidentificacao(),
            DaFase(true, TipoDeBanca.AvaliacaoBiopsicossocial, Renda),
            DaFase(true, TipoDeBanca.AvaliacaoBiopsicossocial, Renda)));

        cronograma.RecusasDasDeclaracoes(CandidatosAtendidosPelasBancas.Codigos).Should().BeEmpty();
        ConferenciaDasBancas.ConferirMinimoDeBancasPorTipo(cronograma, Etapas()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Todas as bancas sem categoria num tipo que exige duas ou mais publicam: sem recusa e com o item aprovado")]
    public void TodasSemCategoriaNoTipoQueExige_SemRecusa()
    {
        CronogramaDoProcesso cronograma = Fases(Fase(Heteroidentificacao(), Heteroidentificacao(), Heteroidentificacao()));

        cronograma.RecusasDasDeclaracoes().Should().BeEmpty();
        ItemAprovado(cronograma, Etapas()).Should().BeTrue();
    }

    [Fact(DisplayName = "O item sai verde com as bancas completas e vermelho com a falta")]
    public void Item_VerdeSemFaltaEVermelhoComFalta()
    {
        ItemAprovado(Fases(Fase(Heteroidentificacao(), Heteroidentificacao())), Etapas()).Should().BeTrue();
        ItemAprovado(Fases(Fase(Heteroidentificacao())), Etapas()).Should().BeFalse();
    }
}
