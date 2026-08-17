namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class ConfiguracaoTaxaInscricaoTests
{
    [Fact(DisplayName = "Criar com cobra=true e valor positivo tem sucesso (CA-02)")]
    public void Criar_CobraComValorPositivo_Sucesso()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: true, valor: 150.00m, fundamentosCodigos: null, confirmacaoFundamentos: false);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Cobra.Should().BeTrue();
        resultado.Value.Valor.Should().Be(150.00m);
        resultado.Value.Fundamentos.Should().BeEmpty();
    }

    [Theory(DisplayName = "Criar com cobra=true e valor ausente ou não positivo falha (CA-02)")]
    [InlineData(null)]
    [InlineData(0d)]
    [InlineData(-10d)]
    public void Criar_CobraComValorInvalido_Falha(double? valor)
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: true, valor: (decimal?)valor, fundamentosCodigos: null, confirmacaoFundamentos: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoTaxaInscricao.ValorObrigatorioQuandoCobra");
    }

    [Fact(DisplayName = "Criar com cobra=false e valor ausente tem sucesso (CA-03)")]
    public void Criar_NaoCobraSemValor_Sucesso()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: false, valor: null, fundamentosCodigos: null, confirmacaoFundamentos: false);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Cobra.Should().BeFalse();
        resultado.Value.Valor.Should().BeNull();
    }

    [Fact(DisplayName = "Criar com cobra=false e valor informado falha (CA-03)")]
    public void Criar_NaoCobraComValor_Falha()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: false, valor: 10m, fundamentosCodigos: null, confirmacaoFundamentos: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoTaxaInscricao.ValorNaoPermitidoQuandoNaoCobra");
    }

    [Fact(DisplayName = "Criar com cobra=false e fundamento configurado falha — não cobrar e isentar são exclusivos (CA-03)")]
    public void Criar_NaoCobraComFundamento_Falha()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: false, valor: null, fundamentosCodigos: [FundamentoIsencaoCodigo.CadastroUnico], confirmacaoFundamentos: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoTaxaInscricao.FundamentoExigeCobranca");
    }

    [Fact(DisplayName = "Criar com cobra=true e zero fundamentos tem sucesso — isenção é opcional (CA-04)")]
    public void Criar_CobraSemFundamentos_Sucesso()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: true, valor: 100m, fundamentosCodigos: [], confirmacaoFundamentos: false);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Fundamentos.Should().BeEmpty();
        resultado.Value.ConfirmacaoFundamentos.Should().BeFalse();
    }

    [Fact(DisplayName = "Criar com token de fundamento desconhecido falha (CA-05)")]
    public void Criar_FundamentoDesconhecido_Falha()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: true, valor: 100m, fundamentosCodigos: ["FUNDAMENTO_INEXISTENTE"], confirmacaoFundamentos: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoTaxaInscricao.FundamentoDesconhecido");
    }

    [Fact(DisplayName = "Criar com fundamento e ConfirmacaoFundamentos=false falha (CA-06)")]
    public void Criar_FundamentoSemConfirmacao_Falha()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: true, valor: 100m, fundamentosCodigos: [FundamentoIsencaoCodigo.CadastroUnico], confirmacaoFundamentos: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoTaxaInscricao.ConfirmacaoFundamentosObrigatoria");
    }

    [Fact(DisplayName = "Criar com fundamento e ConfirmacaoFundamentos=true tem sucesso, sem depender do tipo de processo (CA-06)")]
    public void Criar_FundamentoComConfirmacao_Sucesso()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: true, valor: 100m,
            fundamentosCodigos: [FundamentoIsencaoCodigo.CadastroUnico, FundamentoIsencaoCodigo.DoacaoMedulaOssea],
            confirmacaoFundamentos: true);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ConfirmacaoFundamentos.Should().BeTrue();
        resultado.Value.Fundamentos.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Criar deduplica e ordena fundamentos pelo código canônico, independente da ordem/repetição de entrada")]
    public void Criar_Fundamentos_DeduplicaEOrdena()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: true, valor: 100m,
            fundamentosCodigos: [
                FundamentoIsencaoCodigo.DoacaoMedulaOssea,
                FundamentoIsencaoCodigo.CadastroUnico,
                FundamentoIsencaoCodigo.DoacaoMedulaOssea,
            ],
            confirmacaoFundamentos: true);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Fundamentos.Select(static f => f.ToCodigo()).Should().Equal(
            FundamentoIsencaoCodigo.CadastroUnico, FundamentoIsencaoCodigo.DoacaoMedulaOssea);
    }

    [Fact(DisplayName = "Criar com cobra=false, fundamentos nulo e ConfirmacaoFundamentos=true tem sucesso — a confirmação é irrelevante sem fundamento")]
    public void Criar_NaoCobraSemFundamentoComConfirmacaoIrrelevante_Sucesso()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: false, valor: null, fundamentosCodigos: null, confirmacaoFundamentos: true);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ConfirmacaoFundamentos.Should().BeFalse();
    }

    [Fact(DisplayName = "Criar com cobra=false, valor informado e fundamento configurado acumula as duas violações")]
    public void Criar_NaoCobraComValorEFundamento_AcumulaAsDuasViolacoes()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: false, valor: 10m, fundamentosCodigos: [FundamentoIsencaoCodigo.CadastroUnico], confirmacaoFundamentos: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors.Should().Contain(e => e.Field == "valor" && e.Error.Code == "ConfiguracaoTaxaInscricao.ValorNaoPermitidoQuandoNaoCobra");
        resultado.Errors.Should().Contain(e => e.Field == "fundamentos" && e.Error.Code == "ConfiguracaoTaxaInscricao.FundamentoExigeCobranca");
    }

    [Fact(DisplayName = "Criar com cobra=false, fundamento desconhecido e sem confirmação acumula as três violações — vocabulário e confirmação não dependem de cobra")]
    public void Criar_NaoCobraComFundamentoDesconhecidoSemConfirmacao_AcumulaAsTresViolacoes()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: false, valor: null, fundamentosCodigos: ["FUNDAMENTO_INEXISTENTE"], confirmacaoFundamentos: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
        resultado.Errors.Should().Contain(e => e.Field == "fundamentos" && e.Error.Code == "ConfiguracaoTaxaInscricao.FundamentoExigeCobranca");
        resultado.Errors.Should().Contain(e => e.Field == "fundamentos" && e.Error.Code == "ConfiguracaoTaxaInscricao.FundamentoDesconhecido");
        resultado.Errors.Should().Contain(e => e.Field == "confirmacaoFundamentos" && e.Error.Code == "ConfiguracaoTaxaInscricao.ConfirmacaoFundamentosObrigatoria");
    }

    [Fact(DisplayName = "Criar com fundamento desconhecido e sem confirmação acumula as duas violações — a confirmação é exigida independente do fundamento ser reconhecido")]
    public void Criar_FundamentoDesconhecidoSemConfirmacao_AcumulaAsDuasViolacoes()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: true, valor: 100m, fundamentosCodigos: ["FUNDAMENTO_INEXISTENTE"], confirmacaoFundamentos: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors.Should().Contain(e => e.Field == "fundamentos" && e.Error.Code == "ConfiguracaoTaxaInscricao.FundamentoDesconhecido");
        resultado.Errors.Should().Contain(e => e.Field == "confirmacaoFundamentos" && e.Error.Code == "ConfiguracaoTaxaInscricao.ConfirmacaoFundamentosObrigatoria");
    }
}
