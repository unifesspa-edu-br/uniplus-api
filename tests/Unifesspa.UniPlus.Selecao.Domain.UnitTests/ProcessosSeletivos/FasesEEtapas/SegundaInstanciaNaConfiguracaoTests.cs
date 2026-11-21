namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.ApoioDaSegundaInstancia;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.CenarioDeProcesso;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.RecursosParaQuemJulga;

/// <summary>
/// A banca de 2ª instância que o recurso que admite instância superior exige: na fase, a própria fase a prevê; na etapa, ela ou a
/// fase em que acontece. A banca dedicada à 1ª instância não supre a falta, e a parte que a leitura do documento recusou deixa o
/// item do painel pendente em vez de afirmar a falta.
/// </summary>
public sealed class SegundaInstanciaNaConfiguracaoTests
{
    private const string CodigoDaFaltaNaFase = "Banca.FaseSemBancaDaSegundaInstancia";
    private const string CodigoDaFaltaNaEtapa = "Banca.EtapaSemBancaDaSegundaInstancia";

    private const string DetalheDaFaltaNaFase =
        "A fase 'AVALIACAO' admite recurso em 2ª instância, e nenhuma banca prevista nela tem o papel 'Julga o recurso em 2ª instância'. Acrescente à fase uma banca com esse papel, ou declare que o recurso não admite instância superior.";

    private const string DetalheDaFaltaNaSegundaEtapa =
        "A etapa na posição 2 admite recurso em 2ª instância, e nem ela nem a fase 'AVALIACAO', em que ela acontece, têm banca com o papel 'Julga o recurso em 2ª instância'. Acrescente uma banca com esse papel à etapa ou à fase, ou declare que o recurso não admite instância superior.";

    private static void DeveFicarPendenteSemCausa(Cenario cenario)
    {
        cenario.Faltas.Causas.Should().BeEmpty();
        cenario.Faltas.Indeterminado.Should().BeTrue();
        cenario.Causas.Should().BeEmpty();
        cenario.ItemEstaVerde(ItemDaFalta).Should().BeFalse();
    }

    private static void DeveEstarAtendido(Cenario cenario)
    {
        cenario.Causas.Should().BeEmpty();
        cenario.Faltas.Indeterminado.Should().BeFalse();
        cenario.ItemEstaVerde(ItemDaFalta).Should().BeTrue();
    }

    private static void DeveRecusarAFaltaDaFase(Cenario cenario)
    {
        FieldError recusa = cenario.Causas.Should().ContainSingle().Subject;

        recusa.Field.Should().Be("fases[0].bancasRequeridas");
        recusa.Error.Code.Should().Be(CodigoDaFaltaNaFase);
        recusa.Error.Message.Should().Be(DetalheDaFaltaNaFase);
        recusa.Identificadores.Should().Equal(new IdentificadorDoItem("fases", Fase));
        cenario.ItemEstaVerde(ItemDaFalta).Should().BeFalse();
    }

    [Fact(DisplayName = "O recurso da fase que admite instância superior, sem banca de 2ª instância na fase, é recusado no campo das bancas da fase")]
    public void Fase_AdmiteInstanciaSuperiorSemBanca_Recusa() =>
        DeveRecusarAFaltaDaFase(Montar([FaseQueAdmite()], []));

    [Fact(DisplayName = "A banca dedicada à 1ª instância não supre a falta da banca de 2ª instância na fase")]
    public void Fase_ComSoABancaDeRecursosDaPrimeiraInstancia_Recusa() =>
        DeveRecusarAFaltaDaFase(Montar([FaseQueAdmite(BancasDeTeste.DeRecursosDaFase())], []));

    [Fact(DisplayName = "A banca de 2ª instância só numa etapa da fase não supre a falta da fase")]
    public void Fase_ComABancaDeSegundaInstanciaSoNaEtapa_Recusa() =>
        DeveRecusarAFaltaDaFase(Montar([FaseQueAdmite()], [EtapaQueAdmite(DaSegundaInstanciaNaEtapa())]));

    [Fact(DisplayName = "A fase com banca de 2ª instância, que admite ou não instância superior, não tem falta")]
    public void Fase_ComBancaDeSegundaInstancia_SemFalta()
    {
        DeveEstarAtendido(Montar([FaseQueAdmite(DaSegundaInstanciaNaFase())], []));
        DeveEstarAtendido(Montar([FaseQueNaoAdmite()], []));
    }

    [Fact(DisplayName = "A parte recusada nas bancas da fase deixa a falta sem julgar: a banca pode estar entre as recusadas")]
    public void Fase_ComBancasRecusadasNaLeitura_FicaPendenteSemCausa() =>
        DeveFicarPendenteSemCausa(Montar([FaseQueAdmite()], [], "fases[0].bancasRequeridas"));

    [Fact(DisplayName = "A parte recusada na regra de recurso da fase deixa a falta sem julgar: o recurso pode admitir instância superior")]
    public void Fase_ComARegraDeRecursoRecusadaNaLeitura_FicaPendenteSemCausa() =>
        DeveFicarPendenteSemCausa(Montar([FaseSemRecurso()], [], $"fases[0].{FaseDeclarada.CampoDaRegraDeRecurso}"));

    [Fact(DisplayName = "A fase recusada inteira na leitura deixa a falta pendente, sem causa")]
    public void Fase_RecusadaInteiraNaLeitura_FicaPendenteSemCausa() =>
        DeveFicarPendenteSemCausa(Montar([], [], "fases"));

