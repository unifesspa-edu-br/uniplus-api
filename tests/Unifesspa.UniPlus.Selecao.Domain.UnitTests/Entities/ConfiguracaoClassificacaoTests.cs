namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaConfiguracaoClassificacao;

public sealed class ConfiguracaoClassificacaoTests
{
    [Fact(DisplayName = "Criar com FORMULA_MEDIA_PONDERADA e arredondamento tem sucesso")]
    public void Criar_MediaPonderadaComArredondamento_Sucesso()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.RegraArredondamento.Should().NotBeNull();
    }

    [Fact(DisplayName = "Criar com FORMULA_MEDIA_PONDERADA sem a escala da nota final tem sucesso: o rascunho a tolera ausente")]
    public void Criar_MediaPonderada_SemEscalaDaNotaFinal_Sucesso()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.EscalaNotaFinal.Should().BeNull();
    }

    [Theory(DisplayName = "Criar com a escala da nota final menor ou igual a zero falha no campo escalaNotaFinal")]
    [InlineData(0)]
    [InlineData(-10)]
    public void Criar_EscalaDaNotaFinalNaoPositiva_Falha(int escala)
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada() with
        {
            EscalaNotaFinal = escala,
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("escalaNotaFinal");
        resultado.Errors[0].Error.Code.Should().Be("ConfiguracaoClassificacao.EscalaNotaFinalInvalida");
    }

    [Theory(DisplayName = "Criar confere a escala da nota final com o que a NotaFinal comporta: 99999,9999 passa; 100000 e a quinta casa decimal não")]
    [InlineData("99999.9999", true)]
    [InlineData("100000", false)]
    [InlineData("100.00001", false)]
    public void Criar_EscalaDaNotaFinal_ConfereOLimiteDaNotaFinal(string escala, bool valida)
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada() with
        {
            EscalaNotaFinal = decimal.Parse(escala, System.Globalization.CultureInfo.InvariantCulture),
        });

        resultado.IsSuccess.Should().Be(valida);
        if (!valida)
        {
            resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("escalaNotaFinal");
            resultado.Errors[0].Error.Code.Should().Be("ConfiguracaoClassificacao.EscalaNotaFinalInvalida");
            resultado.Errors[0].Error.Message.Should().Contain("5 dígitos inteiros e 4 casas decimais");
        }
    }

    [Fact(DisplayName = "Criar com a escala da nota final decimal positiva congela o valor")]
    public void Criar_EscalaDaNotaFinalPositiva_CongelaOValor()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada() with
        {
            EscalaNotaFinal = 1000m,
        });

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.EscalaNotaFinal.Should().Be(1000m);
    }

    [Fact(DisplayName = "Criar com CLASSIFICACAO_IMPORTADA e a escala da nota final informada falha")]
    public void Criar_Importada_ComEscalaDaNotaFinal_Falha()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoImportada() with
        {
            EscalaNotaFinal = 100m,
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("escalaNotaFinal");
        resultado.Errors[0].Error.Code.Should().Be("ConfiguracaoClassificacao.EscalaNotaFinalIndevida");
    }

    [Fact(DisplayName = "Criar com CLASSIFICACAO_IMPORTADA e sem arredondamento tem sucesso")]
    public void Criar_Importada_SemArredondamento_Sucesso()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoImportada());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.RegraArredondamento.Should().BeNull();
    }

    [Fact(DisplayName = "Criar com CLASSIFICACAO_IMPORTADA e arredondamento informado falha")]
    public void Criar_Importada_ComArredondamento_Falha()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = RegraCalculoImportada(),
            RegraArredondamento = RegraArredondamento(),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = RegraOrdemAlocacao(),
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.ArredondamentoIndevido");
    }

    [Fact(DisplayName = "Criar com FORMULA_MEDIA_PONDERADA sem arredondamento falha — acumula os dois campos")]
    public void Criar_MediaPonderada_SemArredondamento_Falha()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = RegraCalculoMediaPonderada(),
            RegraOrdemAlocacao = RegraOrdemAlocacao(),
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "ConfiguracaoClassificacao.ArredondamentoObrigatorio",
            "ConfiguracaoClassificacao.CasasArredondamentoObrigatorio",
        ]);
    }

    [Fact(DisplayName = "ADR-0125: regra de arredondamento válida com casas inválidas recusa só CasasArredondamentoObrigatorio (achado de revisão)")]
    public void Criar_MediaPonderada_ComRegraArredondamentoECasasInvalidas_RecusaSoCasas()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = RegraCalculoMediaPonderada(),
            RegraArredondamento = RegraArredondamento(),
            CasasArredondamento = 0,
            RegraOrdemAlocacao = RegraOrdemAlocacao(),
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("casasArredondamento");
        resultado.Errors[0].Error.Code.Should().Be("ConfiguracaoClassificacao.CasasArredondamentoObrigatorio");
    }

    [Fact(DisplayName = "Criar com CLASSIFICACAO_IMPORTADA e regra de eliminação informada falha")]
    public void Criar_Importada_ComEliminacao_Falha()
    {
        RegraEliminacao eliminacao = Eliminacao(RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem.Codigo());

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoImportada() with
        {
            RegrasEliminacao = [eliminacao],
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.EliminacaoIndevida");
    }

    [Fact(DisplayName = "Criar com CLASSIFICACAO_IMPORTADA, regra ENEM e BaseadoEmEnem=true ainda falha por EliminacaoIndevida (precedência)")]
    public void Criar_Importada_ComEliminacaoEnemEBaseadoEmEnem_FalhaPorEliminacaoIndevida()
    {
        // EliminacaoIndevida (importada não aceita NENHUMA eliminação) precede o
        // gate ENEM novo — mesmo com BaseadoEmEnem=true, uma classificação importada com
        // ELIMINACAO-CORTE-REDACAO é recusada pelo motivo da eliminação indevida, não pelo motivo ENEM.
        RegraEliminacao eliminacao = Eliminacao(RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem.Codigo());

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoImportada() with
        {
            RegrasEliminacao = [eliminacao],
            BaseadoEmEnem = true,
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoClassificacao.EliminacaoIndevida");
    }

    [Fact(DisplayName = "Criar com lista de eliminação vincula os filhos à configuração")]
    public void Criar_ComEliminacao_Vincula()
    {
        RegraEliminacao eliminacao = Eliminacao(RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem.Codigo());

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoEnemLocal() with
        {
            RegrasEliminacao = [eliminacao],
        });

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.RegrasEliminacao.Should().ContainSingle();
        resultado.Value.RegrasEliminacao.Single().ConfiguracaoClassificacaoId.Should().Be(resultado.Value.Id);
    }

    // ── BaseadoEmEnem: a invariante que fecha as duas ramificações por TipoProcesso ──

    [Fact(DisplayName = "Criar seta BaseadoEmEnem no valor informado")]
    public void Criar_SetaBaseadoEmEnem()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoEnemLocal());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.BaseadoEmEnem.Should().BeTrue();
    }

    [Theory(DisplayName = "Criar com eliminação que depende do ENEM e BaseadoEmEnem=false é recusado — independente de TipoProcesso")]
    [InlineData(RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem)]
    [InlineData(RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem)]
    [InlineData(RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem)]
    public void Criar_EliminacaoEnemForaDeProcessoEnem_Recusa(RegraDeEliminacaoPorCalculo codigoRegra)
    {
        RegraEliminacao eliminacao = Eliminacao(codigoRegra.Codigo());

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada() with
        {
            RegrasEliminacao = [eliminacao],
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem");
    }

    [Theory(DisplayName = "Criar com eliminação que depende do ENEM e BaseadoEmEnem=true tem sucesso — independente de TipoProcesso")]
    [InlineData(RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem)]
    [InlineData(RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem)]
    [InlineData(RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem)]
    public void Criar_EliminacaoEnemEmProcessoEnem_Sucesso(RegraDeEliminacaoPorCalculo codigoRegra)
    {
        RegraEliminacao eliminacao = Eliminacao(codigoRegra.Codigo());

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoEnemLocal() with
        {
            RegrasEliminacao = [eliminacao],
        });

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "ADR-0125: ArredondamentoObrigatorio e CasasArredondamentoObrigatorio acumulam no mesmo lote")]
    public void Criar_SemArredondamento_AcumulaAsDuasViolacoes()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = RegraCalculoMediaPonderada(),
            RegraOrdemAlocacao = RegraOrdemAlocacao(),
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "ConfiguracaoClassificacao.ArredondamentoObrigatorio",
            "ConfiguracaoClassificacao.CasasArredondamentoObrigatorio",
        ]);
    }

    [Fact(DisplayName = "ADR-0125: ArredondamentoIndevido, CasasArredondamentoIndevido, EliminacaoIndevida e EliminacaoEnemForaDeProcessoEnem acumulam no mesmo lote")]
    public void Criar_ImportadaComArredondamentoEEliminacaoEnemSemBaseadoEmEnem_AcumulaAsQuatroViolacoes()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoImportada() with
        {
            RegraArredondamento = RegraArredondamento(),
            CasasArredondamento = 2,
            RegrasEliminacao = [Eliminacao(RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem.Codigo())],
        });

        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "ConfiguracaoClassificacao.ArredondamentoIndevido",
            "ConfiguracaoClassificacao.CasasArredondamentoIndevido",
            "ConfiguracaoClassificacao.EliminacaoIndevida",
            "ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem",
        ]);
    }

    [Fact(DisplayName = "ADR-0125: regra de arredondamento importada com só casas informadas recusa só CasasArredondamentoIndevido (achado de revisão)")]
    public void Criar_ImportadaComSoCasasInformadas_RecusaSoCasas()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = RegraCalculoImportada(),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = RegraOrdemAlocacao(),
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("casasArredondamento");
        resultado.Errors[0].Error.Code.Should().Be("ConfiguracaoClassificacao.CasasArredondamentoIndevido");
    }
}
