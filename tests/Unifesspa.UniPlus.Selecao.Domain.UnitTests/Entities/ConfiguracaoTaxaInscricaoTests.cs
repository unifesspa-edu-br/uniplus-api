namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class ConfiguracaoTaxaInscricaoTests
{
    [Fact(DisplayName = "Criar com cobra=true, valor positivo e um fundamento tem sucesso (CA-02)")]
    public void Criar_CobraComValorPositivo_Sucesso()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: true, valor: 150.00m, fundamentosCodigos: [FundamentoIsencaoCodigo.CadastroUnico], confirmacaoFundamentos: true);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Cobra.Should().BeTrue();
        resultado.Value.Valor.Should().Be(150.00m);
        resultado.Value.Fundamentos.Should().Equal(FundamentoIsencao.CadastroUnico);
    }

    [Theory(DisplayName = "Criar com cobra=true e valor ausente ou não positivo falha (CA-02)")]
    [InlineData(null)]
    [InlineData(0d)]
    [InlineData(-10d)]
    public void Criar_CobraComValorInvalido_Falha(double? valor)
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: true, valor: (decimal?)valor, fundamentosCodigos: [FundamentoIsencaoCodigo.CadastroUnico], confirmacaoFundamentos: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoTaxaInscricao.ValorObrigatorioQuandoCobra");
        resultado.Errors.Should().ContainSingle("o fundamento está declarado — só o valor viola");
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

    [Theory(DisplayName = "Criar com cobra=true e zero fundamentos falha — quem cobra reconhece ao menos um fundamento (issue #1310)")]
    [InlineData(true)]
    [InlineData(false)]
    public void Criar_CobraSemFundamentos_Falha(bool listaVazia)
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: true, valor: 100m, fundamentosCodigos: listaVazia ? [] : null, confirmacaoFundamentos: false);

        resultado.IsFailure.Should().BeTrue(
            "lista vazia e lista ausente são a mesma declaração — nenhum fundamento reconhecido");
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("fundamentos");
        resultado.Errors[0].Error.Code.Should().Be("ConfiguracaoTaxaInscricao.FundamentoObrigatorioQuandoCobra");
    }

    [Fact(DisplayName = "Criar com cobra=true, valor não positivo e zero fundamentos acumula as duas violações (ADR-0125)")]
    public void Criar_CobraSemValorESemFundamentos_AcumulaAsDuasViolacoes()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: true, valor: null, fundamentosCodigos: null, confirmacaoFundamentos: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2,
            "valor e fundamentos são famílias independentes — o cliente não pode descobrir uma de cada vez");
        resultado.Errors.Should().Contain(e => e.Field == "valor" && e.Error.Code == "ConfiguracaoTaxaInscricao.ValorObrigatorioQuandoCobra");
        resultado.Errors.Should().Contain(e => e.Field == "fundamentos" && e.Error.Code == "ConfiguracaoTaxaInscricao.FundamentoObrigatorioQuandoCobra");
    }

    [Fact(DisplayName = "Criar com cobra=true e só token desconhecido reporta apenas FundamentoDesconhecido — a obrigatoriedade não duplica a recusa (issue #1310)")]
    public void Criar_CobraComTokenDesconhecido_NaoAcumulaObrigatoriedade()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: true, valor: 100m, fundamentosCodigos: ["FUNDAMENTO_INEXISTENTE"], confirmacaoFundamentos: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(
            "quem errou o token já sabe o que corrigir — \"informe ao menos um\" não acrescenta ação");
        resultado.Errors[0].Error.Code.Should().Be("ConfiguracaoTaxaInscricao.FundamentoDesconhecido");
    }

    [Fact(DisplayName = "Criar com cobra=false e zero fundamentos continua com sucesso — a obrigatoriedade só alcança quem cobra (UNI-REQ-0100)")]
    public void Criar_NaoCobraSemFundamentos_Sucesso()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: false, valor: null, fundamentosCodigos: [], confirmacaoFundamentos: false);

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

    [Fact(DisplayName = "Criar aceita o fundamento de carência socioeconômica (issue #1296)")]
    public void Criar_ComCarenciaSocioeconomica_Sucesso()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: true,
            valor: 150.00m,
            fundamentosCodigos: [FundamentoIsencaoCodigo.CarenciaSocioeconomica],
            confirmacaoFundamentos: true);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Fundamentos.Should().Equal(FundamentoIsencao.CarenciaSocioeconomica);
    }

    [Fact(DisplayName = "Criar aceita os três fundamentos juntos (issue #1296)")]
    public void Criar_ComOsTresFundamentos_Sucesso()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = ConfiguracaoTaxaInscricao.Criar(
            cobra: true,
            valor: 150.00m,
            fundamentosCodigos: [.. FundamentoIsencaoCodigo.Codigos],
            confirmacaoFundamentos: true);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Fundamentos.Should().HaveCount(3);
    }
}
