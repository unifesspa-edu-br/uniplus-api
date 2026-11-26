namespace Unifesspa.UniPlus.Kernel.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class HipoteseLegalTratamentoTests
{
    private static readonly (HipoteseLegalTratamento Valor, string Codigo)[] Esperados =
    [
        (HipoteseLegalTratamento.Consentimento, "CONSENTIMENTO"),
        (HipoteseLegalTratamento.CumprimentoObrigacaoLegal, "CUMPRIMENTO_OBRIGACAO_LEGAL"),
        (HipoteseLegalTratamento.ExecucaoPoliticasPublicas, "EXECUCAO_POLITICAS_PUBLICAS"),
        (HipoteseLegalTratamento.EstudosPorOrgaoDePesquisa, "ESTUDOS_POR_ORGAO_DE_PESQUISA"),
        (HipoteseLegalTratamento.ExecucaoContrato, "EXECUCAO_CONTRATO"),
        (HipoteseLegalTratamento.ExercicioRegularDeDireitos, "EXERCICIO_REGULAR_DE_DIREITOS"),
        (HipoteseLegalTratamento.ProtecaoDaVida, "PROTECAO_DA_VIDA"),
        (HipoteseLegalTratamento.TutelaDaSaude, "TUTELA_DA_SAUDE"),
        (HipoteseLegalTratamento.InteresseLegitimo, "INTERESSE_LEGITIMO"),
        (HipoteseLegalTratamento.ProtecaoDoCredito, "PROTECAO_DO_CREDITO"),
        (HipoteseLegalTratamento.PrevencaoAFraude, "PREVENCAO_A_FRAUDE"),
    ];

    [Fact]
    public void Valores_Codigos_SeguemOContrato()
    {
        CatalogoDe<HipoteseLegalTratamento>.Valores().Should().Equal(Esperados.Select(static item => item.Valor));
        foreach ((HipoteseLegalTratamento valor, string codigo) in Esperados)
        {
            valor.Codigo().Should().Be(codigo);
            CatalogoDe<HipoteseLegalTratamento>.TentarObter(codigo, out HipoteseLegalTratamento obtido).Should().BeTrue();
            obtido.Should().Be(valor);
        }
    }

    [Theory]
    [InlineData("consentimento")]
    [InlineData("DESCONHECIDA")]
    public void TentarObter_CodigoForaDoContrato_Recusa(string codigo) =>
        CatalogoDe<HipoteseLegalTratamento>.TentarObter(codigo, out _).Should().BeFalse();
}
