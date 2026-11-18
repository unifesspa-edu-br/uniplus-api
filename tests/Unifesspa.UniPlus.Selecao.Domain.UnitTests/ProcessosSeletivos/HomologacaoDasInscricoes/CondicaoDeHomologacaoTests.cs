namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes.CondicoesDeHomologacaoParaTestes;

/// <summary>Uma condição da regra: o que se apura da inscrição, de que edição do exame, e o motivo desfavorável que ela leva quando não é cumprida.</summary>
public sealed class CondicaoDeHomologacaoTests
{
    [Fact(DisplayName = "A condição do resultado no retorno do INEP guarda a edição do exame e o motivo desfavorável")]
    public void Declarar_ResultadoNoRetornoDoInep_GuardaEdicaoEMotivo()
    {
        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.Declarar(
            CodigoDoRetornoDoInep, edicaoDoExame: 2026, "SEM_RESULTADO_INEP", enquadramento: null);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Tipo.Should().Be(TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep);
        resultado.Value.EdicaoDoExame.Should().Be(2026);
        resultado.Value.MotivoDesfavoravelCodigo.Should().Be("SEM_RESULTADO_INEP");
        resultado.Value.Enquadramento.Should().BeNull();
    }

    [Fact(DisplayName = "O enquadramento opcional é um predicado em forma normal disjuntiva, mantido como veio")]
    public void Declarar_ComEnquadramento_GuardaOPredicado()
    {
        PredicadoDnf enquadramento = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement("AMPLA")).Value!)]).Value!;

        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.Declarar(CodigoDoRetornoDoInep, 2026, "SEM_RESULTADO_INEP", enquadramento);

        resultado.Value!.Enquadramento.Should().BeSameAs(enquadramento);
    }

    [Fact(DisplayName = "O enquadramento sem cláusula é recusado como campo inválido")]
    public void Declarar_EnquadramentoSemClausula_RecusaOCampo()
    {
        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.Declarar(CodigoDoRetornoDoInep, 2026, "SEM_RESULTADO_INEP", PredicadoDnf.SemClausula);

        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code)).Should().Equal(("enquadramento", "PredicadoDnf.FormaJsonInvalida"));
    }

    [Theory(DisplayName = "As edições nos limites do intervalo plausível são aceitas")]
    [InlineData(1998)]
    [InlineData(2100)]
    public void Declarar_EdicaoNosLimites_Aceita(int edicao)
    {
        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.Declarar(CodigoDoRetornoDoInep, edicao, "SEM_RESULTADO_INEP", enquadramento: null);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Theory(DisplayName = "Condição sem edição do exame, ou com edição fora de 1998 a 2100, é recusada no campo da edição")]
    [InlineData(2101)]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-2026)]
    [InlineData(1997)]
    [InlineData(10000)]
    public void Declarar_EdicaoDoExameInvalida_RecusaNoCampoDaEdicao(int? edicao)
    {
        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.Declarar(CodigoDoRetornoDoInep, edicao, "SEM_RESULTADO_INEP", enquadramento: null);

        RecusasPorCampo(resultado).Should().Equal(("edicaoDoExame", HomologacaoDasInscricoesErrorCodes.EdicaoDoExameInvalida));
    }

    [Theory(DisplayName = "Condição sem o código do motivo desfavorável é aceita: o motivo ausente fica ausente")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Declarar_MotivoAusente_Aceita(string? motivo)
    {
        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.Declarar(CodigoDoRetornoDoInep, 2026, motivo, enquadramento: null);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.MotivoDesfavoravelCodigo.Should().BeNull();
    }

    [Fact(DisplayName = "Condição com o código do motivo fora do formato é recusada no campo do motivo")]
    public void Declarar_MotivoMalFormatado_RecusaNoCampoDoMotivo()
    {
        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.Declarar(CodigoDoRetornoDoInep, 2026, "sem resultado", enquadramento: null);

        RecusasPorCampo(resultado).Should().Equal(("motivoDesfavoravelCodigo", MotivoDeDecisaoErrorCodes.CodigoFormatoInvalido));
    }

    [Theory(DisplayName = "Tipo desconhecido ou ausente é recusado no campo do tipo como condição não disponível")]
    [InlineData("TAXA_PAGA_OU_ISENCAO_DEFERIDA")]
    [InlineData("resultado_no_retorno_do_inep")]
    [InlineData("")]
    [InlineData(null)]
    public void Declarar_TipoDesconhecidoOuAusente_RecusaNoCampoDoTipo(string? tipoCodigo)
    {
        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.Declarar(tipoCodigo, 2026, "SEM_RESULTADO_INEP", enquadramento: null);

        RecusasPorCampo(resultado).Should().Equal(("tipo", HomologacaoDasInscricoesErrorCodes.CondicaoNaoDisponivel));
    }

    [Fact(DisplayName = "As recusas da condição se acumulam, na ordem tipo, edição e motivo")]
    public void Declarar_VariosCamposInvalidos_AcumulaAsRecusas()
    {
        Result<CondicaoDeHomologacao> resultado = CondicaoDeHomologacao.Declarar("NOTA_MINIMA", 1997, "sem resultado", enquadramento: null);

        RecusasPorCampo(resultado).Should().Equal(
            ("tipo", HomologacaoDasInscricoesErrorCodes.CondicaoNaoDisponivel),
            ("edicaoDoExame", HomologacaoDasInscricoesErrorCodes.EdicaoDoExameInvalida),
            ("motivoDesfavoravelCodigo", MotivoDeDecisaoErrorCodes.CodigoFormatoInvalido));
    }

    [Fact(DisplayName = "Condições com o mesmo conteúdo são iguais; mudar a edição, o motivo ou o enquadramento as distingue")]
    public void MesmoConteudo_ComparaTipoEdicaoMotivoEEnquadramento()
    {
        CondicaoDeHomologacao condicao = ConfirmacaoDoInep(2026, "SEM_RESULTADO_INEP");

        condicao.MesmoConteudo(ConfirmacaoDoInep(2026, "SEM_RESULTADO_INEP")).Should().BeTrue();
        condicao.MesmoConteudo(ConfirmacaoDoInep(2025, "SEM_RESULTADO_INEP")).Should().BeFalse();
        condicao.MesmoConteudo(ConfirmacaoDoInep(2026, "OUTRO_MOTIVO")).Should().BeFalse();
    }

    [Fact(DisplayName = "Condições sem motivo são iguais entre si e diferentes da que tem motivo")]
    public void MesmoConteudo_MotivoAusente_ComparaAAusencia()
    {
        CondicaoDeHomologacao semMotivo = CondicaoDeHomologacao.Declarar(CodigoDoRetornoDoInep, 2026, null, enquadramento: null).Value!;

        semMotivo.MesmoConteudo(CondicaoDeHomologacao.Declarar(CodigoDoRetornoDoInep, 2026, null, enquadramento: null).Value!).Should().BeTrue();
        semMotivo.MesmoConteudo(ConfirmacaoDoInep(2026, "SEM_RESULTADO_INEP")).Should().BeFalse();
    }

    private static (string? Campo, string Codigo)[] RecusasPorCampo(Result<CondicaoDeHomologacao> resultado) =>
        [.. resultado.Errors.Select(static recusa => (recusa.Field, recusa.Error.Code))];
}
