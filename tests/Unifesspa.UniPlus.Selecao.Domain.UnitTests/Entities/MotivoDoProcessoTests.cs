namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

/// <summary>A vigência da escolha de um motivo no edital: de que versão entra e até que versão vale.</summary>
public sealed class MotivoDoProcessoTests
{
    private static MotivoDoProcesso Escolha(int versaoDeEntrada, int? versaoDeSaida = null)
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDoProcesso escolha = ProcessoParaMotivos.Escolha(
            processo,
            ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO"),
            ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.Classificacao, "NOTA_ZERADA", SentidoDoMotivo.Desfavoravel),
            versaoDeEntrada);
        if (versaoDeSaida is { } saida)
        {
            ConstructorInfo construtor = typeof(VigenciaNoEdital).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single(c => c.GetParameters().Length == 2);
            typeof(MotivoDoProcesso).GetProperty(nameof(MotivoDoProcesso.Vigencia))!.SetValue(escolha, construtor.Invoke([versaoDeEntrada, saida]));
        }

        return escolha;
    }

    [Theory(DisplayName = "A escolha é pendente na versão em que entra e nas anteriores a ela, e deixa de ser na seguinte")]
    [InlineData(2, 2, true)]
    [InlineData(2, 1, true)]
    [InlineData(2, 3, false)]
    [InlineData(1, 1, true)]
    public void PendenteNa_Bordas(int entrada, int versaoEmPreparo, bool pendente) =>
        Escolha(entrada).PendenteNa(versaoEmPreparo).Should().Be(pendente);

    [Fact(DisplayName = "No rascunho, com a versão 1 em preparo, a escolha que entra na versão 1 é pendente")]
    public void PendenteNa_NoRascunho() =>
        Escolha(VersaoConfiguracao.NumeroDaAbertura).PendenteNa(VersaoConfiguracao.NumeroDaAbertura).Should().BeTrue();

    [Theory(DisplayName = "A escolha sem saída vale da versão de entrada em diante")]
    [InlineData(3, 2, false)]
    [InlineData(3, 3, true)]
    [InlineData(3, 4, true)]
    public void ValeNaVersao_SemSaida(int entrada, int versao, bool vale) =>
        MotivoDoProcesso.ValeNaVersao(versao).Compile()(Escolha(entrada)).Should().Be(vale);

    [Theory(DisplayName = "A escolha com saída vale da entrada até a versão anterior à saída")]
    [InlineData(2, 4, 1, false)]
    [InlineData(2, 4, 2, true)]
    [InlineData(2, 4, 3, true)]
    [InlineData(2, 4, 4, false)]
    [InlineData(2, 4, 5, false)]
    public void ValeNaVersao_ComSaida(int entrada, int saida, int versao, bool vale) =>
        MotivoDoProcesso.ValeNaVersao(versao).Compile()(Escolha(entrada, saida)).Should().Be(vale);

    [Fact(DisplayName = "A escolha atual é a que ainda não saiu do edital")]
    public void NaEscolhaAtual_SoSemSaida()
    {
        Func<MotivoDoProcesso, bool> naEscolhaAtual = MotivoDoProcesso.NaEscolhaAtual.Compile();

        naEscolhaAtual(Escolha(1)).Should().BeTrue();
        naEscolhaAtual(Escolha(1, 2)).Should().BeFalse();
    }

    [Fact(DisplayName = "A vigência não nasce com a versão de entrada anterior à primeira")]
    public void Vigencia_EntradaAbaixoDaPrimeira_Recusa()
    {
        FluentActions.Invoking(() => Escolha(0)).Should().Throw<ArgumentOutOfRangeException>();
    }
}
