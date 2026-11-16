namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using Xunit;

/// <summary>
/// Os tipos de decisão declarados por fase e etapa ainda não entram no envelope congelado nem no hash da
/// publicação. Estas provas caracterizam essa limitação pelo caminho real da restauração, em que a prova em
/// sombra aprova e o grafo devolvido é aplicado à raiz viva, e não pela reposição direta da entidade.
/// </summary>
public sealed partial class RestauradorDeConfiguracaoTests
{
    private const string TipoDeClassificacao = TipoMotivoDeDecisaoCodigo.Classificacao;
    private const string TipoDeIsencaoDaTaxa = TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa;
    private const string TipoDeAnaliseDocumental = TipoMotivoDeDecisaoCodigo.AnaliseDocumental;
    private const string CodigoDaFaseFinal = "RESULTADO_FINAL";

    private static FaseCronograma FaseFinalDe(ProcessoSeletivo processo) =>
        processo.CronogramaFases.Single(static f => f.Codigo == CodigoDaFaseFinal);

    private static EtapaProcesso EtapaDeProvaObjetivaDe(ProcessoSeletivo processo) =>
        processo.Etapas.Single(static e => e.Nome == "Prova Objetiva");

    private static VersaoConfiguracao PublicadoCongelandoTudo(ProcessoSeletivo processo)
    {
        byte[] bytes = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes;
        CorpusEnvelope.Publicar(processo);
        return CorpusEnvelope.VersaoDeAbertura(processo, bytes);
    }

    private static string HashCongeladoDe(ProcessoSeletivo processo) =>
        CorpusEnvelope.VersaoDeAbertura(processo, CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo)).Bytes)
            .HashConfiguracao;

    [Fact(DisplayName = "Restaurar pelo caminho real ainda não preserva os tipos de decisão que a fase declarava na raiz viva")]
    public void Restaurar_AindaNaoPreservaOsTiposDeDecisaoDaFaseNaRaizViva()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico(tiposDeDecisaoDaFaseFinal: [TipoDeClassificacao, TipoDeIsencaoDaTaxa]);
        VersaoConfiguracao versao = PublicadoCongelandoTudo(processo);
        FaseFinalDe(processo).TiposDeDecisao.Should().Equal(
            [TipoDeClassificacao, TipoDeIsencaoDaTaxa], "pré-condição: a fase publicada declarava os tipos");

        ReporORestauradoNaRaizViva(processo, versao);

        FaseFinalDe(processo).TiposDeDecisao.Should().BeEmpty(
            "o envelope ainda não congela os tipos da fase: a prova em sombra aprova e a reposição troca a declaração viva pela do grafo");
    }

    [Fact(DisplayName = "Restaurar pelo caminho real ainda não preserva os tipos de decisão que a etapa declarava na raiz viva")]
    public void Restaurar_AindaNaoPreservaOsTiposDeDecisaoDaEtapaNaRaizViva()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        EtapaDeProvaObjetivaDe(processo).DefinirTiposDeDecisao([TipoDeClassificacao, TipoDeIsencaoDaTaxa]).IsSuccess.Should().BeTrue();
        VersaoConfiguracao versao = PublicadoCongelandoTudo(processo);
        EtapaDeProvaObjetivaDe(processo).TiposDeDecisao.Should().Equal(
            [TipoDeClassificacao, TipoDeIsencaoDaTaxa], "pré-condição: a etapa publicada declarava os tipos");

        ReporORestauradoNaRaizViva(processo, versao);

        EtapaDeProvaObjetivaDe(processo).TiposDeDecisao.Should().BeEmpty(
            "o envelope ainda não congela os tipos da etapa: a prova em sombra aprova e a reposição troca a declaração viva pela do grafo");
    }

    [Fact(DisplayName = "Alterar só os tipos de decisão da fase ainda não muda o hash da configuração congelada")]
    public void AlterarSoOsTiposDeDecisaoDaFase_AindaNaoMudaOHash()
    {
        string semTipos = HashCongeladoDe(CorpusEnvelope.ProcessoRico());
        string comTipos = HashCongeladoDe(CorpusEnvelope.ProcessoRico(tiposDeDecisaoDaFaseFinal: [TipoDeClassificacao]));
        string comOutrosTipos = HashCongeladoDe(
            CorpusEnvelope.ProcessoRico(tiposDeDecisaoDaFaseFinal: [TipoDeIsencaoDaTaxa, TipoDeAnaliseDocumental]));

        comTipos.Should().Be(semTipos, "a declaração da fase ainda não entra no envelope");
        comOutrosTipos.Should().Be(semTipos, "trocar a declaração da fase ainda não altera o hash");
    }

    [Fact(DisplayName = "Alterar só os tipos de decisão da etapa ainda não muda o hash da configuração congelada")]
    public void AlterarSoOsTiposDeDecisaoDaEtapa_AindaNaoMudaOHash()
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico();
        string semTipos = HashCongeladoDe(processo);

        EtapaDeProvaObjetivaDe(processo).DefinirTiposDeDecisao([TipoDeClassificacao]).IsSuccess.Should().BeTrue();
        string comTipos = HashCongeladoDe(processo);
        EtapaDeProvaObjetivaDe(processo).DefinirTiposDeDecisao([TipoDeIsencaoDaTaxa, TipoDeAnaliseDocumental]).IsSuccess.Should().BeTrue();
        string comOutrosTipos = HashCongeladoDe(processo);

        comTipos.Should().Be(semTipos, "a declaração da etapa ainda não entra no envelope");
        comOutrosTipos.Should().Be(semTipos, "trocar a declaração da etapa ainda não altera o hash");
    }
}
