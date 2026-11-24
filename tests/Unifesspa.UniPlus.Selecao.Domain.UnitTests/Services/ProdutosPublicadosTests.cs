namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>
/// A reconciliação dos produtos publicados preserva a identidade da linha viva (a instância
/// rastreada) sempre que a configuração que chega reconhece a mesma publicação.
/// </summary>
public sealed class ProdutosPublicadosTests
{
    private const string PrimeiroAto = "RESULTADO_ANALISE";
    private const string SegundoAto = "RESULTADO_ANULACAO";

    private static string Ordinal(string ato) => ato;

    private static string MesmaChave(string ato) => "RESULTADO";

    private static ProdutoDaFase Fase(string ato, PapelProdutoFase? papel) => ProdutoDaFase.Criar(ato, papel);

    private static ProdutoDaEtapa Etapa(string ato, PapelProdutoFase? papel) => ProdutoDaEtapa.Criar(ato, papel);

    private static ProdutosReconciliados<ProdutoDaFase> UmaPassada(
        IReadOnlyList<ProdutoDaFase> vivos, IReadOnlyList<ProdutoDaFase> chegados) =>
        ProdutosPublicados.ReconciliarPreservandoIdentidade(vivos, chegados, Ordinal);

    private static ProdutosReconciliados<ProdutoDaFase> DuasPassadas(
        IReadOnlyList<ProdutoDaFase> vivos, IReadOnlyList<ProdutoDaFase> chegados) =>
        ProdutosPublicados.ReconciliarPreservandoIdentidadeMesmoComTrocaDePapel(vivos, chegados, Ordinal);

    [Fact]
    public void UmaPassada_ParExato_ReaproveitaALinhaViva()
    {
        ProdutoDaFase viva = Fase("GABARITO", PapelProdutoFase.Preliminar);
        ProdutoDaFase chegado = Fase("GABARITO", PapelProdutoFase.Preliminar);

        ProdutosReconciliados<ProdutoDaFase> resultado = UmaPassada([viva], [chegado]);

        resultado.Produtos.Should().ContainSingle().Which.Should().BeSameAs(viva);
    }

    [Fact]
    public void UmaPassada_SemPar_EntraComoProdutoNovo()
    {
        ProdutoDaFase chegado = Fase("GABARITO", PapelProdutoFase.Preliminar);

        ProdutosReconciliados<ProdutoDaFase> resultado = UmaPassada([], [chegado]);

        resultado.Produtos.Should().ContainSingle().Which.Should().BeSameAs(chegado);
    }

    [Fact]
    public void UmaPassada_LinhaVivaSemPar_Sai()
    {
        ProdutoDaFase viva = Fase("GABARITO", PapelProdutoFase.Preliminar);
        ProdutoDaFase chegado = Fase("RESULTADO", PapelProdutoFase.Definitivo);

        ProdutosReconciliados<ProdutoDaFase> resultado = UmaPassada([viva], [chegado]);

        resultado.Produtos.Should().NotContain(viva);
    }

    [Fact]
    public void UmaPassada_TrocaDePapel_RecriaOProdutoEMantemOPapelDaLinhaViva()
    {
        ProdutoDaFase viva = Fase("GABARITO", PapelProdutoFase.Preliminar);
        ProdutoDaFase chegado = Fase("GABARITO", PapelProdutoFase.Definitivo);

        ProdutosReconciliados<ProdutoDaFase> resultado = UmaPassada([viva], [chegado]);

        resultado.Produtos.Should().ContainSingle().Which.Should().BeSameAs(chegado);
        viva.Papel.Should().Be(PapelProdutoFase.Preliminar);
    }