    [Fact(DisplayName = "O recurso da fase que a instância superior não admitida já recusa só tem essa recusa, e a falta fica pendente")]
    public void Fase_ComRecursoRecusadoPelaInstanciaSuperiorNaoAdmitida_SoARecusaDoTipo()
    {
        Cenario cenario = Montar([FaseQueAdmiteSobreItemQueSeEncerra()], [EtapaComAvaliadoraQueEncerra()]);

        DeveFicarPendenteSemCausa(cenario);
        QuemJulgaORecurso.InstanciaSuperiorNaoAdmitida(cenario.Cronograma, cenario.Etapas).Should().ContainSingle()
            .Which.Error.Code.Should().Be("Banca.TipoNaoAdmiteInstanciaSuperior");
    }

    [Fact(DisplayName = "O recurso da etapa na posição 2 que admite instância superior, sem banca de 2ª instância na etapa nem na fase, é recusado no campo das bancas da etapa")]
    public void Etapa_AdmiteInstanciaSuperiorSemBancaNemNaFase_Recusa()
    {
        Cenario cenario = Montar([FaseSemRecurso()], [EtapaQueNaoAdmite(), EtapaQueAdmite()]);

        FieldError recusa = cenario.Causas.Should().ContainSingle().Subject;

        recusa.Field.Should().Be("etapas[1].bancas");
        recusa.Error.Code.Should().Be(CodigoDaFaltaNaEtapa);
        recusa.Error.Message.Should().Be(DetalheDaFaltaNaSegundaEtapa);
        recusa.Identificadores.Should().Equal(new IdentificadorDoItem("etapas", cenario.IdentificadorDaEtapa(1)));
        cenario.ItemEstaVerde(ItemDaFalta).Should().BeFalse();
    }

    [Fact(DisplayName = "As bancas dedicadas à 1ª instância, na etapa e na fase, não suprem a falta da banca de 2ª instância da etapa")]
    public void Etapa_ComAsDedicadasDaPrimeiraInstanciaSemASegunda_Recusa()
    {
        Cenario cenario = Montar([FaseSemRecurso(BancasDeTeste.DeRecursosDaFase())], [EtapaQueAdmite(BancasDeTeste.DeRecursosDaEtapa())]);

        FieldError recusa = cenario.Causas.Should().ContainSingle().Subject;

        recusa.Field.Should().Be("etapas[0].bancas");
        recusa.Error.Code.Should().Be(CodigoDaFaltaNaEtapa);
    }

    [Fact(DisplayName = "A etapa que tem banca de 2ª instância, própria ou da fase, não tem falta")]
    public void Etapa_ComBancaDeSegundaInstanciaNaEtapaOuNaFase_SemFalta()
    {
        DeveEstarAtendido(Montar([FaseSemRecurso(DaSegundaInstanciaNaFase())], [EtapaQueAdmite()]));
        DeveEstarAtendido(Montar([FaseSemRecurso()], [EtapaQueAdmite(DaSegundaInstanciaNaEtapa())]));
        DeveEstarAtendido(Montar([FaseSemRecurso()], [EtapaQueNaoAdmite()]));
    }

    [Theory(DisplayName = "A parte que a leitura recusou deixa a falta da etapa pendente, sem causa")]
    [InlineData("etapas[0].bancas")]
    [InlineData("fases[0].bancasRequeridas")]
    [InlineData("fases")]
    public void Etapa_ComParteRecusadaNaLeitura_FicaPendenteSemCausa(string parteRecusada) =>
        DeveFicarPendenteSemCausa(Montar(parteRecusada == "fases" ? [] : [FaseSemRecurso()], [EtapaQueAdmite()], parteRecusada));

    [Fact(DisplayName = "A lista de recursos da etapa recusada deixa a falta pendente: algum recurso pode admitir instância superior")]
    public void Etapa_ComRecursosRecusadosNaLeitura_FicaPendenteSemCausa() =>
        DeveFicarPendenteSemCausa(Montar([FaseSemRecurso()], [EtapaSemRecurso()], "etapas[0].recursos"));

    [Fact(DisplayName = "O recurso da etapa que a instância superior não admitida já recusa só tem essa recusa, e a falta fica pendente")]
    public void Etapa_ComRecursoRecusadoPelaInstanciaSuperiorNaoAdmitida_SoARecusaDoTipo()
    {
        Cenario cenario = Montar([FaseSemRecurso()], [EtapaQueAdmiteSobreItemQueSeEncerra()]);

        DeveFicarPendenteSemCausa(cenario);
        QuemJulgaORecurso.InstanciaSuperiorNaoAdmitida(cenario.Cronograma, cenario.Etapas).Should().ContainSingle()
            .Which.Error.Code.Should().Be("Banca.TipoNaoAdmiteInstanciaSuperior");
    }

    [Fact(DisplayName = "A banca é de 2ª instância pelo papel da cópia do tipo, e não pelo código do tipo")]
    public void BancaDeSegundaInstancia_EReconhecidaPeloPapelDaCopia_NaoPeloCodigo()
    {
        TipoDeBancaCopiado codigoDeRecursosComPapelDeSegundaInstancia =
            TiposDeBancaCopiadosDeTeste.Com(TipoDeBanca.AnaliseDeRecursos, PapelDaBanca.JulgaASegundaInstancia);
        TipoDeBancaCopiado codigoDeSegundaInstanciaComPapelDePrimeira =
            TiposDeBancaCopiadosDeTeste.Com(TipoDeBanca.AnaliseDeRecursosDeSegundaInstancia, PapelDaBanca.JulgaAPrimeiraInstancia);

        DeveEstarAtendido(Montar([FaseQueAdmite(NaFase(codigoDeRecursosComPapelDeSegundaInstancia))], []));
        DeveRecusarAFaltaDaFase(Montar([FaseQueAdmite(NaFase(codigoDeSegundaInstanciaComPapelDePrimeira))], []));
    }
}
