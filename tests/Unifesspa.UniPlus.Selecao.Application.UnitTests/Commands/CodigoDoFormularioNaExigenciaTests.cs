namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>O código do formulário é obrigatório em toda exigência de documento e precisa estar no formato.</summary>
public sealed class CodigoDoFormularioNaExigenciaTests
{
    [Theory(DisplayName = "Exigência sem código do formulário é recusada no campo do código como obrigatório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Recusa_CodigoAusente_RecusaNoCampoDoCodigo(string? codigo)
    {
        List<FieldError> recusas = EscritaDaExigencia.RecusaDoCodigoDoFormulario(codigo).ToList();

        recusas.Select(static r => (r.Field, r.Error.Code)).Should().Equal((MomentoDaExigenciaInput.CaminhoDoFormulario, CodigoDoFormularioErrorCodes.Obrigatorio));
    }

    [Theory(DisplayName = "Exigência com código fora do formato é recusada no campo do código")]
    [InlineData("habilitacao")]
    [InlineData("COM-TRACO")]
    public void Recusa_CodigoForaDoFormato_RecusaNoCampoDoCodigo(string codigo)
    {
        List<FieldError> recusas = EscritaDaExigencia.RecusaDoCodigoDoFormulario(codigo).ToList();

        recusas.Select(static r => (r.Field, r.Error.Code)).Should().Equal((MomentoDaExigenciaInput.CaminhoDoFormulario, CodigoDoFormularioErrorCodes.FormatoInvalido));
    }

    [Theory(DisplayName = "Exigência com código no formato não é recusada pelo código")]
    [InlineData("HABILITACAO")]
    [InlineData("FORMULARIO_DA_BANCA_2")]
    public void Recusa_CodigoNoFormato_NaoRecusa(string codigo) =>
        EscritaDaExigencia.RecusaDoCodigoDoFormulario(codigo).Should().BeEmpty();
}
