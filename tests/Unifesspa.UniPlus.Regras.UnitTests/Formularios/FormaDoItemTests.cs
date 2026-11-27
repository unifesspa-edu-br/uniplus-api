namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>A ajuda do item na forma do item.</summary>
public sealed class FormaDoItemTests
{
    [Theory(DisplayName = "A ajuda tem até 1000 caracteres")]
    [InlineData(1000, 0)]
    [InlineData(1001, 1)]
    public void RecusasDaAjuda_Tamanho_RecusaSoAcimaDoLimite(int tamanho, int recusas)
    {
        List<FieldError> erros = [.. FormaDoItem.RecusasDaAjuda(new string('a', tamanho))];

        erros.Should().HaveCount(recusas);
        erros.Should().OnlyContain(e => e.Field == "ajuda" && e.Error.Code == ItemFormularioErrorCodes.AjudaTamanho);
    }

    [Fact(DisplayName = "O limite da ajuda conta o texto aparado")]
    public void RecusasDaAjuda_EspacosNasPontas_NaoContam() =>
        FormaDoItem.RecusasDaAjuda($"  {new string('a', 1000)}  ").Should().BeEmpty();

    [Theory(DisplayName = "A ajuda ausente ou vazia não é recusada")]
    [InlineData(null)]
    [InlineData("   ")]
    public void RecusasDaAjuda_SemAjuda_SemRecusa(string? ajuda) =>
        FormaDoItem.RecusasDaAjuda(ajuda).Should().BeEmpty();
}
