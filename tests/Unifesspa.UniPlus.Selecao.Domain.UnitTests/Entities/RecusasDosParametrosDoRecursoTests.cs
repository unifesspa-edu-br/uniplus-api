namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// As recusas dos parâmetros do recurso nas duas entidades: o código de erro de cada dono, o campo relativo ao recurso e a
/// acumulação das causas independentes.
/// </summary>
public sealed class RecusasDosParametrosDoRecursoTests
{
    private static readonly ReferenciaRegra Regra = ReferenciaRegra.De(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), "v1", new string('a', 64));

    private static readonly ParametrosDeclaradosDoRecurso TodasAsCausas = new(
        new PrazoDeclarado(0m, "DIAS"),
        AdmiteInstanciaSuperior: false,
        new PrazoDeclarado(1m, "DIAS_UTEIS"),
        EfeitoSuspensivo: null,
        GuardaDaVaga: "talvez");

    private static readonly ParametrosDeclaradosDoRecurso Valido = ParametrosDoRecursoDeTeste.Declarados();

    private static readonly ParametrosDeclaradosDoRecurso ComInstanciaSuperior = ParametrosDoRecursoDeTeste.ComInstanciaSuperior();

    private static readonly Dictionary<MotivoDaRecusaDosParametros, ParametrosDeclaradosDoRecurso> UmaCausaPorMotivo = new()
    {
        [MotivoDaRecusaDosParametros.PrazoPrecisaoInvalida] = Valido with { PrazoDeInterposicao = new PrazoDeclarado(1.12345m, "HORAS") },
        [MotivoDaRecusaDosParametros.PrazoNaoPositivo] = Valido with { PrazoDeInterposicao = new PrazoDeclarado(0m, "HORAS") },
        [MotivoDaRecusaDosParametros.PrazoEmDiasCorridos] = Valido with { PrazoDeInterposicao = new PrazoDeclarado(1m, "DIAS") },
        [MotivoDaRecusaDosParametros.PrazoSemUnidadeDeclaravel] = Valido with { PrazoDeInterposicao = new PrazoDeclarado(1m, null) },
        [MotivoDaRecusaDosParametros.PrazoEmFracaoDeDiaUtil] = Valido with { PrazoDeInterposicao = new PrazoDeclarado(1.5m, "DIAS_UTEIS") },
        [MotivoDaRecusaDosParametros.EfeitoSuspensivoNaoDeclarado] = Valido with { EfeitoSuspensivo = null },
        [MotivoDaRecusaDosParametros.GuardaDaVagaNaoDeclaravel] = Valido with { GuardaDaVaga = "talvez" },
        [MotivoDaRecusaDosParametros.PrazoSegundaInstanciaNaoDeclarado] = ComInstanciaSuperior with { PrazoDaSegundaInstancia = PrazoDeclarado.Ausente },
        [MotivoDaRecusaDosParametros.PrazoSegundaInstanciaSemInstanciaSuperior] = Valido with { PrazoDaSegundaInstancia = new PrazoDeclarado(1m, "HORAS") },
        [MotivoDaRecusaDosParametros.NaoGuardaComInstanciaSuperior] = ComInstanciaSuperior with { GuardaDaVaga = "NAO_GUARDA" },
    };

    [Fact(DisplayName = "Causas independentes são acumuladas na ordem nas duas entidades, cada uma com o código do dono")]
    public void Criar_RecusasIndependentes_AcumulaNasDuasEntidades()
    {
        string[] sufixos = ["PrazoNaoPositivo", "EfeitoSuspensivoNaoDeclarado", "GuardaDaVagaNaoDeclaravel", "PrazoSegundaInstanciaSemInstanciaSuperior"];

        RegraRecursoFase.Criar(Regra, TodasAsCausas, Guid.Empty).Errors.Select(static erro => erro.Error.Code)
            .Should().Equal(sufixos.Select(static sufixo => $"RegraRecursoFase.{sufixo}"));
        RecursoDaEtapa.Criar(EventoInicialDoPrazo.PublicacaoDoAto, Regra, TodasAsCausas, Guid.Empty).Errors.Select(static erro => erro.Error.Code)
            .Should().Equal(sufixos.Select(static sufixo => $"RecursoDaEtapa.{sufixo}"));
    }

    [Fact(DisplayName = "O campo de cada recusa é relativo ao recurso, nas duas entidades")]
    public void Criar_CampoRelativoAoRecurso()
    {
        string[] campos = ["prazoValor", "efeitoSuspensivo", "guardaDaVaga", "prazoSegundaInstanciaValor"];

        RegraRecursoFase.Criar(Regra, TodasAsCausas, Guid.Empty).Errors.Select(static erro => erro.Field).Should().Equal(campos);
        RecursoDaEtapa.Criar(EventoInicialDoPrazo.PublicacaoDoAto, Regra, TodasAsCausas, Guid.Empty).Errors.Select(static erro => erro.Field).Should().Equal(campos);
    }

    [Fact(DisplayName = "Há uma declaração para cada motivo de recusa")]
    public void UmaCausaPorMotivo_CobreTodosOsMotivos() =>
        UmaCausaPorMotivo.Keys.Should().BeEquivalentTo(Enum.GetValues<MotivoDaRecusaDosParametros>());

    [Fact(DisplayName = "Cada motivo vira um código próprio em cada dono, e os dois donos nomeiam o mesmo conjunto de motivos")]
    public void CadaMotivo_CodigoProprioNosDoisDonos()
    {
        foreach (MotivoDaRecusaDosParametros motivo in Enum.GetValues<MotivoDaRecusaDosParametros>())
        {
            ParametrosDeclaradosDoRecurso declarados = UmaCausaPorMotivo[motivo];

            string doFase = RegraRecursoFase.Criar(Regra, declarados, Guid.Empty).Errors.Single().Error.Code;
            string daEtapa = RecursoDaEtapa.Criar(EventoInicialDoPrazo.PublicacaoDoAto, Regra, declarados, Guid.Empty).Errors.Single().Error.Code;

            doFase.Should().Be(motivo == MotivoDaRecusaDosParametros.PrazoPrecisaoInvalida ? "uniplus.validacao.formato" : $"RegraRecursoFase.{motivo}");
            daEtapa.Should().Be(motivo == MotivoDaRecusaDosParametros.PrazoPrecisaoInvalida ? "uniplus.validacao.formato" : $"RecursoDaEtapa.{motivo}");
        }
    }

    [Fact(DisplayName = "A referência de regra inválida da fase é recusada na raiz da regra, junto das recusas dos parâmetros")]
    public void RegraInvalida_NaRaizDaRegra()
    {
        ReferenciaRegra outra = ReferenciaRegra.De("FADA_MULTIPLICATIVO", "v1", new string('b', 64));

        IReadOnlyList<FieldError> erros = RegraRecursoFase.Criar(outra, TodasAsCausas, Guid.Empty).Errors;

        erros.Select(static erro => erro.Error.Code).Should().StartWith("RegraRecursoFase.RegraCatalogoInvalida");
        erros[0].Field.Should().BeEmpty();
        erros.Should().HaveCount(5);
    }
}
