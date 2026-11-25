namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// O código que identifica o formulário no processo: letra maiúscula inicial, depois letras maiúsculas,
/// dígitos e sublinhado, até sessenta caracteres. Sem acento e sem hífen, para valer como chave estável
/// no documento e no envelope congelado.
/// </summary>
public sealed class CodigoDoFormularioTests
{
    [Theory(DisplayName = "O código no formato fechado é aceito")]
    [InlineData("INSCRICAO")]
    [InlineData("HABILITACAO_2")]
    [InlineData("A")]
    public void FormatoValido_E_Aceito(string valor)
    {
        Result<CodigoDoFormulario> resultado = CodigoDoFormulario.Criar(valor);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(valor);
    }

    [Fact(DisplayName = "O código aceita sessenta caracteres e recusa sessenta e um")]
    public void Tamanho_AceitaSessentaERecusaSessentaEUm()
    {
        CodigoDoFormulario.Criar("A" + new string('B', 59)).IsSuccess.Should().BeTrue();
        CodigoDoFormulario.Criar("A" + new string('B', 60)).Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(CodigoDoFormularioErrorCodes.FormatoInvalido);
    }

    [Theory(DisplayName = "Código ausente ou em branco é recusado como obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ausente_RecusaComoObrigatorio(string? valor)
    {
        CodigoDoFormulario.Criar(valor).Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(CodigoDoFormularioErrorCodes.Obrigatorio);
    }

    [Theory(DisplayName = "Código fora do formato é recusado")]
    [InlineData("  INSCRICAO ")]
    [InlineData("INSCRICAO\n")]
    [InlineData("inscricao")]
    [InlineData("1INSCRICAO")]
    [InlineData("_INSCRICAO")]
    [InlineData("INSCRIÇÃO")]
    [InlineData("INSCRICAO-2")]
    [InlineData("INSCRICAO 2")]
    [InlineData("A__B")]
    [InlineData("A_")]
    public void ForaDoFormato_Recusa(string valor)
    {
        CodigoDoFormulario.Criar(valor).Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(CodigoDoFormularioErrorCodes.FormatoInvalido);
    }

    [Fact(DisplayName = "A ordem dos códigos é ordinal, não a da cultura")]
    public void Ordem_EOrdinal()
    {
        CodigoDoFormulario[] codigos = [.. new[] { "B_1", "B2", "A" }.Select(static valor => CodigoDoFormulario.Criar(valor).Value!)];

        CodigoDoFormulario[] ordenados = [.. codigos.Order(CodigoDoFormulario.OrdemOrdinal)];

        ordenados.Select(static codigo => codigo.Valor).Should().Equal("A", "B2", "B_1");
    }
}
