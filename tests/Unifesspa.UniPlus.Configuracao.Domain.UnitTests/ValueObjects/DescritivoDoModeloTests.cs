namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O descritivo confere o nome, a descrição e o tipo de processo, todos juntos, no caminho do descritivo.</summary>
public sealed class DescritivoDoModeloTests
{
    [Fact(DisplayName = "O descritivo bem formado não tem recusa, com a descrição e o tipo ausentes")]
    public void Conferir_DescritivoBemFormado_NaoRecusa()
    {
        new DescritivoDoModelo("Habilitação", null, null).Conferir().Should().BeEmpty();
        new DescritivoDoModelo(new string('N', ModeloFormulario.NomeMaxLength), new string('D', ModeloFormulario.DescricaoMaxLength), "TIPO_1")
            .Conferir().Should().BeEmpty();
    }

    [Theory(DisplayName = "O nome vazio ou em branco é recusado no caminho do nome")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Conferir_NomeVazio_Recusa(string? nome) =>
        new DescritivoDoModelo(nome, null, null).Conferir().Should().ContainSingle().Which.Should().Match<FieldError>(
            static e => e.Field == "descritivo.nome" && e.Error.Code == ModeloFormularioErrorCodes.NomeObrigatorio);

    [Fact(DisplayName = "O nome acima do limite é recusado, sem repetir o valor enviado")]
    public void Conferir_NomeLongo_RecusaSemRepetirOValor()
    {
        string nome = new('N', ModeloFormulario.NomeMaxLength + 1);

        FieldError erro = new DescritivoDoModelo(nome, null, null).Conferir().Should().ContainSingle().Subject;

        erro.Field.Should().Be("descritivo.nome");
        erro.Error.Code.Should().Be(ModeloFormularioErrorCodes.NomeTamanho);
        erro.Error.Message.Should().NotContain(nome);
    }

    [Fact(DisplayName = "A descrição acima do limite é recusada no caminho da descrição")]
    public void Conferir_DescricaoLonga_Recusa() =>
        new DescritivoDoModelo("Habilitação", new string('D', ModeloFormulario.DescricaoMaxLength + 1), null).Conferir()
            .Should().ContainSingle().Which.Should().Match<FieldError>(
                static e => e.Field == "descritivo.descricao" && e.Error.Code == ModeloFormularioErrorCodes.DescricaoTamanho);

    [Theory(DisplayName = "O tipo de processo em branco ou fora do formato é recusado no caminho do tipo")]
    [InlineData("   ", ModeloFormularioErrorCodes.TipoProcessoCodigoEmBranco)]
    [InlineData(" TIPO", ModeloFormularioErrorCodes.TipoProcessoCodigoFormatoInvalido)]
    [InlineData("tipo_minusculo", ModeloFormularioErrorCodes.TipoProcessoCodigoFormatoInvalido)]
    public void Conferir_TipoDeProcessoInvalido_Recusa(string tipo, string codigo) =>
        new DescritivoDoModelo("Habilitação", null, tipo).Conferir().Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "descritivo.tipoProcessoCodigo" && e.Error.Code == codigo);

    [Fact(DisplayName = "As recusas do nome, da descrição e do tipo saem juntas, nessa ordem")]
    public void Conferir_VariasRecusas_AcumulamNaOrdem() =>
        new DescritivoDoModelo(" ", new string('D', ModeloFormulario.DescricaoMaxLength + 1), " TIPO").Conferir()
            .Select(static e => e.Field).Should().Equal("descritivo.nome", "descritivo.descricao", "descritivo.tipoProcessoCodigo");

    [Fact(DisplayName = "O texto que o banco não grava é recusado no campo dele")]
    public void Conferir_TextoNaoGravavel_RecusaNoCampo() =>
        new DescritivoDoModelo("Habilitação", "texto\0", null).Conferir().Should().ContainSingle().Which.Field.Should().Be("descritivo.descricao");
}
