namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O obrigatório sem envio que sai ou passa a facultativo registra a conclusão decorrente da fase; o obrigatório com
/// envio não passa a facultativo (UNI-REQ-0211, UNI-REQ-0144).
/// </summary>
public sealed class ConferenciaDaObrigatoriedadeDosFormulariosTests
{
    private static readonly Guid FaseA = Guid.Parse("00000000-0000-7000-8000-00000000000a");
    private static readonly Guid FaseB = Guid.Parse("00000000-0000-7000-8000-00000000000b");

    private static CodigoDoFormulario Codigo(string valor) => CodigoDoFormulario.Criar(valor).Value!;

    private static FormularioNoDocumento Formulario(string codigo, Guid fase, bool obrigatorio) => new(Codigo(codigo), fase, obrigatorio);

    private static FatosDaExecucao ComEnvio(params string[] codigos) => new()
    {
        Instante = FaseDaRetificacaoDeTeste.Instante,
        FasesIniciadas = FasesIniciadas.Nenhuma,
        FormulariosComEnvio = new HashSet<CodigoDoFormulario>(codigos.Select(Codigo)),
    };

    [Fact(DisplayName = "O obrigatório sem envio que sai do documento registra a conclusão da fase, com os obrigatórios que restam")]
    public void ObrigatorioSemEnvioRetirado_RegistraConclusao()
    {
        ResultadoDaObrigatoriedade resultado = ConferenciaDaObrigatoriedadeDosFormularios.Conferir(
            [Formulario("DADOS", FaseA, true), Formulario("INSCRICAO", FaseA, true)],
            [Formulario("INSCRICAO", FaseA, true)],
            ComEnvio());

        resultado.Recusas.Should().BeEmpty();
        ConclusaoDecorrente conclusao = resultado.ConclusoesDecorrentes.Should().ContainSingle().Subject;
        conclusao.FaseCanonicaOrigemId.Should().Be(FaseA);
        conclusao.ObrigatoriosNaVersaoNova.Should().Equal(Codigo("INSCRICAO"));
    }

    [Fact(DisplayName = "O obrigatório sem envio que passa a facultativo registra a conclusão, e a fase sem obrigatório restante a leva vazia")]
    public void ObrigatorioViraFacultativo_RegistraConclusao()
    {
        ResultadoDaObrigatoriedade resultado = ConferenciaDaObrigatoriedadeDosFormularios.Conferir(
            [Formulario("INSCRICAO", FaseA, true)], [Formulario("INSCRICAO", FaseA, false)], ComEnvio());

        resultado.Recusas.Should().BeEmpty();
        resultado.ConclusoesDecorrentes.Should().ContainSingle().Which.ObrigatoriosNaVersaoNova.Should().BeEmpty();
    }

    [Fact(DisplayName = "O obrigatório com envio que passa a facultativo é recusado, no campo dele, e não registra conclusão")]
    public void ObrigatorioComEnvioViraFacultativo_Recusa()
    {
        ResultadoDaObrigatoriedade resultado = ConferenciaDaObrigatoriedadeDosFormularios.Conferir(
            [Formulario("INSCRICAO", FaseA, true)], [Formulario("INSCRICAO", FaseA, false)], ComEnvio("INSCRICAO"));

        resultado.Recusas.Should().ContainSingle().Which.Should().Match<Unifesspa.UniPlus.Kernel.Results.FieldError>(
            r => r.Field == "formularios[INSCRICAO]" && r.Error.Code == RetificacaoErrorCodes.FormularioObrigatorioComEnvioNaoViraFacultativo);
        resultado.ConclusoesDecorrentes.Should().BeEmpty();
    }

