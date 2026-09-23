namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class GrupoAreaEnemSnapshotTests
{
    [Fact(DisplayName = "Criar guarda código e rótulo, sem espaços nas bordas")]
    public void Criar_Valido_Guarda()
    {
        Result<GrupoAreaEnemSnapshot> resultado = GrupoAreaEnemSnapshot.Criar("  HUMANISTICA_I ", " Humanística I ");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Codigo.Should().Be("HUMANISTICA_I");
        resultado.Value.Rotulo.Should().Be("Humanística I");
    }

    [Fact(DisplayName = "O rótulo em forma decomposta é recomposto (NFC), como no envelope canônico")]
    public void Criar_RotuloDecomposto_RecompoeEmNfc()
    {
        string decomposto = "Humanística I".Normalize(NormalizationForm.FormD);

        GrupoAreaEnemSnapshot grupo = GrupoAreaEnemSnapshot.Criar("HUMANISTICA_I", decomposto).Value!;

        grupo.Rotulo.Should().Be("Humanística I".Normalize(NormalizationForm.FormC));
    }

    [Theory(DisplayName = "Código ausente ou em branco é recusado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemCodigo_Recusa(string? codigo)
    {
        GrupoAreaEnemSnapshot.Criar(codigo, "Tecnológica").Error!.Code.Should().Be("GrupoAreaEnemSnapshot.CodigoObrigatorio");
    }

    [Theory(DisplayName = "Rótulo ausente ou em branco é recusado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemRotulo_Recusa(string? rotulo)
    {
        GrupoAreaEnemSnapshot.Criar("TECNOLOGICA", rotulo).Error!.Code.Should().Be("GrupoAreaEnemSnapshot.RotuloObrigatorio");
    }

    [Fact(DisplayName = "Caractere nulo é recusado na fronteira do domínio")]
    public void Criar_CaractereNulo_Recusa()
    {
        GrupoAreaEnemSnapshot.Criar("TECNO\0LOGICA", "Tecnológica").Error!.Code.Should().Be("GrupoAreaEnemSnapshot.CaractereNulo");
    }

    [Theory(DisplayName = "Código ou rótulo acima do tamanho da coluna é recusado")]
    [InlineData(31, 10)]
    [InlineData(10, 61)]
    public void Criar_AcimaDoTamanho_Recusa(int tamanhoCodigo, int tamanhoRotulo)
    {
        GrupoAreaEnemSnapshot.Criar(new string('C', tamanhoCodigo), new string('r', tamanhoRotulo))
            .Error!.Code.Should().Be("GrupoAreaEnemSnapshot.TamanhoInvalido");
    }

    [Fact(DisplayName = "No limite do tamanho da coluna é aceito")]
    public void Criar_NoLimite_Aceita()
    {
        GrupoAreaEnemSnapshot.Criar(new string('C', 30), new string('r', 60)).IsSuccess.Should().BeTrue();
    }
}
