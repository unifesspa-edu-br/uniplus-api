namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoRestauradorDeConfiguracao;

/// <summary>
/// Os tipos de decisão declarados por fase e etapa entram no envelope congelado e no hash da publicação. Estas
/// provas conferem isso pelo caminho real da restauração, em que a prova em sombra aprova e o grafo devolvido é
/// aplicado à raiz viva, e não pela reposição direta da entidade.
/// </summary>
public sealed class RestauradorDeConfiguracaoTiposDeDecisaoTests
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
        byte[] bytes = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo)).Bytes;
        CorpusEnvelopeEntrada.Publicar(processo);
        return CorpusEnvelopeEntrada.VersaoDeAbertura(processo, bytes);
    }

    private static string HashCongeladoDe(ProcessoSeletivo processo) =>
        CorpusEnvelopeEntrada.VersaoDeAbertura(processo, CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(processo)).Bytes)
            .HashConfiguracao;

    [Fact(DisplayName = "Restaurar pelo caminho real preserva os tipos de decisão que a fase declarava na raiz viva")]
    public void Restaurar_PreservaOsTiposDeDecisaoDaFaseNaRaizViva()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico(tiposDeDecisaoDaFaseFinal: [TipoDeClassificacao, TipoDeIsencaoDaTaxa]);
        VersaoConfiguracao versao = PublicadoCongelandoTudo(processo);

        ReporORestauradoNaRaizViva(processo, versao);

        FaseFinalDe(processo).TiposDeDecisao.Should().Equal(
            [TipoDeClassificacao, TipoDeIsencaoDaTaxa], "o envelope congela os tipos da fase e o grafo os repõe na raiz viva");
    }

    [Fact(DisplayName = "Restaurar pelo caminho real preserva os tipos de decisão que a etapa declarava na raiz viva")]
    public void Restaurar_PreservaOsTiposDeDecisaoDaEtapaNaRaizViva()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        EtapaDeProvaObjetivaDe(processo).DefinirTiposDeDecisao([TipoDeClassificacao]).IsSuccess.Should().BeTrue();
        VersaoConfiguracao versao = PublicadoCongelandoTudo(processo);

        ReporORestauradoNaRaizViva(processo, versao);

        EtapaDeProvaObjetivaDe(processo).TiposDeDecisao.Should().Equal(
            [TipoDeClassificacao], "o envelope congela os tipos da etapa e o grafo os repõe na raiz viva");
    }

    [Fact(DisplayName = "Alterar só os tipos de decisão da fase muda o hash da configuração congelada")]
    public void AlterarSoOsTiposDeDecisaoDaFase_MudaOHash()
    {
        string semTipos = HashCongeladoDe(CorpusEnvelopeProcessoRico.ProcessoRico());
        string comTipos = HashCongeladoDe(CorpusEnvelopeProcessoRico.ProcessoRico(tiposDeDecisaoDaFaseFinal: [TipoDeClassificacao]));
        string comOutrosTipos = HashCongeladoDe(
            CorpusEnvelopeProcessoRico.ProcessoRico(tiposDeDecisaoDaFaseFinal: [TipoDeIsencaoDaTaxa, TipoDeAnaliseDocumental]));

        comTipos.Should().NotBe(semTipos, "a declaração da fase entra no envelope");
        comOutrosTipos.Should().NotBe(semTipos).And.NotBe(comTipos, "trocar a declaração da fase altera o hash");
    }

    [Fact(DisplayName = "Alterar só os tipos de decisão da etapa muda o hash da configuração congelada")]
    public void AlterarSoOsTiposDeDecisaoDaEtapa_MudaOHash()
    {
        ProcessoSeletivo processo = CorpusEnvelopeProcessoRico.ProcessoRico();
        string semTipos = HashCongeladoDe(processo);

        EtapaDeProvaObjetivaDe(processo).DefinirTiposDeDecisao([TipoDeClassificacao]).IsSuccess.Should().BeTrue();
        string comTipos = HashCongeladoDe(processo);
        EtapaDeProvaObjetivaDe(processo).DefinirTiposDeDecisao([TipoDeIsencaoDaTaxa, TipoDeAnaliseDocumental]).IsSuccess.Should().BeTrue();
        string comOutrosTipos = HashCongeladoDe(processo);

        comTipos.Should().NotBe(semTipos, "a declaração da etapa entra no envelope");
        comOutrosTipos.Should().NotBe(semTipos).And.NotBe(comTipos, "trocar a declaração da etapa altera o hash");
    }
}
