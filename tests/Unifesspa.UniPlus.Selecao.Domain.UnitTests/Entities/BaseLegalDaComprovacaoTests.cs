namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class BaseLegalDaComprovacaoTests
{
    [Fact(DisplayName = "Criar aceita referência, abrangência e status coerentes")]
    public void Criar_DadosCoerentes_Aceita()
    {
        Result<BaseLegalDaComprovacao> resultado = BaseLegalDaComprovacao.Criar(
            "Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, "Observação livre");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Referencia.Should().Be("Lei 12.711/2012, art. 3º");
    }

    [Fact(DisplayName = "Referência vazia é recusada")]
    public void Criar_ReferenciaVazia_Recusa()
    {
        Result<BaseLegalDaComprovacao> resultado = BaseLegalDaComprovacao.Criar(
            "", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("BaseLegalDaComprovacao.ReferenciaObrigatoria");
    }

    [Fact(DisplayName = "Abrangência Nenhuma é recusada")]
    public void Criar_AbrangenciaNenhuma_Recusa()
    {
        Result<BaseLegalDaComprovacao> resultado = BaseLegalDaComprovacao.Criar(
            "Lei X", TipoAbrangencia.Nenhuma, StatusBaseLegal.Resolvido, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("BaseLegalDaComprovacao.AbrangenciaObrigatoria");
    }

    [Fact(DisplayName = "Status Nenhuma é recusado")]
    public void Criar_StatusNenhuma_Recusa()
    {
        Result<BaseLegalDaComprovacao> resultado = BaseLegalDaComprovacao.Criar(
            "Lei X", TipoAbrangencia.Federal, StatusBaseLegal.Nenhuma, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("BaseLegalDaComprovacao.SituacaoObrigatoria");
    }

    [Fact(DisplayName = "ADR-0125: referência vazia e status Nenhuma acumulam no mesmo lote")]
    public void Criar_ReferenciaVaziaEStatusNenhuma_AcumulaAsDuasViolacoes()
    {
        Result<BaseLegalDaComprovacao> resultado = BaseLegalDaComprovacao.Criar(
            "", TipoAbrangencia.Federal, StatusBaseLegal.Nenhuma, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "BaseLegalDaComprovacao.ReferenciaObrigatoria",
            "BaseLegalDaComprovacao.SituacaoObrigatoria",
        ]);
    }

    [Fact(DisplayName = "ValidarFormaBasica sem violação retorna lote vazio")]
    public void ValidarFormaBasica_SemViolacao_Vazio()
    {
        List<FieldError> erros = BaseLegalDaComprovacao.ValidarFormaBasica(
            "Lei X", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido);

        erros.Should().BeEmpty();
    }
}