    [Fact]
    public void UmaPassada_ParRepetidoNaChegada_ConsomeAPrimeiraLinhaVivaNaoConsumidaDeCadaVez()
    {
        ProdutoDaFase primeiraViva = Fase("GABARITO", PapelProdutoFase.Preliminar);
        ProdutoDaFase segundaViva = Fase("GABARITO", PapelProdutoFase.Preliminar);
        ProdutoDaFase primeiroChegado = Fase("GABARITO", PapelProdutoFase.Preliminar);
        ProdutoDaFase segundoChegado = Fase("GABARITO", PapelProdutoFase.Preliminar);
        ProdutoDaFase terceiroChegado = Fase("GABARITO", PapelProdutoFase.Preliminar);

        ProdutosReconciliados<ProdutoDaFase> resultado = UmaPassada(
            [primeiraViva, segundaViva], [primeiroChegado, segundoChegado, terceiroChegado]);

        resultado.Produtos.Should().Equal(primeiraViva, segundaViva, terceiroChegado);
    }

    [Fact]
    public void UmaPassada_PreservaAOrdemDeChegada()
    {
        ProdutoDaFase vivaDoResultado = Fase("RESULTADO", PapelProdutoFase.Definitivo);
        ProdutoDaFase vivaDoGabarito = Fase("GABARITO", PapelProdutoFase.Preliminar);
        ProdutoDaFase novo = Fase("EDITAL", null);

        ProdutosReconciliados<ProdutoDaFase> resultado = UmaPassada(
            [vivaDoGabarito, vivaDoResultado],
            [Fase("RESULTADO", PapelProdutoFase.Definitivo), novo, Fase("GABARITO", PapelProdutoFase.Preliminar)]);

        resultado.Produtos.Should().Equal(vivaDoResultado, novo, vivaDoGabarito);
    }

    [Fact]
    public void UmaPassada_ParSemPapel_CasaComLinhaVivaSemPapel()
    {
        ProdutoDaFase viva = Fase("AVISO", null);

        ProdutosReconciliados<ProdutoDaFase> resultado = UmaPassada([viva], [Fase("AVISO", null)]);

        resultado.Produtos.Should().ContainSingle().Which.Should().BeSameAs(viva);
    }

    [Fact]
    public void UmaPassada_ComparaOAtoPelaChaveDeQuemChama()
    {
        ProdutoDaEtapa viva = Etapa(SegundoAto, PapelProdutoFase.Preliminar);
        ProdutoDaEtapa chegado = Etapa(PrimeiroAto, PapelProdutoFase.Preliminar);

        ProdutosReconciliados<ProdutoDaEtapa> pelaForma = ProdutosPublicados
            .ReconciliarPreservandoIdentidade([viva], [chegado], MesmaChave);
        ProdutosReconciliados<ProdutoDaEtapa> ordinal = ProdutosPublicados
            .ReconciliarPreservandoIdentidade([viva], [chegado], Ordinal);

        pelaForma.Produtos.Should().ContainSingle().Which.Should().BeSameAs(viva);
        ordinal.Produtos.Should().ContainSingle().Which.Should().BeSameAs(chegado);
    }

    [Fact]
    public void UmaPassada_MapaAssociaOIdDoChegadoAoIdDaLinhaFinal()
    {
        ProdutoDaFase viva = Fase("GABARITO", PapelProdutoFase.Preliminar);
        ProdutoDaFase chegadoComPar = Fase("GABARITO", PapelProdutoFase.Preliminar);
        ProdutoDaFase chegadoNovo = Fase("EDITAL", null);

        ProdutosReconciliados<ProdutoDaFase> resultado = UmaPassada([viva], [chegadoComPar, chegadoNovo]);

        resultado.IdentidadeVivaPorIdentidadeChegada.Should().BeEquivalentTo(
            new Dictionary<Guid, Guid> { [chegadoComPar.Id] = viva.Id, [chegadoNovo.Id] = chegadoNovo.Id });
    }

