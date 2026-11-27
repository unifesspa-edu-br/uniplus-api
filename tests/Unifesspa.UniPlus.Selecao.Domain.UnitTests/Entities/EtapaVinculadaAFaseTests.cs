namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Globalization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioEtapaVinculadaAFase;

/// <summary>
/// A etapa declara a fase a que pertence, e a raiz resolve o vínculo. É o que permite
/// qualquer fase subdividir-se — não só a que o cadastro marcava como agrupadora — e o
/// que faz a habilitação com oito etapas ser exprimível. Aqui, a etapa acontece dentro da
/// janela da fase que a abriga.
/// </summary>
public sealed class EtapaVinculadaAFaseTests
{
    // ── A etapa acontece dentro da fase que a abriga ──
    //
    // A matriz abaixo é a mesma que o editor já aplica na tela: dentro passa, bordas
    // coincidentes passam, transbordo de qualquer lado recusa, e o que não foi declarado não
    // é conferido.

    [Theory(DisplayName = "Etapa contida na janela da fase é aceita")]
    [InlineData("2027-03-02T08:00:00Z", "2027-03-09T18:00:00Z")]   // dentro
    [InlineData("2027-03-01T00:00:00Z", "2027-03-10T00:00:00Z")]   // bordas coincidentes
    public void EtapaDentroDaFase_Aceita(string inicio, string fim)
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();

