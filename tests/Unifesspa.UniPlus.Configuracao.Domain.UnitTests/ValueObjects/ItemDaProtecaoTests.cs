namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O item sob o circuito de aprovação: o cadastro e o identificador.</summary>
public sealed class ItemDaProtecaoTests
{
    [Theory(DisplayName = "O cadastro do vocabulário, escrito exatamente, é aceito")]
    [InlineData("TIPO_DOCUMENTO", CadastroDoCatalogo.TipoDocumento)]
    [InlineData("DADO_DO_CANDIDATO", CadastroDoCatalogo.Fato)]
    public void Ler_CadastroValido(string cadastro, CadastroDoCatalogo esperado)
    {
        Guid id = Guid.CreateVersion7();
        List<FieldError> erros = [];

        ItemDaProtecao.Ler(cadastro, id, erros).Should().Be(new ItemDaProtecao(esperado, id));
        erros.Should().BeEmpty();
    }

    [Theory(DisplayName = "O cadastro ausente ou fora do vocabulário é recusado no campo cadastro")]
    [InlineData(null)]
    [InlineData("MODALIDADE")]
    [InlineData("fato")]
    [InlineData("tipo_documento")]
    [InlineData(" TIPO_DOCUMENTO ")]
    [InlineData(" DADO_DO_CANDIDATO ")]
    public void Ler_CadastroInvalido_RecusaNoCampo(string? cadastro)
    {
        List<FieldError> erros = [];

        ItemDaProtecao.Ler(cadastro, Guid.CreateVersion7(), erros).Should().BeNull();
        erros.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "cadastro" && e.Error.Code == ProtecaoDoCatalogoErrorCodes.CadastroInvalido);
    }
}