    [Fact]
    public void DuasPassadas_ParExato_TemPrioridadeSobreOMesmoAto()
    {
        ProdutoDaFase viva = Fase("GABARITO", PapelProdutoFase.Preliminar);
        ProdutoDaFase semPar = Fase("GABARITO", PapelProdutoFase.Definitivo);
        ProdutoDaFase comPar = Fase("GABARITO", PapelProdutoFase.Preliminar);

        ProdutosReconciliados<ProdutoDaFase> resultado = DuasPassadas([viva], [semPar, comPar]);

        resultado.Produtos.Should().Equal(semPar, viva);
    }

    [Fact]
    public void DuasPassadas_TrocaDePapel_ReaproveitaALinhaVivaEAtualizaOPapel()
    {
        ProdutoDaFase viva = Fase("GABARITO", PapelProdutoFase.Preliminar);
        ProdutoDaFase chegado = Fase("GABARITO", PapelProdutoFase.Definitivo);

        ProdutosReconciliados<ProdutoDaFase> resultado = DuasPassadas([viva], [chegado]);

        resultado.Produtos.Should().ContainSingle().Which.Should().BeSameAs(viva);
        viva.Papel.Should().Be(PapelProdutoFase.Definitivo);
    }

    [Fact]
    public void DuasPassadas_CadaLinhaVivaEConsumidaNoMaximoUmaVez()
    {
        ProdutoDaFase viva = Fase("GABARITO", PapelProdutoFase.Preliminar);
        ProdutoDaFase primeiro = Fase("GABARITO", PapelProdutoFase.Definitivo);
        ProdutoDaFase segundo = Fase("GABARITO", null);

        ProdutosReconciliados<ProdutoDaFase> resultado = DuasPassadas([viva], [primeiro, segundo]);

        resultado.Produtos.Should().Equal(viva, segundo);
    }

    [Fact]
    public void DuasPassadas_ChegadoSemParNemAto_EntraComoProdutoNovo()
    {
        ProdutoDaFase viva = Fase("GABARITO", PapelProdutoFase.Preliminar);
        ProdutoDaFase chegado = Fase("RESULTADO", PapelProdutoFase.Definitivo);

        ProdutosReconciliados<ProdutoDaFase> resultado = DuasPassadas([viva], [chegado]);

        resultado.Produtos.Should().ContainSingle().Which.Should().BeSameAs(chegado);
        viva.Papel.Should().Be(PapelProdutoFase.Preliminar);
    }

    [Fact]
    public void DuasPassadas_LinhaVivaNaoConsumida_Sai()
    {
        ProdutoDaFase viva = Fase("GABARITO", PapelProdutoFase.Preliminar);

        ProdutosReconciliados<ProdutoDaFase> resultado = DuasPassadas([viva], []);

        resultado.Produtos.Should().BeEmpty();
    }

    [Fact]
    public void DuasPassadas_LinhaReaproveitadaPeloAto_OcupaOLugarDaOrdemDeChegada()
    {
        ProdutoDaFase vivaQueTrocaDePapel = Fase("GABARITO", PapelProdutoFase.Preliminar);
        ProdutoDaFase vivaDoResultado = Fase("RESULTADO", PapelProdutoFase.Definitivo);
        ProdutoDaFase novo = Fase("EDITAL", null);

        ProdutosReconciliados<ProdutoDaFase> resultado = DuasPassadas(
            [vivaQueTrocaDePapel, vivaDoResultado],
            [Fase("GABARITO", PapelProdutoFase.Definitivo), novo, Fase("RESULTADO", PapelProdutoFase.Definitivo)]);

        resultado.Produtos.Should().Equal(vivaQueTrocaDePapel, novo, vivaDoResultado);
    }