        Result resultado = processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO", Instante(inicio), Instante(fim))]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Etapa que começa antes da fase é recusada sem repetir o nome da etapa nem o código da fase")]
    public void EtapaComecaAntesDaFase_Recusada()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();

        Result resultado = processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO",
                Instante("2027-02-28T08:00:00Z"), Instante("2027-03-09T18:00:00Z"))]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaComecaAntesDaFase");
        resultado.Error.Message.Should().Contain("período da fase").And.NotContain("Prova objetiva").And.NotContain("AVALIACAO").And.NotContain("2027");
    }

    [Fact(DisplayName = "Etapa que termina depois da fase é recusada sem repetir o nome da etapa nem o código da fase")]
    public void EtapaTerminaDepoisDaFase_Recusada()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();

        Result resultado = processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO",
                Instante("2027-03-02T08:00:00Z"), Instante("2027-03-11T18:00:00Z"))]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaTerminaDepoisDaFase");
        resultado.Error.Message.Should().Contain("período da fase").And.NotContain("Prova objetiva").And.NotContain("AVALIACAO").And.NotContain("2027");
    }

    [Fact(DisplayName = "Etapa que transborda dos dois lados é recusada pelo início, sem somar mensagens")]
    public void EtapaTransbordaDosDoisLados_RecusaPeloInicio()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();

        Result resultado = processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO",
                Instante("2027-02-28T08:00:00Z"), Instante("2027-03-11T18:00:00Z"))]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaComecaAntesDaFase");
    }

    [Fact(DisplayName = "Etapa sem janela não é conferida — em branco ela acontece na janela da fase")]
    public void EtapaSemJanela_NaoEhConferida()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();

        Result resultado = processo.DefinirEtapas(
            [Etapa("Prova objetiva", "AVALIACAO")]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Fase sem janela não contém nada — a etapa datada passa")]
    public void FaseSemJanela_NaoEhConferida()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases([Fase(1, "AVALIACAO")], [])
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO",
                Instante("2027-03-02T08:00:00Z"), Instante("2027-03-09T18:00:00Z"))]);

        resultado.IsSuccess.Should().BeTrue(
            "exigir data da fase por causa da etapa inventaria obrigação que o cadastro não faz");
    }

    [Fact(DisplayName = "Etapa sem fase declarada não é conferida contra fase alguma")]
    public void EtapaSemFaseDeclarada_NaoEhConferida()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();

        Result resultado = processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", faseCodigo: null,
                Instante("2020-01-01T08:00:00Z"), Instante("2020-01-02T18:00:00Z"))]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ── A etapa que declara só um dos extremos ──
    //
    // Meia janela é estado válido: a etapa que só diz quando começa termina com a fase, e a
    // que só diz quando termina começa com ela. O extremo declarado é conferido contra os
    // DOIS extremos da fase, senão uma prova marcada para depois de a fase acabar passa por
    // não transbordar de nenhum lado que a comparação homônima alcance.

    [Theory(DisplayName = "Etapa de meia janela dentro da fase é aceita")]
    [InlineData("2027-03-02T08:00:00Z", null)]   // começa dentro, termina com a fase
    [InlineData(null, "2027-03-09T18:00:00Z")]   // começa com a fase, termina dentro
    public void EtapaDeMeiaJanelaDentroDaFase_Aceita(string? inicio, string? fim)
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();

        Result resultado = processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO", InstanteOpcional(inicio), InstanteOpcional(fim))]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Theory(DisplayName = "Etapa de meia janela que acontece inteira fora da fase é recusada")]
    [InlineData("2027-03-20T08:00:00Z", null)]   // começa depois de a fase acabar
    [InlineData(null, "2027-02-20T18:00:00Z")]   // termina antes de a fase começar
    public void EtapaDeMeiaJanelaForaDaFase_Recusada(string? inicio, string? fim)
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();

        Result resultado = processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO", InstanteOpcional(inicio), InstanteOpcional(fim))]);

        resultado.IsFailure.Should().BeTrue(
            "a etapa herda da fase o extremo que não declarou, e herdá-lo aqui produziria uma "
            + "janela que termina antes de começar");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaForaDaJanelaDaFase");
        resultado.Error.Message.Should().Contain("período da fase").And.NotContain("Prova objetiva").And.NotContain("AVALIACAO").And.NotContain("2027");
    }

    [Fact(DisplayName = "Fase de meia janela confere o extremo que declarou")]
    public void FaseDeMeiaJanela_ConfereOExtremoQueDeclarou()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases(
            [Fase(1, "AVALIACAO", inicio: null, fim: Instante("2027-03-10T00:00:00Z"))],
            []).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO", Instante("2027-03-20T08:00:00Z"), fim: null)]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaForaDaJanelaDaFase");
    }

    [Fact(DisplayName = "Cada etapa é conferida contra a fase que ela declara, não contra a primeira do cronograma")]
    public void CadaEtapaEhConferidaContraAPropriaFase()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirCronogramaFases([
            Fase(1, "HABILITACAO", Instante("2027-03-01T00:00:00Z"), Instante("2027-03-10T00:00:00Z")),
            Fase(2, "AVALIACAO", Instante("2027-04-01T00:00:00Z"), Instante("2027-04-10T00:00:00Z")),
        ], []).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirEtapas([
            EtapaComJanela("Análise documental", "HABILITACAO", Instante("2027-03-02T08:00:00Z"), Instante("2027-03-09T18:00:00Z")),
            EtapaComJanela("Prova objetiva", "AVALIACAO", Instante("2027-03-05T08:00:00Z"), Instante("2027-03-06T18:00:00Z"), 2),
        ]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaComecaAntesDaFase");
        resultado.Error.Message.Should().Contain("período da fase").And.NotContain("Prova objetiva").And.NotContain("AVALIACAO").And.NotContain("2027");
    }

    // ── A fase também se move ──
    //
    // A gravação das etapas confere a janela contra o cronograma DAQUELE instante. Quem
    // encolhe a fase depois desfaz a mesma invariante pelo outro lado, e não há gravação de
    // etapa adiante que a reconfira.

    [Fact(DisplayName = "Encolher a fase para fora da etapa já datada é recusado na gravação do cronograma")]
    public void CronogramaQueEncolheAFase_RecusaAEtapaJaDatada()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();
        processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO",
                Instante("2027-03-02T08:00:00Z"), Instante("2027-03-09T18:00:00Z"))]).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirCronogramaFases(
            [Fase(1, "AVALIACAO", Instante("2027-03-05T00:00:00Z"), Instante("2027-03-10T00:00:00Z"))],
            []);

        resultado.IsFailure.Should().BeTrue(
            "sem esta metade o certame volta a ser publicável com a prova pendurada fora da fase");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaComecaAntesDaFase");
    }

    [Fact(DisplayName = "Remover do cronograma a fase que hospeda a etapa datada é aceito — a etapa sai na poda")]
    public void CronogramaQueRemoveAFase_NaoEhBarradoPelaEtapaQueSaiJunto()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeMarco();
        processo.DefinirEtapas(
            [EtapaComJanela("Prova objetiva", "AVALIACAO",
                Instante("2027-03-02T08:00:00Z"), Instante("2027-03-09T18:00:00Z"))]).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirCronogramaFases(
            [Fase(1, "HABILITACAO", Instante("2027-05-01T00:00:00Z"), Instante("2027-05-10T00:00:00Z"))],
            []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Should().BeEmpty("a etapa da fase removida sai junto com ela");
    }
}
