namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O ciclo de aprovação da proteção do item: começa em 1 e só avança (ADR-0163).</summary>
public sealed class CicloDaAprovacaoTests
{
    [Fact(DisplayName = "Todo item nasce no primeiro ciclo, e o reinício abre o seguinte")]
    public void Primeiro_ESeguinte()
    {
        CicloDaAprovacao.Primeiro.Numero.Should().Be(1);
        CicloDaAprovacao.Primeiro.Seguinte().Should().Be(new CicloDaAprovacao(2));
    }

    [Fact(DisplayName = "O ciclo lido no item, de 1 em diante, é aceito sem recusa")]
    public void Ler_CicloValido_Aceita()
    {
        List<FieldError> erros = [];

        CicloDaAprovacao? lido = CicloDaAprovacao.Ler(3, erros);

        lido.Should().Be(new CicloDaAprovacao(3));
        erros.Should().BeEmpty();
    }

    [Theory(DisplayName = "O ciclo ausente, zero ou negativo é recusado no campo ciclo")]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ler_CicloInvalido_RecusaNoCampo(int? numero)
    {
        List<FieldError> erros = [];

        CicloDaAprovacao? lido = CicloDaAprovacao.Ler(numero, erros);

        lido.Should().BeNull();
        erros.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "ciclo" && e.Error.Code == ProtecaoDoCatalogoErrorCodes.CicloInvalido);
    }
}
