namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Reconciliado = Unifesspa.UniPlus.Selecao.Domain.Services.ReconciliadorQuadroInstitucional.QuadroReconciliado;

public sealed class ReconciliadorQuadroInstitucionalTests
{
    [Fact(DisplayName = "REDUZIR_DE tira o excesso inteiro da modalidade nomeada")]
    public void ReduzirDe_TiraODaModalidadeNomeada()
    {
        Result<Reconciliado> resultado = ReconciliadorQuadroInstitucional.Reduzir(
            ["AC", "AC_PCD"], [38, 4], excesso: 2, new ParametrosReduzirDe("AC_PCD"));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Quantidades.Should().Equal([38, 2]);
        resultado.Value.Reduzido.Should().Be(2);
    }

    [Fact(DisplayName = "REDUZIR_DE recusa quando a modalidade não comporta o excesso")]
    public void ReduzirDe_SemSaldo_Recusa()
    {
        Result<Reconciliado> resultado = ReconciliadorQuadroInstitucional.Reduzir(
            ["AC", "AC_PCD"], [38, 1], excesso: 5, new ParametrosReduzirDe("AC_PCD"));

        resultado.IsFailure.Should().BeTrue(
            "reduzir o que se pode e devolver um quadro ainda estourado entregaria como reconciliado o que não fecha");
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.AjusteNaoAbsorveOExcesso");
        resultado.Error.Message.Should().NotContain("AC_PCD");
    }

    [Fact(DisplayName = "Ajuste que nomeia modalidade fora do quadro é recusado")]
    public void Reduzir_ModalidadeForaDoQuadro_Recusa()
    {
        Result<Reconciliado> resultado = ReconciliadorQuadroInstitucional.Reduzir(
            ["AC"], [42], excesso: 2, new ParametrosReduzirDe("LB_PPI"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.AjusteReferenciaModalidadeForaDoQuadro");
        resultado.Error.Message.Should().NotContain("LB_PPI");
    }

    [Fact(DisplayName = "REDUZIR_PROPORCIONAL_EM reparte o excesso na proporção do declarado")]
    public void ReduzirProporcional_ReparteNaProporcao()
    {
        Result<Reconciliado> resultado = ReconciliadorQuadroInstitucional.Reduzir(
            ["AC", "AC_I", "AC_Q"], [10, 30, 60], excesso: 10,
            new ParametrosReduzirProporcionalEm(["AC_I", "AC_Q"]));

        resultado.IsSuccess.Should().BeTrue();

        // 30 e 60 cedem na razão de 1 para 2: a parte inteira tira 3 e 6, e o maior resto
        // manda a décima para AC_Q. AC não é alvo do ajuste e fica intacta.
        resultado.Value!.Quantidades.Should().Equal([10, 27, 53]);
        resultado.Value.Reduzido.Should().Be(10);
    }

    [Fact(DisplayName = "REDUZIR_PROPORCIONAL_EM fecha exato mesmo quando a proporção não é inteira")]
    public void ReduzirProporcional_ComRestos_FechaExato()
    {
        // 1/3 de 7 não é inteiro em nenhuma das três: a parte inteira retira 6, e o maior
        // resto decide quem cede a sétima. Sem isso o quadro fecharia com uma vaga a mais.
        Result<Reconciliado> resultado = ReconciliadorQuadroInstitucional.Reduzir(
            ["A", "B", "C"], [10, 10, 10], excesso: 7,
            new ParametrosReduzirProporcionalEm(["A", "B", "C"]));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Quantidades.Sum().Should().Be(23, "30 declaradas menos as 7 do excesso");
        resultado.Value.Reduzido.Should().Be(7);
        resultado.Value.Quantidades.Should().OnlyContain(q => q >= 0);
    }

    [Fact(DisplayName = "REDUZIR_PROPORCIONAL_EM recusa quando o conjunto não comporta o excesso")]
    public void ReduzirProporcional_SemSaldo_Recusa()
    {
        Result<Reconciliado> resultado = ReconciliadorQuadroInstitucional.Reduzir(
            ["AC", "AC_I"], [40, 3], excesso: 5, new ParametrosReduzirProporcionalEm(["AC_I"]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.AjusteNaoAbsorveOExcesso");
        resultado.Error.Message.Should().NotContain("AC_I");
    }

    [Fact(DisplayName = "A recusa do excesso não absorvido por uma modalidade não repete o código nem as quantidades enviados")]
    public void ReduzirDe_SemSaldo_MensagemSemValorRecebido()
    {
        Result<Reconciliado> resultado = ReconciliadorQuadroInstitucional.Reduzir(
            ["AC", "MODALIDADE_FICTICIA_ZETA"], [38, 7137], excesso: 90210, new ParametrosReduzirDe("MODALIDADE_FICTICIA_ZETA"));

        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.AjusteNaoAbsorveOExcesso");
        resultado.Error.Message.Should().Be("A modalidade do ajuste não tem vagas suficientes para absorver o excesso.");
    }

    [Fact(DisplayName = "A recusa do excesso não absorvido pelo conjunto não repete as quantidades enviadas")]
    public void ReduzirProporcional_SemSaldo_MensagemSemValorRecebido()
    {
        Result<Reconciliado> resultado = ReconciliadorQuadroInstitucional.Reduzir(
            ["AC", "MODALIDADE_FICTICIA_ZETA"], [40, 7137], excesso: 90210, new ParametrosReduzirProporcionalEm(["MODALIDADE_FICTICIA_ZETA"]));

        resultado.Error!.Code.Should().Be("ConfiguracaoDistribuicaoVagas.AjusteNaoAbsorveOExcesso");
        resultado.Error.Message.Should().Be("As modalidades do ajuste somam menos vagas do que o excesso a absorver.");
    }

    [Fact(DisplayName = "A recusa de modalidade fora do quadro não repete o código enviado, em REDUZIR_DE e em REDUZIR_PROPORCIONAL_EM")]
    public void ModalidadeForaDoQuadro_MensagemSemOCodigoEnviado()
    {
        Result<Reconciliado> deUma = ReconciliadorQuadroInstitucional.Reduzir(
            ["AC"], [42], excesso: 2, new ParametrosReduzirDe("MODALIDADE_FICTICIA_ZETA"));
        Result<Reconciliado> proporcional = ReconciliadorQuadroInstitucional.Reduzir(
            ["AC"], [42], excesso: 2, new ParametrosReduzirProporcionalEm(["AC", "MODALIDADE_FICTICIA_ZETA"]));

        new[] { deUma, proporcional }.Select(static r => (r.Error!.Code, r.Error.Message)).Should().OnlyContain(static erro =>
            erro.Code == "ConfiguracaoDistribuicaoVagas.AjusteReferenciaModalidadeForaDoQuadro"
            && erro.Message == "O ajuste reduz de modalidade que não está entre as modalidades selecionadas.");
    }

    [Fact(DisplayName = "Sem excesso o quadro atravessa intacto")]
    public void Reduzir_SemExcesso_NaoAltera()
    {
        Result<Reconciliado> resultado = ReconciliadorQuadroInstitucional.Reduzir(
            ["AC", "AC_PCD"], [38, 2], excesso: 0, new ParametrosReduzirDe("AC_PCD"));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Quantidades.Should().Equal([38, 2]);
        resultado.Value.Reduzido.Should().Be(0);
    }
}