    [Fact(DisplayName = "A retirada do obrigatório com envio não é recusa daqui: é a do formulário com envio")]
    public void ObrigatorioComEnvioRetirado_NaoRecusaAqui()
    {
        ConferenciaDaObrigatoriedadeDosFormularios.Conferir([Formulario("INSCRICAO", FaseA, true)], [], ComEnvio("INSCRICAO")).Recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Facultativo retirado, obrigatório mantido e obrigatório acrescentado não registram conclusão")]
    public void SemDiminuicaoDoConjunto_NaoRegistra()
    {
        ConferenciaDaObrigatoriedadeDosFormularios.Conferir(
            [Formulario("INSCRICAO", FaseA, true), Formulario("EXTRA", FaseA, false)],
            [Formulario("INSCRICAO", FaseA, true), Formulario("NOVO", FaseA, true)],
            ComEnvio()).ConclusoesDecorrentes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Uma conclusão por fase afetada, em ordem de fase, ainda que dois obrigatórios saiam da mesma fase")]
    public void UmaConclusaoPorFase()
    {
        ResultadoDaObrigatoriedade resultado = ConferenciaDaObrigatoriedadeDosFormularios.Conferir(
            [Formulario("A1", FaseB, true), Formulario("A2", FaseA, true), Formulario("A3", FaseA, true), Formulario("A4", FaseB, true)],
            [Formulario("A4", FaseB, true)],
            ComEnvio());

        resultado.ConclusoesDecorrentes.Select(static c => c.FaseCanonicaOrigemId).Should().Equal(FaseA, FaseB);
        resultado.ConclusoesDecorrentes[0].ObrigatoriosNaVersaoNova.Should().BeEmpty();
        resultado.ConclusoesDecorrentes[1].ObrigatoriosNaVersaoNova.Should().Equal(Codigo("A4"));
    }

    [Fact(DisplayName = "O obrigatório sem envio que muda de fase continua obrigatório e diminui o conjunto da fase de origem")]
    public void ObrigatorioMudaDeFase_RegistraConclusaoDaFaseDeOrigem()
    {
        ResultadoDaObrigatoriedade resultado = ConferenciaDaObrigatoriedadeDosFormularios.Conferir(
            [Formulario("INSCRICAO", FaseA, true), Formulario("DADOS", FaseA, true)],
            [Formulario("INSCRICAO", FaseA, true), Formulario("DADOS", FaseB, true)],
            ComEnvio());

        resultado.Recusas.Should().BeEmpty();
        ConclusaoDecorrente conclusao = resultado.ConclusoesDecorrentes.Should().ContainSingle().Subject;
        conclusao.FaseCanonicaOrigemId.Should().Be(FaseA);
        conclusao.ObrigatoriosNaVersaoNova.Should().Equal(Codigo("INSCRICAO"));
    }

    [Fact(DisplayName = "O obrigatório com envio que muda de fase, mantido obrigatório, não é recusado aqui")]
    public void ObrigatorioComEnvioMudaDeFase_NaoRecusaAqui()
    {
        ConferenciaDaObrigatoriedadeDosFormularios.Conferir(
            [Formulario("INSCRICAO", FaseA, true)], [Formulario("INSCRICAO", FaseB, true)], ComEnvio("INSCRICAO")).Recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Vários obrigatórios com envio que passam a facultativos acumulam as recusas em ordem ordinal do código")]
    public void ObrigatoriosComEnvioViramFacultativos_AcumulamEmOrdem()
    {
        ResultadoDaObrigatoriedade resultado = ConferenciaDaObrigatoriedadeDosFormularios.Conferir(
            [Formulario("ISENCAO", FaseA, true), Formulario("DADOS", FaseA, true), Formulario("INSCRICAO", FaseA, true)],
            [Formulario("INSCRICAO", FaseA, false), Formulario("ISENCAO", FaseA, false), Formulario("DADOS", FaseA, false)],
            ComEnvio("ISENCAO", "INSCRICAO", "DADOS"));

        resultado.Recusas.Select(static r => r.Field).Should().Equal("formularios[DADOS]", "formularios[INSCRICAO]", "formularios[ISENCAO]");
        resultado.ConclusoesDecorrentes.Should().BeEmpty();
    }
}
