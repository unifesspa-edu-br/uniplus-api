namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.ApoioDaSegundaInstancia;

public sealed class ExcessoDeBancasDaSegundaInstanciaTests
{
    private const string Item = "cronograma_uma_banca_da_segunda_instancia_por_fase_ou_etapa";
    private const string DetalheFase = "A fase 'AVALIACAO' prevê 3 bancas com o papel 'Julga o recurso em 2ª instância', e a 2ª instância é julgada por uma única banca na fase. Mantenha uma e retire as demais, a começar pela banca 'Banca de análise de recursos de 2ª instância 2'.";
    private const string DetalheEtapa = "A etapa na posição 1 prevê 3 bancas com o papel 'Julga o recurso em 2ª instância', e a 2ª instância é julgada por uma única banca na etapa. Mantenha uma e retire as demais, a começar pela banca 'Banca de análise de recursos de 2ª instância 2'.";

    [Fact]
    public void Fase_TresBancas_RecusaUmaVezNaSegunda()
    {
        Cenario cenario = Montar([FaseQueAdmite(DaSegundaInstanciaNaFase(), DaSegundaInstanciaNaFase(), DaSegundaInstanciaNaFase())], []);
        FieldError erro = cenario.Causas.Should().ContainSingle().Subject;
        erro.Error.Code.Should().Be("Banca.FaseComMaisDeUmaBancaDaSegundaInstancia");
        erro.Field.Should().Be("fases[0].bancasRequeridas[1]");
        erro.Error.Message.Should().Be(DetalheFase);
        erro.Identificadores.Should().Equal(new IdentificadorDoItem("fases", Fase), new IdentificadorDoItem("bancasRequeridas", cenario.IdentificadorDaBancaDaFase(0, 1)));
        cenario.ItemEstaVerde(Item).Should().BeFalse();
    }

    [Fact]
    public void Etapa_TresBancas_RecusaUmaVezNaSegunda()
    {
        Cenario cenario = Montar([FaseSemRecurso()], [EtapaQueAdmite(DaSegundaInstanciaNaEtapa(), DaSegundaInstanciaNaEtapa(), DaSegundaInstanciaNaEtapa())]);
        FieldError erro = cenario.Causas.Should().ContainSingle().Subject;
        erro.Error.Code.Should().Be("Banca.EtapaComMaisDeUmaBancaDaSegundaInstancia");
        erro.Field.Should().Be("etapas[0].bancas[1]");
        erro.Error.Message.Should().Be(DetalheEtapa);
        erro.Identificadores.Should().Equal(new IdentificadorDoItem("etapas", cenario.IdentificadorDaEtapa(0)), new IdentificadorDoItem("bancas", cenario.IdentificadorDaBancaDaEtapa(0, 1)));
        cenario.ItemEstaVerde(Item).Should().BeFalse();
    }

    [Fact]
    public void FaseEEtapa_UmaBancaEmCadaLugar_Aceitas()
    {
        Cenario cenario = Montar([FaseQueAdmite(DaSegundaInstanciaNaFase())], [EtapaQueAdmite(DaSegundaInstanciaNaEtapa())]);
        cenario.Causas.Should().BeEmpty();
        cenario.ItemEstaVerde(Item).Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Lugar_SemRecursoESobras_CortaExcesso(bool fase)
    {
        Cenario cenario = fase
            ? Montar([FaseSemRecurso(DaSegundaInstanciaNaFase(), DaSegundaInstanciaNaFase())], [])
            : Montar([FaseSemRecurso()], [EtapaSemRecurso(DaSegundaInstanciaNaEtapa(), DaSegundaInstanciaNaEtapa())]);
        cenario.Causas.Should().HaveCount(2).And.OnlyContain(erro => erro.Error.Code.Contains("SemRecursoParaJulgar", StringComparison.Ordinal));
        cenario.ItemEstaVerde(Item).Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Lugar_AdmissaoIncerta_RecusaExcesso(bool fase)
    {
        Cenario cenario = fase
            ? Montar([FaseSemRecurso(DaSegundaInstanciaNaFase(), DaSegundaInstanciaNaFase())], [], $"fases[0].{FaseDeclarada.CampoDaRegraDeRecurso}")
            : Montar([FaseSemRecurso()], [EtapaSemRecurso(DaSegundaInstanciaNaEtapa(), DaSegundaInstanciaNaEtapa())], "etapas[0].recursos");
        FieldError erro = cenario.Causas.Should().ContainSingle().Subject;
        ConferirExcessoComAdmissaoIncerta(cenario, erro, fase);
        cenario.ItemEstaVerde(Item).Should().BeFalse();
    }
    private static void ConferirExcessoComAdmissaoIncerta(Cenario cenario, FieldError erro, bool fase)
    {
        erro.Error.Code.Should().Be(fase ? "Banca.FaseComMaisDeUmaBancaDaSegundaInstancia" : "Banca.EtapaComMaisDeUmaBancaDaSegundaInstancia");
        erro.Field.Should().Be(fase ? "fases[0].bancasRequeridas[1]" : "etapas[0].bancas[1]");
        erro.Error.Message.Should().Be((fase ? DetalheFase : DetalheEtapa).Replace("prevê 3 bancas", "prevê 2 bancas", StringComparison.Ordinal));
        erro.Identificadores.Should().Equal(fase
            ? [new IdentificadorDoItem("fases", Fase), new IdentificadorDoItem("bancasRequeridas", cenario.IdentificadorDaBancaDaFase(0, 1))]
            : [new IdentificadorDoItem("etapas", cenario.IdentificadorDaEtapa(0)), new IdentificadorDoItem("bancas", cenario.IdentificadorDaBancaDaEtapa(0, 1))]);
    }

}
