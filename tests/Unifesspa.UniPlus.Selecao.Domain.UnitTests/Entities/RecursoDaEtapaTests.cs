namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cobertura de <see cref="RecursoDaEtapa.Criar"/> — as invariantes de forma sobre o prazo de
/// interposição, o efeito suspensivo, a guarda da vaga e o prazo da 2ª instância.
/// </summary>
/// <remarks>
/// <para>
/// O recurso da etapa carrega o MESMO <see cref="ParametrosRegraPrazoRecurso"/> que a regra da fase, conferido pela mesma
/// declaração: a configuração que a fase recusa com 422 não pode ser gravada na etapa em silêncio.
/// </para>
/// <para>
/// O que legitimamente difere entre as duas é a <b>âncora</b>: a fase conta sempre da
/// publicação do ato, a etapa também admite a ciência individual. A âncora muda QUANDO o
/// relógio começa, não COMO ele conta — por isso a recusa de dia corrido vale nas duas.
/// </para>
/// </remarks>
public sealed class RecursoDaEtapaTests
{
    private static readonly ReferenciaRegra Regra = ReferenciaRegra.De(
        RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), "v1", new string('a', 64));

    private static readonly Guid Ancora = Guid.CreateVersion7();

    private static Result<RecursoDaEtapa> Criar(
        ParametrosDeclaradosDoRecurso parametros,
        EventoInicialDoPrazo? ancora = EventoInicialDoPrazo.PublicacaoDoAto) =>
        RecursoDaEtapa.Criar(ancora, Regra, parametros, Ancora);

    [Fact(DisplayName = "Recurso da etapa com prazo e evento que inicia o prazo válidos é criado")]
    public void PrazoValido_Cria()
    {
        Criar(ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas)).IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "Prazo não positivo é recusado")]
    [InlineData(0)]
    [InlineData(-5)]
    public void PrazoNaoPositivo_Recusa(int prazo)
    {
        Result<RecursoDaEtapa> resultado = Criar(ParametrosDoRecursoDeTeste.Declarados(prazo, UnidadePrazo.Horas));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RecursoDaEtapa.PrazoNaoPositivo");
    }

    /// <summary>
    /// UNI-REQ-0113 fala do prazo em que o CANDIDATO tem de agir — feriado encolhe a janela
    /// dele qualquer que seja o instante que iniciou a contagem. Por isso a recusa alcança
    /// também a âncora de ciência individual.
    /// </summary>
    [Theory(DisplayName = "Prazo em dias corridos é recusado nas duas âncoras")]
    [InlineData(EventoInicialDoPrazo.PublicacaoDoAto)]
    [InlineData(EventoInicialDoPrazo.CienciaIndividual)]
    public void PrazoEmDiasCorridos_Recusa(EventoInicialDoPrazo ancora)
    {
        Result<RecursoDaEtapa> resultado = Criar(ParametrosDoRecursoDeTeste.Declarados(3m, UnidadePrazo.Dias), ancora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RecursoDaEtapa.PrazoEmDiasCorridos");
    }

    [Fact(DisplayName = "Prazo sem unidade declarável é recusado")]
    public void PrazoSemUnidade_Recusa()
    {
        Result<RecursoDaEtapa> resultado = Criar(ParametrosDoRecursoDeTeste.Declarados() with { PrazoDeInterposicao = new PrazoDeclarado(2m, null) });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RecursoDaEtapa.PrazoSemUnidadeDeclaravel");
    }

    [Fact(DisplayName = "Fração de dia útil é recusada — prazo menor que um dia se declara em horas")]
    public void FracaoDeDiaUtil_Recusa()
    {
        Result<RecursoDaEtapa> resultado = Criar(ParametrosDoRecursoDeTeste.Declarados(2.5m, UnidadePrazo.DiasUteis));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RecursoDaEtapa.PrazoEmFracaoDeDiaUtil");
    }

    [Fact(DisplayName = "Âncora não declarada continua sendo recusada")]
    public void AncoraNenhuma_Recusa()
    {
        Result<RecursoDaEtapa> resultado = Criar(ParametrosDoRecursoDeTeste.Declarados(), null);

        resultado.IsFailure.Should().BeTrue();
    }
    /// <summary>
    /// O decodificador do envelope exige este código exato ao reidratar a janela recursal da
    /// etapa. Aceitar outra entrada do catálogo — ainda que do tipo certo — produziria um
    /// certame publicável cujo envelope não volta, e o descarte da retificação deixaria de ser
    /// possível. A regra da fase já recusava o mesmo input.
    /// </summary>
    [Fact(DisplayName = "Recusa regra de código diferente do prazo ancorado em ato")]
    public void Recusa_RegraDeOutroCodigo()
    {
        ReferenciaRegra outra = ReferenciaRegra.De(
            "RECURSO-PRAZO-OUTRA-CONTAGEM", "v1", new string('b', 64));

        Result<RecursoDaEtapa> resultado = RecursoDaEtapa.Criar(
            EventoInicialDoPrazo.PublicacaoDoAto, outra, ParametrosDoRecursoDeTeste.Declarados(), Ancora);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RecursoDaEtapa.RegraCatalogoInvalida");
    }

    [Fact(DisplayName = "Aceita a regra de prazo ancorado em ato")]
    public void Aceita_RegraAncoradaEmAto()
    {
        Result<RecursoDaEtapa> resultado = RecursoDaEtapa.Criar(
            EventoInicialDoPrazo.PublicacaoDoAto, Regra, ParametrosDoRecursoDeTeste.Declarados(), Ancora);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Recurso da etapa com instância superior guarda o prazo próprio da 2ª instância")]
    public void ComInstanciaSuperior_GuardaOPrazoDaSegundaInstancia()
    {
        Result<RecursoDaEtapa> resultado = Criar(ParametrosDoRecursoDeTeste.ComInstanciaSuperior());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Parametros.AdmiteInstanciaSuperior.Should().BeTrue();
        resultado.Value.Parametros.PrazoDaSegundaInstancia.Should().Be(new PrazoDoRecurso(3m, UnidadePrazo.DiasUteis));
    }

    [Fact(DisplayName = "Recurso da etapa com instância superior e sem prazo da 2ª instância é recusada no campo do prazo")]
    public void ComInstanciaSuperior_SemPrazoDaSegundaInstancia_Recusa()
    {
        Result<RecursoDaEtapa> resultado = Criar(ParametrosDoRecursoDeTeste.Declarados(admiteInstanciaSuperior: true));

        resultado.Error!.Code.Should().Be("RecursoDaEtapa.PrazoSegundaInstanciaNaoDeclarado");
        resultado.Errors.Single().Field.Should().Be("prazoSegundaInstanciaValor");
    }

    [Fact(DisplayName = "Recurso da etapa sem instância superior recusa o prazo da 2ª instância")]
    public void SemInstanciaSuperior_ComPrazoDaSegundaInstancia_Recusa()
    {
        Result<RecursoDaEtapa> resultado = Criar(ParametrosDoRecursoDeTeste.Declarados(segundaInstancia: new PrazoDoRecurso(3m, UnidadePrazo.DiasUteis)));

        resultado.Error!.Code.Should().Be("RecursoDaEtapa.PrazoSegundaInstanciaSemInstanciaSuperior");
    }

    [Fact(DisplayName = "Recurso da etapa com instância superior que não guarda a vaga é recusado")]
    public void ComInstanciaSuperior_NaoGuarda_Recusa()
    {
        Result<RecursoDaEtapa> resultado = Criar(ParametrosDoRecursoDeTeste.ComInstanciaSuperior() with { GuardaDaVaga = "NAO_GUARDA" });

        resultado.Error!.Code.Should().Be("RecursoDaEtapa.NaoGuardaComInstanciaSuperior");
    }

    [Theory(DisplayName = "Recurso da etapa aceita cada uma das três opções de efeito suspensivo")]
    [InlineData("PARA_TODOS_OS_RECURSOS", EfeitoSuspensivo.ParaTodosOsRecursos)]
    [InlineData("SEM_EFEITO_SUSPENSIVO", EfeitoSuspensivo.SemEfeitoSuspensivo)]
    [InlineData("UNIDADE_DECIDE_EM_CADA_RECURSO", EfeitoSuspensivo.UnidadeDecideEmCadaRecurso)]
    public void EfeitoSuspensivoDeclarado_Cria(string codigo, EfeitoSuspensivo esperado)
    {
        Result<RecursoDaEtapa> resultado = Criar(ParametrosDoRecursoDeTeste.Declarados(efeitoSuspensivo: codigo));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Parametros.EfeitoSuspensivo.Should().Be(esperado);
    }

    [Fact(DisplayName = "Recurso da etapa sem efeito suspensivo declarado é recusado")]
    public void EfeitoSuspensivoNaoDeclarado_Recusa()
    {
        Result<RecursoDaEtapa> resultado = Criar(ParametrosDoRecursoDeTeste.Declarados(efeitoSuspensivo: null));

        resultado.Error!.Code.Should().Be("RecursoDaEtapa.EfeitoSuspensivoNaoDeclarado");
    }
}
