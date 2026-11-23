namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.OrdemCanonica;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;

/// <summary>
/// A configuração que um documento lido entrega, com as fases e as etapas na ordem do autor e uma parte recusada, é conferida ao
/// congelar: cada causa sai no ponteiro da posição em que o item veio no documento, e a causa da parte recusada, que a leitura já
/// deu como ponteiro, não passa de novo pela conversão. Nenhuma causa sai em notação de pontos e colchetes.
/// </summary>
public sealed class ConferenciaDoDocumentoLidoComOrigemDistintaTests
{
    private const int PosicaoDaFaseDeProdutosRepetidosNoDocumento = 1;
    private const int PosicaoDaEtapaSemDefinitivoNoDocumento = 4;

    private static ConfiguracaoDeclarada DocumentoComAsPartesForaDeOrdem()
    {
        FaseCronograma comAtoRepetido = new ModeloDeFase("INSCRICAO", 1).Janela(1, 5)
            .Publicando(ModeloDeFase.Preliminar("RESULTADO_PROVA"), ModeloDeFase.Preliminar("RESULTADO_PROVA")).Adotada();
        FaseCronograma comParecerSemResultado = (new ModeloDeFase("RESULTADO", 2) { EmiteParecerIndividual = true }).Janela(6, 9).Adotada();
        EtapaProcesso semDefinitivo = new ModeloDeEtapa("Prova") { Ordem = 1, Produtos = [ModeloDeEtapa.Preliminar("RESULTADO_PROVA")] }.Adotada();
        EtapaProcesso conforme = new ModeloDeEtapa("Redação") { Ordem = 2 }.Adotada();
        OrigemNoDocumento origem = OrigemForaDeOrdem(comAtoRepetido, comParecerSemResultado, semDefinitivo, conforme);

        return Lida([comAtoRepetido, comParecerSemResultado], [semDefinitivo, conforme], origem);
    }

    private static OrigemNoDocumento OrigemForaDeOrdem(
        FaseCronograma comAtoRepetido, FaseCronograma comParecerSemResultado, EtapaProcesso semDefinitivo, EtapaProcesso conforme)
    {
        ParteRecusada daLeitura = new("fases[0].bancasRequeridas", "banca-a", 5, Causa("/cronograma/fases/1/bancasRequeridas/5"));

        return OrigemNoDocumento.Reunir(
        [
            OrigemNoDocumento.DaLista(
                "fases", [(comParecerSemResultado, 0), (comAtoRepetido, PosicaoDaFaseDeProdutosRepetidosNoDocumento)], OrdemCanonicaDaConfiguracao.Fases, [daLeitura]),
            OrigemNoDocumento.DaLista(
                "etapas", [(conforme, 0), (semDefinitivo, PosicaoDaEtapaSemDefinitivoNoDocumento)], OrdemCanonicaDaConfiguracao.Etapas, []),
        ]);
    }

    private static IReadOnlyList<FieldError> CausasConvertidas(ConfiguracaoDeclarada documento) =>
        PonteiroNoDocumento.Converter(
            ConfiguracaoAptaACongelar.Conferir(Guid.CreateVersion7(), documento, CadastrosDeTeste.Padrao).Errors, documento.Origem);

    [Fact(DisplayName = "A causa da fase e da etapa lidas sai no ponteiro da posição de origem, distinta da canônica")]
    public void Causa_DaFaseEDaEtapa_SaiNaPosicaoDeOrigem()
    {
        IReadOnlyList<FieldError> causas = CausasConvertidas(DocumentoComAsPartesForaDeOrdem());

        causas.Select(static causa => causa.Field).Should().Contain(
        [
            "/cronograma/fases/1/publicacoesPrevistas",
            "/cronograma/fases/0/emiteParecerIndividual",
            "/etapas/4/publicacoesPrevistas",
        ]);
        causas.Count(static causa => causa.Field == "/cronograma/fases/1/bancasRequeridas/5").Should().Be(1);
    }

    [Fact(DisplayName = "A causa da parte recusada sai na frente das recusas das declarações que ficaram, com o ponteiro que a leitura deu")]
    public void Causa_DaParteRecusada_SaiNaFrenteDasDeclaracoes()
    {
        IReadOnlyList<FieldError> causas = CausasConvertidas(DocumentoComAsPartesForaDeOrdem());

        int daParte = causas.ToList().FindIndex(static causa => causa.Error.Code == "Leitura.ParteRecusada");
        int daDeclaracao = causas.ToList().FindIndex(static causa => causa.Field == "/cronograma/fases/1/publicacoesPrevistas");

        daParte.Should().BeGreaterThanOrEqualTo(0).And.BeLessThan(daDeclaracao);
    }

    [Fact(DisplayName = "Nenhuma causa da configuração lida sai em notação de pontos e colchetes")]
    public void Causa_DaConfiguracaoLida_NuncaSaiEmNotacaoDePontosEColchetes()
    {
        string?[] campos = [.. CausasConvertidas(DocumentoComAsPartesForaDeOrdem()).Select(static causa => causa.Field)];

        campos.OfType<string>().Should().OnlyContain(static campo => campo.Length == 0 || campo[0] == '/');
    }

    [Fact(DisplayName = "A regra da fase com a lista de produtos recusada fica sem causa na conferência, e a das demais fases segue")]
    public void Conferencia_ComParteRecusadaNosProdutos_NaoTrazACausaSuspensaETrazAsDemais()
    {
        ConfiguracaoDeclarada documento = DocumentoComAsPartesForaDeOrdem();
        ConfiguracaoDeclarada comProdutosRecusados = Lida(
            [.. documento.Cronograma.CronogramaFases],
            [.. documento.Avaliacao.Etapas],
            OrigemNoDocumento.Reunir([documento.Origem, ComPartesRecusadas("fases[1].produtos")]));

        string?[] campos = [.. CausasConvertidas(comProdutosRecusados).Select(static causa => causa.Field)];

        campos.Should().NotContain("/cronograma/fases/0/emiteParecerIndividual");
        campos.Should().Contain("/cronograma/fases/1/publicacoesPrevistas");
    }
}
