namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.ApoioDoModeloDeFormulario;

public sealed class FormatoDosCodigosDoModeloFormularioTests
{
    [Fact]
    public void ConferirCadastro_CodigosNosLimites_Aceita() =>
        Conferir(new string('A', 60), new string('B', 80)).Should().BeEmpty();

    [Theory]
    [InlineData(" MODELO")]
    [InlineData("MODELO ")]
    [InlineData("zq9k")]
    [InlineData("MODELO_Á")]
    public void ConferirCadastro_CodigoInvalido_RecusaSemRepetirValor(string codigo) =>
        ConferirRecusa(Conferir(codigo, null), ModeloFormularioErrorCodes.CodigoFormatoInvalido, codigo);

    [Theory]
    [InlineData(" TIPO")]
    [InlineData("TIPO ")]
    [InlineData("zq9k")]
    [InlineData("TIPO_Á")]
    public void ConferirCadastro_TipoInvalido_RecusaSemRepetirValor(string codigo) =>
        ConferirRecusa(Conferir("MODELO", codigo), ModeloFormularioErrorCodes.TipoProcessoCodigoFormatoInvalido, codigo);

    [Fact]
    public void ConferirCadastro_CodigoCom61Caracteres_Recusa() =>
        ConferirRecusa(Conferir(new string('A', 61), null), ModeloFormularioErrorCodes.CodigoFormatoInvalido, new string('A', 61));

    [Fact]
    public void ConferirCadastro_TipoCom81Caracteres_Recusa() =>
        ConferirRecusa(Conferir("MODELO", new string('B', 81)), ModeloFormularioErrorCodes.TipoProcessoCodigoFormatoInvalido, new string('B', 81));

    [Fact]
    public void ConferirCadastro_TipoAusente_Aceita() => Conferir("MODELO", null).Should().BeEmpty();

    [Fact]
    public void CriarECorrigir_CodigosValidos_GravaComoRecebidos()
    {
        ConteudoDoModelo conteudo = Conteudo([Item("CERTIFICADO", 0)]);
        Result<ModeloFormulario> criado = ModeloFormulario.Criar("MODELO_1", "Modelo", null, FinalidadeFormulario.Habilitacao, "TIPO_1", conteudo, SemDerivacoes);
        criado.IsSuccess.Should().BeTrue();
        ModeloFormulario modelo = criado.Value!;
        modelo.Codigo.Should().Be("MODELO_1");
        modelo.TipoProcessoCodigo.Should().Be("TIPO_1");
        modelo.Atualizar("Modelo", null, "TIPO_2", conteudo, SemDerivacoes).IsSuccess.Should().BeTrue();
        modelo.TipoProcessoCodigo.Should().Be("TIPO_2");
    }

    private static List<FieldError> Conferir(string codigo, string? tipo) =>
        ModeloFormulario.ConferirCadastro(codigo, "Modelo", null, tipo, FinalidadeFormulario.Habilitacao);

    private static void ConferirRecusa(List<FieldError> erros, string codigo, string valor)
    {
        FieldError erro = erros.Should().ContainSingle().Subject;
        erro.Error.Code.Should().Be(codigo);
        erro.Error.Message.Should().NotContain(valor);
    }
}
