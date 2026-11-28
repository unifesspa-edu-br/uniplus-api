namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao.FaseDaRetificacaoDeTeste;

/// <summary>
/// Mudar os tipos de decisão que a fase ou a etapa declara muda a entrada do preliminar já publicado, então a
/// retificação exige declarar o novo ciclo; declarar os mesmos tipos em outra ordem não muda nada. Os tipos da fase
/// atingem a fase e as posteriores; os da etapa atingem só a própria etapa.
/// </summary>
public sealed class ConferenciaDoCicloNovoPelosTiposDeDecisaoTests
{
    private const string Materia = "RESULTADO_PROVA";
    private const string OutraMateria = "RESULTADO_ANALISE";

    [Fact(DisplayName = "Mudar só os tipos de decisão da fase do preliminar publicado exige declarar o novo ciclo")]
    public void TiposDaPropriaFase_ExigemDeclararOCiclo()
    {
        FaseCronograma[] publicadas = Cronograma(out ProdutoDaFase preliminar);
        FaseCronograma[] novas = [publicadas[0], ComTiposDeDecisao(publicadas[1], TipoMotivoDeDecisao.Classificacao.Codigo())];

        ResultadoDoCicloNovo resultado = Conferir(publicadas, novas, Item(publicadas[1], preliminar));

        resultado.Recusas.Should().ContainSingle().Which.Error.Code.Should().Be(RetificacaoErrorCodes.CicloNovoNaoDeclarado);
    }

    [Fact(DisplayName = "Mudar só os tipos de decisão de fase anterior à do preliminar exige declarar o novo ciclo")]
    public void TiposDeFaseAnterior_ExigemDeclararOCiclo()
    {
        FaseCronograma[] publicadas = Cronograma(out ProdutoDaFase preliminar);
        FaseCronograma[] novas = [ComTiposDeDecisao(publicadas[0], TipoMotivoDeDecisao.Classificacao.Codigo()), publicadas[1]];

        ResultadoDoCicloNovo resultado = Conferir(publicadas, novas, Item(publicadas[1], preliminar));

        resultado.Recusas.Should().ContainSingle().Which.Error.Code.Should().Be(RetificacaoErrorCodes.CicloNovoNaoDeclarado);
    }

