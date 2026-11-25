namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

public sealed class CaminhosDasBasesLegaisEEtapasTests
{
    [Theory]
    [InlineData(null, "indeferimento.basesLegais[2]")]
    [InlineData("referencia", "indeferimento.basesLegais[2].referencia")]
    [InlineData("[0].valor", "indeferimento.basesLegais[2][0].valor")]
    public void ComIndice_BaseLegal_PosicionaACausa(string? campo, string esperado)
    {
        FieldError causa = new(campo, new DomainError("codigo", "mensagem"), "origem");

        FieldError resultado = EscritaDasBasesLegais.ComIndice([causa], 2).Single();

        resultado.Field.Should().Be(esperado);
        resultado.Origem.Should().Be("origem");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("nome", "etapas[2].nome")]
    [InlineData("[0].valor", "etapas[2][0].valor")]
    public void ComIndice_Etapa_PosicionaACausa(string? campo, string? esperado)
    {
        FieldError causa = new(campo, new DomainError("codigo", "mensagem"), "origem");

        FieldError resultado = EscritaDasEtapasDoProcesso.ComIndice([causa], 2).Single();

        resultado.Field.Should().Be(esperado);
        resultado.Origem.Should().Be("origem");
    }
}
