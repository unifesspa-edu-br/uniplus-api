namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// A conferência dos parâmetros do recurso declarados por quem configura: as nove causas de recusa, o campo de cada uma, a ordem
/// em que se acumulam e o que vale quando o edital não declara a guarda da vaga.
/// </summary>
public sealed class ParametrosDeclaradosDoRecursoTests
{
    private static readonly ParametrosDeclaradosDoRecurso Valido = ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas);

    private static ParametrosDeclaradosDoRecurso ComInstanciaSuperior => Valido with
    {
        AdmiteInstanciaSuperior = true,
        PrazoDaSegundaInstancia = new PrazoDeclarado(3m, "diasUteis"),
    };

    private static readonly ParametrosDeclaradosDoRecurso[] DeclaracoesDeUmaCausa =
    [
        Valido with { PrazoDeInterposicao = new PrazoDeclarado(0m, "horas") },
        Valido with { PrazoDeInterposicao = new PrazoDeclarado(1.12345m, "horas") },
        Valido with { PrazoDeInterposicao = new PrazoDeclarado(1m, "dias") },
        Valido with { PrazoDeInterposicao = new PrazoDeclarado(1m, null) },
        Valido with { PrazoDeInterposicao = new PrazoDeclarado(1.5m, "diasUteis") },
        Valido with { EfeitoSuspensivo = null },
        Valido with { GuardaDaVaga = "talvez" },
        ComInstanciaSuperior with { PrazoDaSegundaInstancia = PrazoDeclarado.Ausente },
        Valido with { PrazoDaSegundaInstancia = new PrazoDeclarado(1m, "horas") },
        ComInstanciaSuperior with { GuardaDaVaga = "naoGuarda" },
    ];

    private static (MotivoDaRecusaDosParametros Motivo, string Campo)[] Recusas(ParametrosDeclaradosDoRecurso declarados) =>
        [.. declarados.Conferir().Recusas.Select(static recusa => (recusa.Motivo, recusa.Campo))];

    [Fact(DisplayName = "Parâmetros válidos sem instância superior são conferidos, e a guarda não declarada vale até o resultado definitivo")]
    public void Valido_Conferido()
    {
        (ParametrosRegraPrazoRecurso? conferidos, IReadOnlyList<RecusaDosParametrosDoRecurso> recusas) = Valido.Conferir();

        recusas.Should().BeEmpty();
        conferidos.Should().Be(new ParametrosRegraPrazoRecurso(
            new PrazoDoRecurso(48m, UnidadePrazo.Horas), false, null, EfeitoSuspensivo.SemEfeitoSuspensivo, GuardaDaVaga.AteOResultadoDefinitivo));
    }

    [Fact(DisplayName = "Com instância superior, o prazo da 2ª instância é conferido junto do de interposição")]
    public void ComInstanciaSuperior_Conferido()
    {
        ParametrosRegraPrazoRecurso? conferidos = ComInstanciaSuperior.Conferir().Conferidos;

        conferidos!.AdmiteInstanciaSuperior.Should().BeTrue();
        conferidos.PrazoDaSegundaInstancia.Should().Be(new PrazoDoRecurso(3m, UnidadePrazo.DiasUteis));
    }

    [Fact]
    public void Conferir_PrazosComCincoCasas_RecusaOsDois()
    {
        ParametrosDeclaradosDoRecurso declarados = ComInstanciaSuperior with
        {
            PrazoDeInterposicao = new PrazoDeclarado(1.12345m, "horas"),
            PrazoDaSegundaInstancia = new PrazoDeclarado(2.12345m, "horas"),
        };
        Recusas(declarados).Should().Equal(
            (MotivoDaRecusaDosParametros.PrazoPrecisaoInvalida, "prazoValor"),
            (MotivoDaRecusaDosParametros.PrazoPrecisaoInvalida, "prazoSegundaInstanciaValor"));
    }

    [Theory(DisplayName = "A guarda declarada é conferida pelo código, e as duas opções valem sem instância superior")]
    [InlineData("ateOResultadoDefinitivo", GuardaDaVaga.AteOResultadoDefinitivo)]
    [InlineData("naoGuarda", GuardaDaVaga.NaoGuarda)]
    [InlineData(null, GuardaDaVaga.AteOResultadoDefinitivo)]
    public void Guarda_ConferidaPeloCodigo(string? codigo, GuardaDaVaga esperada) =>
        (Valido with { GuardaDaVaga = codigo }).Conferir().Conferidos!.GuardaDaVaga.Should().Be(esperada);

    [Theory(DisplayName = "Prazo de interposição ausente, zero ou negativo é recusado no valor")]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void PrazoNaoPositivo(int? valor) =>
        Recusas(Valido with { PrazoDeInterposicao = new PrazoDeclarado(valor, "horas") })
            .Should().Equal((MotivoDaRecusaDosParametros.PrazoNaoPositivo, "prazoValor"));

    [Theory(DisplayName = "Unidade ausente ou desconhecida é recusada como unidade não declarável")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("semanas")]
    [InlineData("DiasUteis")]
    public void PrazoSemUnidadeDeclaravel(string? unidade) =>
        Recusas(Valido with { PrazoDeInterposicao = new PrazoDeclarado(2m, unidade) })
            .Should().Equal((MotivoDaRecusaDosParametros.PrazoSemUnidadeDeclaravel, "prazoUnidade"));

    [Fact(DisplayName = "Dias corridos são recusados na unidade, com causa própria")]
    public void PrazoEmDiasCorridos() =>
        Recusas(Valido with { PrazoDeInterposicao = new PrazoDeclarado(5m, "dias") })
            .Should().Equal((MotivoDaRecusaDosParametros.PrazoEmDiasCorridos, "prazoUnidade"));

    [Theory(DisplayName = "Fração de dia útil é recusada no valor; em horas a fração é aceita")]
    [InlineData(1.5)]
    [InlineData(0.5)]
    public void PrazoEmFracaoDeDiaUtil(decimal valor)
    {
        Recusas(Valido with { PrazoDeInterposicao = new PrazoDeclarado(valor, "diasUteis") })
            .Should().Equal((MotivoDaRecusaDosParametros.PrazoEmFracaoDeDiaUtil, "prazoValor"));
        Recusas(Valido with { PrazoDeInterposicao = new PrazoDeclarado(valor, "horas") }).Should().BeEmpty();
    }

    [Fact(DisplayName = "O valor não positivo é recusado antes da unidade: trocar a unidade não conserta um prazo que fecha antes de abrir")]
    public void ValorAntesDaUnidade() =>
        Recusas(Valido with { PrazoDeInterposicao = new PrazoDeclarado(-5m, "dias") })
            .Should().Equal((MotivoDaRecusaDosParametros.PrazoNaoPositivo, "prazoValor"));

    [Theory(DisplayName = "Efeito suspensivo ausente ou desconhecido é recusado, cada um com a mensagem própria")]
    [InlineData(null, "Declare o efeito suspensivo do recurso na 1ª instância. Opção aceita: sem efeito suspensivo.")]
    [InlineData("comEfeitoSuspensivo", "O efeito suspensivo declarado não é uma das opções aceitas. Opção aceita: sem efeito suspensivo.")]
    public void EfeitoSuspensivoNaoDeclarado(string? codigo, string mensagem)
    {
        RecusaDosParametrosDoRecurso recusa = (Valido with { EfeitoSuspensivo = codigo }).Conferir().Recusas.Single();

        recusa.Motivo.Should().Be(MotivoDaRecusaDosParametros.EfeitoSuspensivoNaoDeclarado);
        recusa.Campo.Should().Be("efeitoSuspensivo");
        recusa.Mensagem.Should().Be(mensagem);
    }

    [Theory(DisplayName = "Guarda declarada com código desconhecido é recusada; a ausente não é recusa")]
    [InlineData("")]
    [InlineData("guardaAteOFim")]
    [InlineData("NaoGuarda")]
    public void GuardaDaVagaNaoDeclaravel(string codigo) =>
        Recusas(Valido with { GuardaDaVaga = codigo })
            .Should().Equal((MotivoDaRecusaDosParametros.GuardaDaVagaNaoDeclaravel, "guardaDaVaga"));

    [Fact(DisplayName = "Com instância superior, o prazo da 2ª instância ausente é recusado no valor")]
    public void PrazoSegundaInstanciaNaoDeclarado() =>
        Recusas(ComInstanciaSuperior with { PrazoDaSegundaInstancia = PrazoDeclarado.Ausente })
            .Should().Equal((MotivoDaRecusaDosParametros.PrazoSegundaInstanciaNaoDeclarado, "prazoSegundaInstanciaValor"));

    [Fact(DisplayName = "O prazo da 2ª instância passa pelas mesmas conferências, no campo dela")]
    public void PrazoDaSegundaInstancia_MesmasConferencias()
    {
        Recusas(ComInstanciaSuperior with { PrazoDaSegundaInstancia = new PrazoDeclarado(0m, "diasUteis") })
            .Should().Equal((MotivoDaRecusaDosParametros.PrazoNaoPositivo, "prazoSegundaInstanciaValor"));
        Recusas(ComInstanciaSuperior with { PrazoDaSegundaInstancia = new PrazoDeclarado(3m, "dias") })
            .Should().Equal((MotivoDaRecusaDosParametros.PrazoEmDiasCorridos, "prazoSegundaInstanciaUnidade"));
        Recusas(ComInstanciaSuperior with { PrazoDaSegundaInstancia = new PrazoDeclarado(3m, null) })
            .Should().Equal((MotivoDaRecusaDosParametros.PrazoSemUnidadeDeclaravel, "prazoSegundaInstanciaUnidade"));
        Recusas(ComInstanciaSuperior with { PrazoDaSegundaInstancia = new PrazoDeclarado(2.5m, "diasUteis") })
            .Should().Equal((MotivoDaRecusaDosParametros.PrazoEmFracaoDeDiaUtil, "prazoSegundaInstanciaValor"));
    }

    [Theory(DisplayName = "Sem instância superior, o prazo da 2ª instância é recusado, no valor ou, sozinha, na unidade")]
    [InlineData(3, "diasUteis", "prazoSegundaInstanciaValor")]
    [InlineData(3, null, "prazoSegundaInstanciaValor")]
    [InlineData(null, "horas", "prazoSegundaInstanciaUnidade")]
    public void PrazoSegundaInstanciaSemInstanciaSuperior(int? valor, string? unidade, string campo) =>
        Recusas(Valido with { PrazoDaSegundaInstancia = new PrazoDeclarado(valor, unidade) })
            .Should().Equal((MotivoDaRecusaDosParametros.PrazoSegundaInstanciaSemInstanciaSuperior, campo));

    [Fact(DisplayName = "Com instância superior, não guardar a vaga é recusado na guarda")]
    public void NaoGuardaComInstanciaSuperior()
    {
        RecusaDosParametrosDoRecurso recusa = (ComInstanciaSuperior with { GuardaDaVaga = "naoGuarda" }).Conferir().Recusas.Single();

        recusa.Motivo.Should().Be(MotivoDaRecusaDosParametros.NaoGuardaComInstanciaSuperior);
        recusa.Campo.Should().Be("guardaDaVaga");
    }

    [Fact(DisplayName = "Sem instância superior, não guardar a vaga é aceito")]
    public void NaoGuardaSemInstanciaSuperior() =>
        Recusas(Valido with { GuardaDaVaga = "naoGuarda" }).Should().BeEmpty();

    [Fact(DisplayName = "A guarda desconhecida com instância superior é recusada só por ela: a outra recusa depende de um dado já recusado")]
    public void GuardaDesconhecidaComInstanciaSuperior() =>
        Recusas(ComInstanciaSuperior with { GuardaDaVaga = "talvez" })
            .Should().Equal((MotivoDaRecusaDosParametros.GuardaDaVagaNaoDeclaravel, "guardaDaVaga"));

    [Fact(DisplayName = "Causas independentes são acumuladas na ordem: interposição, efeito, guarda, 2ª instância, não guardar")]
    public void CausasIndependentes_AcumuladasEmOrdem()
    {
        ParametrosDeclaradosDoRecurso declarados = new(
            new PrazoDeclarado(0m, "dias"), AdmiteInstanciaSuperior: false, new PrazoDeclarado(1m, "diasUteis"), EfeitoSuspensivo: null, GuardaDaVaga: "talvez");

        Recusas(declarados).Should().Equal(
            (MotivoDaRecusaDosParametros.PrazoNaoPositivo, "prazoValor"),
            (MotivoDaRecusaDosParametros.EfeitoSuspensivoNaoDeclarado, "efeitoSuspensivo"),
            (MotivoDaRecusaDosParametros.GuardaDaVagaNaoDeclaravel, "guardaDaVaga"),
            (MotivoDaRecusaDosParametros.PrazoSegundaInstanciaSemInstanciaSuperior, "prazoSegundaInstanciaValor"));
    }

    [Fact(DisplayName = "Com recusa, nenhum parâmetro conferido é devolvido")]
    public void ComRecusa_SemConferidos() =>
        (Valido with { EfeitoSuspensivo = null }).Conferir().Conferidos.Should().BeNull();

    [Fact(DisplayName = "A mensagem da recusa não repete o valor recebido")]
    public void Mensagem_NaoRepeteOValorRecebido()
    {
        ParametrosDeclaradosDoRecurso declarados = Valido with
        {
            PrazoDeInterposicao = new PrazoDeclarado(77.7777m, "dias"),
            EfeitoSuspensivo = "efeitoEstranho",
            GuardaDaVaga = "guardaEstranha",
        };

        declarados.Conferir().Recusas.Select(static recusa => recusa.Mensagem).Should().OnlyContain(
            mensagem => !mensagem.Contains("77", StringComparison.Ordinal)
                && !mensagem.Contains("Estranh", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Todo motivo de recusa é alcançado por alguma declaração")]
    public void TodoMotivo_Alcancado() =>
        DeclaracoesDeUmaCausa.SelectMany(static declarada => declarada.Conferir().Recusas.Select(static recusa => recusa.Motivo))
            .Should().BeEquivalentTo(Enum.GetValues<MotivoDaRecusaDosParametros>());
}