    [Fact]
    public void DuasPassadas_ComparaOAtoDaSegundaPassadaPelaChaveDeQuemChama()
    {
        ProdutoDaEtapa viva = Etapa(SegundoAto, PapelProdutoFase.Preliminar);
        ProdutoDaEtapa chegado = Etapa(PrimeiroAto, PapelProdutoFase.Definitivo);

        ProdutosReconciliados<ProdutoDaEtapa> pelaForma = ProdutosPublicados
            .ReconciliarPreservandoIdentidadeMesmoComTrocaDePapel([viva], [chegado], MesmaChave);

        pelaForma.Produtos.Should().ContainSingle().Which.Should().BeSameAs(viva);
        viva.Papel.Should().Be(PapelProdutoFase.Definitivo);
    }

    [Fact]
    public void DuasPassadas_ComChaveOrdinal_AtoComFormaDiferenteNaoCasa()
    {
        ProdutoDaEtapa viva = Etapa(SegundoAto, PapelProdutoFase.Preliminar);
        ProdutoDaEtapa chegado = Etapa(PrimeiroAto, PapelProdutoFase.Definitivo);

        ProdutosReconciliados<ProdutoDaEtapa> resultado = ProdutosPublicados
            .ReconciliarPreservandoIdentidadeMesmoComTrocaDePapel([viva], [chegado], Ordinal);

        resultado.Produtos.Should().ContainSingle().Which.Should().BeSameAs(chegado);
    }

    [Fact]
    public void DuasPassadas_MapaAponta_ParaALinhaFinalDeCadaChegado()
    {
        ProdutoDaFase vivaDoPar = Fase("RESULTADO", PapelProdutoFase.Definitivo);
        ProdutoDaFase vivaQueTrocaDePapel = Fase("GABARITO", PapelProdutoFase.Preliminar);
        ProdutoDaFase[] chegados =
        [
            Fase("RESULTADO", PapelProdutoFase.Definitivo),
            Fase("GABARITO", PapelProdutoFase.Definitivo),
            Fase("EDITAL", null),
        ];

        ProdutosReconciliados<ProdutoDaFase> resultado = DuasPassadas([vivaDoPar, vivaQueTrocaDePapel], chegados);

        resultado.IdentidadeVivaPorIdentidadeChegada.Values.Should().Equal(
            vivaDoPar.Id, vivaQueTrocaDePapel.Id, chegados[2].Id);
        resultado.IdentidadeVivaPorIdentidadeChegada.Keys.Should().Equal(chegados.Select(static c => c.Id));
    }

    [Fact]
    public void PosicoesRepetidas_ComVariosGrupos_InformaCadaRepeticaoAPartirDaSegundaOcorrencia()
    {
        ProdutoDaFase[] produtos =
        [
            Fase("A", PapelProdutoFase.Preliminar),
            Fase("B", PapelProdutoFase.Preliminar),
            Fase("B", PapelProdutoFase.Preliminar),
            Fase("A", PapelProdutoFase.Preliminar),
            Fase("B", PapelProdutoFase.Preliminar),
        ];

        ProdutosPublicados.PosicoesRepetidas(produtos, Ordinal).Should().Equal(2, 3, 4);
    }

    [Fact]
    public void PosicoesRepetidas_MesmoAtoEmPapeisDiferentes_NaoERepeticao()
    {
        ProdutoDaFase[] produtos =
        [
            Fase("A", PapelProdutoFase.Preliminar),
            Fase("A", PapelProdutoFase.Definitivo),
            Fase("A", null),
        ];

        ProdutosPublicados.PosicoesRepetidas(produtos, Ordinal).Should().BeEmpty();
    }

    [Fact]
    public void PosicoesRepetidas_ComparaOAtoPelaChaveDeQuemChama()
    {
        ProdutoDaEtapa[] produtos =
        [
            Etapa(PrimeiroAto, PapelProdutoFase.Preliminar),
            Etapa(SegundoAto, PapelProdutoFase.Preliminar),
        ];

        ProdutosPublicados.PosicoesRepetidas(produtos, MesmaChave).Should().Equal(1);
        ProdutosPublicados.PosicoesRepetidas(produtos, Ordinal).Should().BeEmpty();
    }
}
