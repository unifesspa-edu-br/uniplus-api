namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class HipoteseLegalTratamentoTests
{
    private static readonly (HipoteseLegalTratamento Valor, string Codigo, string Rotulo)[] Esperados =
    [
            (HipoteseLegalTratamento.Consentimento, "CONSENTIMENTO", "Consentimento"),
            (HipoteseLegalTratamento.CumprimentoObrigacaoLegal, "CUMPRIMENTO_OBRIGACAO_LEGAL", "Cumprimento de obrigação legal"),
            (HipoteseLegalTratamento.ExecucaoPoliticasPublicas, "EXECUCAO_POLITICAS_PUBLICAS", "Execução de políticas públicas"),
            (HipoteseLegalTratamento.EstudosPorOrgaoDePesquisa, "ESTUDOS_POR_ORGAO_DE_PESQUISA", "Estudos por órgão de pesquisa"),
            (HipoteseLegalTratamento.ExecucaoContrato, "EXECUCAO_CONTRATO", "Execução de contrato"),
            (HipoteseLegalTratamento.ExercicioRegularDeDireitos, "EXERCICIO_REGULAR_DE_DIREITOS", "Exercício regular de direitos"),
            (HipoteseLegalTratamento.ProtecaoDaVida, "PROTECAO_DA_VIDA", "Proteção da vida"),
            (HipoteseLegalTratamento.TutelaDaSaude, "TUTELA_DA_SAUDE", "Tutela da saúde"),
            (HipoteseLegalTratamento.InteresseLegitimo, "INTERESSE_LEGITIMO", "Interesse legítimo"),
            (HipoteseLegalTratamento.ProtecaoDoCredito, "PROTECAO_DO_CREDITO", "Proteção do crédito"),
            (HipoteseLegalTratamento.PrevencaoAFraude, "PREVENCAO_A_FRAUDE", "Prevenção à fraude"),
    ];

    [Fact]
    public void Valores_CodigosERotulos_SeguemOContrato()
    {
        CatalogoDe<HipoteseLegalTratamento>.Valores().Should().Equal(Esperados.Select(static item => item.Valor));
        foreach ((HipoteseLegalTratamento valor, string codigo, string rotulo) in Esperados)
        {
            valor.Codigo().Should().Be(codigo);
            valor.Rotulo().Should().Be(rotulo);
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
