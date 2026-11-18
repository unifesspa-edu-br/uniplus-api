namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class CancelamentoDaInscricaoTests
{
    [Fact]
    public void Registrar_MesmosValores_PossuiIgualdadePorValor()
    {
        CancelamentoDaInscricao primeiro = CancelamentoDaInscricao.Registrar("motivo", "conta", DateTimeOffset.UnixEpoch).Value!;
        CancelamentoDaInscricao segundo = CancelamentoDaInscricao.Registrar("motivo", "conta", DateTimeOffset.UnixEpoch).Value!;
        primeiro.Should().Be(segundo);
        primeiro.GetHashCode().Should().Be(segundo.GetHashCode());
        typeof(CancelamentoDaInscricao).GetConstructors().Should().BeEmpty();
        typeof(CancelamentoDaInscricao).GetProperties().Should().OnlyContain(propriedade => !propriedade.CanWrite);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Reidratar_UmCampoNulo_PreservaOsOutros(bool instanteNulo, bool contaNula, bool motivoNulo)
    {
        DateTimeOffset? instante = instanteNulo ? null : DateTimeOffset.UnixEpoch;
        string? conta = contaNula ? null : "conta";
        string? motivo = motivoNulo ? null : "motivo";
        CancelamentoDaInscricao cancelamento = CancelamentoDaInscricao.Reidratar(instante, conta, motivo)!;
        cancelamento.CanceladaEm.Should().Be(instante);
        cancelamento.CanceladaPor.Should().Be(conta);
        cancelamento.MotivoDoCancelamento.Should().Be(motivo);
    }

    [Fact]
    public void Reidratar_TodosCamposNulos_NaoCriaCancelamento()
    {
        CancelamentoDaInscricao.Reidratar(null, null, null).Should().BeNull();
    }

    [Fact]
    public void Registrar_SemMotivo_UsaMotivoPadrao()
    {
        Result<CancelamentoDaInscricao> resultado = CancelamentoDaInscricao.Registrar("  ", "conta", DateTimeOffset.UnixEpoch);
        resultado.Value!.MotivoDoCancelamento.Should().Be(CancelamentoDaInscricao.MotivoPadraoDoCancelamento);
        resultado.Value.CanceladaPor.Should().Be("conta");
        resultado.Value.CanceladaEm.Should().Be(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void Registrar_MotivoComEspacos_RemoveEspacosDasPontas()
    {
        CancelamentoDaInscricao.Registrar(" motivo ", "conta", DateTimeOffset.UnixEpoch)
            .Value!.MotivoDoCancelamento.Should().Be("motivo");
    }

    [Fact]
    public void Registrar_AtorVazio_LancaExcecao()
    {
        Action registrar = () => CancelamentoDaInscricao.Registrar(null, " ", DateTimeOffset.UnixEpoch);
        registrar.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Registrar_MotivoLongo_Recusa()
    {
        Result<CancelamentoDaInscricao> resultado = CancelamentoDaInscricao.Registrar(new string('x', 501), "conta", DateTimeOffset.UnixEpoch);
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.MotivoDoCancelamentoLongo);
    }
}
