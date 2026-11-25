namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class FormatoDosCodigosDePublicacoesTests
{
    [Theory]
    [InlineData("ATO_2")]
    [InlineData("A1")]
    public void Codigos_FormatoValido_Aceitos(string codigo)
    {
        Validar(codigo).IsSuccess.Should().BeTrue();
        TipoDaEntidadeVinculada.TemFormaCanonica(codigo).Should().BeTrue();
    }

    [Theory]
    [InlineData(" ATO")]
    [InlineData("ATO ")]
    [InlineData("codigo_minusculo")]
    [InlineData("ATÓ")]
    [InlineData("ATO__DOIS")]
    [InlineData("2ATO")]
    public void Codigos_FormatoInvalido_RecusadosSemRepetirValor(string codigo)
    {
        Result<CamposDoTipoAtoPublicado> resultado = Validar(codigo);
        resultado.Error!.Code.Should().Be(TipoAtoPublicadoErrorCodes.CodigoFormato);
        resultado.Error.Message.Should().NotContain(codigo);
        TipoDaEntidadeVinculada.TemFormaCanonica(codigo).Should().BeFalse();
    }

    [Theory]
    [InlineData(60, true)]
    [InlineData(61, false)]
    public void Codigos_Limite_Conferido(int tamanho, bool aceito)
    {
        string codigo = new('A', tamanho);
        Validar(codigo).IsSuccess.Should().Be(aceito);
    }

    [Fact]
    public void TemFormaCanonica_RotuloLongo_ConferePeloFormatoSemOLimite() =>
        TipoDaEntidadeVinculada.TemFormaCanonica(new string('A', TipoDaEntidadeVinculada.TamanhoMaximo + 1)).Should().BeTrue();

    [Fact]
    public void ValidarCampos_VariasFalhas_PreservaOrdem()
    {
        Result<CamposDoTipoAtoPublicado> resultado = TipoAtoPublicado.ValidarDeclaracao((new TipoAtoPublicadoDeTeste
        {
            Codigo = "2ATO",
            Nome = "",
            VigenciaInicio = new DateOnly(2026, 1, 2),
            VigenciaFim = new DateOnly(2026, 1, 1),
            BaseLegal = new string('A', 501),
            EfeitoIrreversivel = true,
            EhResultado = true
        }).Declaracao());
        resultado.Errors.Select(erro => erro.Field).Should().Equal(
            "codigo", "nome", "baseLegal", "vigenciaFim", "efeitoIrreversivel");
    }

    private static Result<CamposDoTipoAtoPublicado> Validar(string codigo) =>
        TipoAtoPublicado.ValidarDeclaracao((new TipoAtoPublicadoDeTeste
        {
            Codigo = codigo,
            Nome = "Ato",
            VigenciaInicio = new DateOnly(2026, 1, 1)
        }).Declaracao());
}
