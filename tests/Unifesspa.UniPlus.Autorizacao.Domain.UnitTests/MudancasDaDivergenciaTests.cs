namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Os valores que a divergência da governança guarda: sempre dois valores diferentes, no tipo de cada divergência.</summary>
public sealed class MudancasDaDivergenciaTests
{
    private const string Codigo = "selecao:processos:consultar";

    private static readonly DateTimeOffset Agora = PerfisDeTeste.Instante;
    private static readonly Guid Implantacao = Guid.CreateVersion7();
    private static readonly Guid Perfil = Guid.CreateVersion7();

    [Fact(DisplayName = "A mudança de classificação recusa a classificação nova igual à anterior")]
    public void MudancaDaClassificacao_Igual_Recusa()
    {
        Action criar = () => _ = new MudancaDaClassificacao(ClassificacaoDeProtecao.Pessoal, ClassificacaoDeProtecao.Pessoal);

        criar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "A mudança de classificação guarda a anterior e a nova")]
    public void MudancaDaClassificacao_Diferente_Guarda()
    {
        MudancaDaClassificacao mudanca = new(ClassificacaoDeProtecao.Publico, ClassificacaoDeProtecao.Sensivel);

        mudanca.Anterior.Should().Be(ClassificacaoDeProtecao.Publico);
        mudanca.Nova.Should().Be(ClassificacaoDeProtecao.Sensivel);
    }

    [Theory(DisplayName = "A mudança de escopo recusa o escopo novo igual ao anterior")]
    [InlineData(true)]
    [InlineData(false)]
    public void MudancaDoEscopo_Igual_Recusa(bool deUnidade)
    {
        Action criar = () => _ = new MudancaDoEscopo(deUnidade, deUnidade);

        criar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "A mudança de escopo guarda o valor anterior e o novo")]
    public void MudancaDoEscopo_Diferente_Guarda()
    {
        MudancaDoEscopo mudanca = new(true, false);

        mudanca.DeUnidadeAnterior.Should().BeTrue();
        mudanca.DeUnidadeNovo.Should().BeFalse();
    }

    [Theory(DisplayName = "A divergência de composição recusa o tipo que não é de composição")]
    [InlineData(TipoDeDivergencia.Classificacao)]
    [InlineData(TipoDeDivergencia.Escopo)]
    public void DaComposicao_TipoDeMudanca_Recusa(TipoDeDivergencia tipo)
    {
        Action criar = () => DivergenciaDaGovernanca.DaComposicao(Implantacao, Perfil, Codigo, tipo, Agora, 0);

        criar.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory(DisplayName = "A divergência de composição não guarda valores")]
    [InlineData(TipoDeDivergencia.ComposicaoFaltante)]
    [InlineData(TipoDeDivergencia.ComposicaoExcedente)]
    public void DaComposicao_NaoGuardaValores(TipoDeDivergencia tipo)
    {
        DivergenciaDaGovernanca divergencia = DivergenciaDaGovernanca.DaComposicao(Implantacao, Perfil, Codigo, tipo, Agora, 0);

        divergencia.Tipo.Should().Be(tipo);
        divergencia.MudancaDaClassificacao.Should().BeNull();
        divergencia.MudancaDoEscopo.Should().BeNull();
    }

    [Fact(DisplayName = "A divergência de classificação fixa o tipo e guarda só o par de classificações")]
    public void DaClassificacao_FixaOTipoEGuardaOPar()
    {
        MudancaDaClassificacao mudanca = new(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Pessoal);

        DivergenciaDaGovernanca divergencia = DivergenciaDaGovernanca.DaClassificacao(Implantacao, Perfil, Codigo, mudanca, Agora, 0);

        divergencia.Tipo.Should().Be(TipoDeDivergencia.Classificacao);
        divergencia.MudancaDaClassificacao.Should().Be(mudanca);
        divergencia.MudancaDoEscopo.Should().BeNull();
    }

    [Fact(DisplayName = "A divergência de escopo fixa o tipo e guarda só o par de escopos")]
    public void DoEscopo_FixaOTipoEGuardaOPar()
    {
        MudancaDoEscopo mudanca = new(false, true);

        DivergenciaDaGovernanca divergencia = DivergenciaDaGovernanca.DoEscopo(Implantacao, Perfil, Codigo, mudanca, Agora, 0);

        divergencia.Tipo.Should().Be(TipoDeDivergencia.Escopo);
        divergencia.MudancaDoEscopo.Should().Be(mudanca);
        divergencia.MudancaDaClassificacao.Should().BeNull();
    }
}