    [Fact(DisplayName = "Mudar só os tipos de decisão de fase posterior à do preliminar não exige declarar o novo ciclo")]
    public void TiposDeFasePosterior_NaoExigem()
    {
        FaseCronograma[] publicadas = Cronograma(out ProdutoDaFase preliminar, comPosterior: true);
        FaseCronograma[] novas = [publicadas[0], publicadas[1], ComTiposDeDecisao(publicadas[2], TipoMotivoDeDecisao.Classificacao.Codigo())];

        Conferir(publicadas, novas, Item(publicadas[1], preliminar)).Recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Declarar os mesmos tipos de decisão da fase em outra ordem não exige declarar o novo ciclo")]
    public void TiposDaFaseEmOutraOrdem_NaoExigem()
    {
        ProdutoDaFase preliminar = Preliminar(Materia);
        FaseCronograma publicada = ComTiposDeDecisao(Fase(2, "PROVA", produtos: [preliminar]), TipoMotivoDeDecisao.Classificacao.Codigo(), TipoMotivoDeDecisao.AnaliseDocumental.Codigo());
        FaseCronograma permutada = ComTiposDeDecisao(publicada, TipoMotivoDeDecisao.AnaliseDocumental.Codigo(), TipoMotivoDeDecisao.Classificacao.Codigo());

        Conferir([publicada], [permutada], Item(publicada, preliminar)).Recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Mudar só os tipos de decisão da etapa do preliminar publicado exige declarar o novo ciclo, e só o dela")]
    public void TiposDaEtapa_ExigemDeclararOCicloSoDaPropriaEtapa()
    {
        EtapaProcesso objetiva = EtapaDaProva(Guid.CreateVersion7(), Materia);
        EtapaProcesso analise = EtapaDaProva(Guid.CreateVersion7(), OutraMateria);
        EtapaProcesso objetivaComTipos = EtapaDaProva(objetiva.Id, Materia, TipoMotivoDeDecisao.Classificacao.Codigo());
        FaseCronograma prova = Fase(1, "PROVA");

        ResultadoDoCicloNovo resultado = ConferirEtapas(prova, [objetiva, analise], [objetivaComTipos, analise], ItemPreliminarDaEtapa(objetiva), ItemPreliminarDaEtapa(analise));

        FieldError recusa = resultado.Recusas.Should().ContainSingle().Which;
        recusa.Error.Message.Should().Contain("'Resultado da prova'").And.NotContain("'Resultado da análise'").And.NotContain(Materia);
    }

    [Fact(DisplayName = "Declarar os mesmos tipos de decisão da etapa em outra ordem não exige declarar o novo ciclo")]
    public void TiposDaEtapaEmOutraOrdem_NaoExigem()
    {
        Guid id = Guid.CreateVersion7();
        EtapaProcesso publicada = EtapaDaProva(id, Materia, TipoMotivoDeDecisao.Classificacao.Codigo(), TipoMotivoDeDecisao.AnaliseDocumental.Codigo());
        EtapaProcesso permutada = EtapaDaProva(id, Materia, TipoMotivoDeDecisao.AnaliseDocumental.Codigo(), TipoMotivoDeDecisao.Classificacao.Codigo());

        ResultadoDoCicloNovo resultado = ConferirEtapas(Fase(1, "PROVA"), [publicada], [permutada], ItemPreliminarDaEtapa(publicada));

        resultado.Recusas.Should().BeEmpty();
    }

    private static FaseCronograma[] Cronograma(out ProdutoDaFase preliminar, bool comPosterior = false)
    {
        preliminar = Preliminar(Materia);
        FaseCronograma inscricao = Fase(1, "INSCRICAO");
        FaseCronograma prova = Fase(2, "PROVA", produtos: [preliminar]);

        return comPosterior ? [inscricao, prova, Fase(3, "RESULTADO")] : [inscricao, prova];
    }

    private static ResultadoDoCicloNovo Conferir(FaseCronograma[] publicadas, FaseCronograma[] novas, IdentidadeDoItemDoEdital publicado) =>
        ConferenciaDoCicloNovoDaRetificacao.Conferir(
            publicadas, [], novas, [], new HashSet<IdentidadeDoItemDoEdital>(), Fatos(Iniciadas(), publicado), NomesDaVersaoPublicada);

    private static ResultadoDoCicloNovo ConferirEtapas(
        FaseCronograma fase, EtapaProcesso[] publicadas, EtapaProcesso[] novas, params IdentidadeDoItemDoEdital[] publicados) =>
        ConferenciaDoCicloNovoDaRetificacao.Conferir(
            [fase], publicadas, [fase], novas, new HashSet<IdentidadeDoItemDoEdital>(), Fatos(Iniciadas(), publicados), NomesDaVersaoPublicada);

    private static IdentidadeDoItemDoEdital ItemPreliminarDaEtapa(EtapaProcesso etapa) =>
        IdentidadeDoItemDoEdital.DaEtapa(etapa, etapa.Produtos.Single(static p => p.Papel == PapelDaPublicacaoPrevista.Preliminar));

    private static EtapaProcesso EtapaDaProva(Guid id, string ato, params string[] tipos)
    {
        EtapaProcesso etapa = EtapaComPeso(id, "Prova objetiva", 1m, "PROVA", ProdutoDaEtapa.Criar(ato, PapelDaPublicacaoPrevista.Preliminar));
        etapa.DefinirTiposDeDecisao(tipos).IsSuccess.Should().BeTrue();

        return etapa;
    }
}
