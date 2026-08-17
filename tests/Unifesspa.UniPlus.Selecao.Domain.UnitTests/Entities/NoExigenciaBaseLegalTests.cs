namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Cobertura de <see cref="NoExigenciaBaseLegal"/> (Story #920) — mesmo shape/validação de
/// <see cref="DocumentoExigidoBaseLegal"/>, ver <see cref="DocumentoExigidoBaseLegalTests"/>.
/// </summary>
public sealed class NoExigenciaBaseLegalTests
{
    [Fact(DisplayName = "Criar aceita referência, abrangência e status coerentes")]
    public void Criar_DadosCoerentes_Aceita()
    {
        Result<NoExigenciaBaseLegal> resultado = NoExigenciaBaseLegal.Criar(
            "Lei 12.711/2012, art. 3º", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, "Observação livre");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Referencia.Should().Be("Lei 12.711/2012, art. 3º");
    }

    [Fact(DisplayName = "Referência vazia é recusada")]
    public void Criar_ReferenciaVazia_Recusa()
    {
        Result<NoExigenciaBaseLegal> resultado = NoExigenciaBaseLegal.Criar(
            "", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigenciaBaseLegal.ReferenciaObrigatoria");
    }

    [Fact(DisplayName = "Abrangência Nenhuma é recusada")]
    public void Criar_AbrangenciaNenhuma_Recusa()
    {
        Result<NoExigenciaBaseLegal> resultado = NoExigenciaBaseLegal.Criar(
            "Lei X", TipoAbrangencia.Nenhuma, StatusBaseLegal.Resolvido, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigenciaBaseLegal.AbrangenciaObrigatoria");
    }

    [Fact(DisplayName = "Status Nenhuma é recusado")]
    public void Criar_StatusNenhuma_Recusa()
    {
        Result<NoExigenciaBaseLegal> resultado = NoExigenciaBaseLegal.Criar(
            "Lei X", TipoAbrangencia.Federal, StatusBaseLegal.Nenhuma, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NoExigenciaBaseLegal.StatusObrigatorio");
    }

    [Fact(DisplayName = "ADR-0125: referência vazia e status Nenhuma acumulam no mesmo lote")]
    public void Criar_ReferenciaVaziaEStatusNenhuma_AcumulaAsDuasViolacoes()
    {
        Result<NoExigenciaBaseLegal> resultado = NoExigenciaBaseLegal.Criar(
            "", TipoAbrangencia.Federal, StatusBaseLegal.Nenhuma, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "NoExigenciaBaseLegal.ReferenciaObrigatoria",
            "NoExigenciaBaseLegal.StatusObrigatorio",
        ]);
    }

    [Fact(DisplayName = "ValidarFormaBasica sem violação retorna lote vazio")]
    public void ValidarFormaBasica_SemViolacao_Vazio()
    {
        List<FieldError> erros = NoExigenciaBaseLegal.ValidarFormaBasica(
            "Lei X", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido);

        erros.Should().BeEmpty();
    }
}
