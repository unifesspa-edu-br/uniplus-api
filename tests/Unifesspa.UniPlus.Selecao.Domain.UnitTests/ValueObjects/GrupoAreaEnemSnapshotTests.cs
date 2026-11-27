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

    [Theory(DisplayName = "Código ausente ou em branco é recusado no campo do código")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemCodigo_RecusaNoCampoDoCodigo(string? codigo)
    {
        Result<GrupoAreaEnemSnapshot> resultado = GrupoAreaEnemSnapshot.Criar(codigo, "Tecnológica");

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "grupoAreaEnem.codigo" && e.Error.Code == "GrupoAreaEnemSnapshot.CodigoObrigatorio");
    }

    [Theory(DisplayName = "Rótulo ausente ou em branco é recusado no campo do rótulo")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemRotulo_RecusaNoCampoDoRotulo(string? rotulo)
    {
        Result<GrupoAreaEnemSnapshot> resultado = GrupoAreaEnemSnapshot.Criar("TECNOLOGICA", rotulo);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "grupoAreaEnem.rotulo" && e.Error.Code == "GrupoAreaEnemSnapshot.RotuloObrigatorio");
    }

    [Fact(DisplayName = "Código ausente e rótulo com caractere nulo voltam juntos, cada um no seu campo")]
    public void Criar_SemCodigoERotuloComCaractereInvalido_AcumulaAsDuasRecusas()
    {
        Result<GrupoAreaEnemSnapshot> resultado = GrupoAreaEnemSnapshot.Criar(null, "Tecno\0lógica");

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("grupoAreaEnem.codigo", "GrupoAreaEnemSnapshot.CodigoObrigatorio"),
            ("grupoAreaEnem.rotulo", "GrupoAreaEnemSnapshot.CaractereInvalido"));
    }

    [Fact(DisplayName = "Código com caractere nulo e rótulo ausente voltam juntos, cada um no seu campo")]
    public void Criar_CodigoComCaractereInvalidoESemRotulo_AcumulaAsDuasRecusas()
    {
        Result<GrupoAreaEnemSnapshot> resultado = GrupoAreaEnemSnapshot.Criar("TECNO\0LOGICA", null);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("grupoAreaEnem.codigo", "GrupoAreaEnemSnapshot.CaractereInvalido"),
            ("grupoAreaEnem.rotulo", "GrupoAreaEnemSnapshot.RotuloObrigatorio"));
    }

    [Fact(DisplayName = "Código acima do limite e rótulo com caractere nulo voltam juntos, cada um no seu campo")]
    public void Criar_CodigoLongoERotuloComCaractereInvalido_AcumulaAsDuasRecusas()
    {
        Result<GrupoAreaEnemSnapshot> resultado = GrupoAreaEnemSnapshot.Criar(new string('C', 31), "Tecno\0lógica");

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("grupoAreaEnem.codigo", "GrupoAreaEnemSnapshot.TamanhoInvalido"),
            ("grupoAreaEnem.rotulo", "GrupoAreaEnemSnapshot.CaractereInvalido"));
    }

    [Fact(DisplayName = "Caractere nulo no código é recusado na fronteira do domínio")]
    public void Criar_CaractereNulo_Recusa()
    {
        GrupoAreaEnemSnapshot.Criar("TECNO\0LOGICA", "Tecnológica").Error!.Code.Should().Be("GrupoAreaEnemSnapshot.CaractereInvalido");
    }

    [Fact(DisplayName = "Código e rótulo inválidos ao mesmo tempo não repetem o valor recebido em mensagem nem em campo")]
    public void Criar_CodigoERotuloInvalidos_NaoRepeteOValorRecebido()
    {
        Result<GrupoAreaEnemSnapshot> resultado = GrupoAreaEnemSnapshot.Criar("CODIGO-FICTICIO-\u202E-RECONHECIVEL", "ROTULO-FICTICIO-\0-RECONHECIVEL");

        resultado.Errors.Should().HaveCount(2);
        resultado.Errors.Select(e => e.Error.Message).Concat(resultado.Errors.Select(e => e.Field))
            .Should().OnlyContain(texto => !texto!.Contains("FICTICIO", StringComparison.OrdinalIgnoreCase)
                && !texto.Contains("RECONHECIVEL", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "O rótulo com controle bidirecional é recusado na criação, sem ecoar o texto nem o controle")]
    public void Criar_RotuloComControleBidirecional_RecusaSemEcoar()
    {
        Result<GrupoAreaEnemSnapshot> resultado = GrupoAreaEnemSnapshot.Criar("TECNOLOGICA", "Tecno\u202Elógica");

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("GrupoAreaEnemSnapshot.CaractereInvalido");
        resultado.Error!.Message.Should().NotContain("\u202E").And.NotContain("Tecno");
        resultado.Value.Should().BeNull();
    }

    [Theory(DisplayName = "Código ou rótulo acima do tamanho da coluna é recusado no campo e com o limite na mensagem")]
    [InlineData(31, 10, "grupoAreaEnem.codigo", "O código do grupo de área do ENEM deve ter até 30 caracteres.")]
    [InlineData(10, 61, "grupoAreaEnem.rotulo", "O rótulo do grupo de área do ENEM deve ter até 60 caracteres.")]
    public void Criar_AcimaDoTamanho_Recusa(int tamanhoCodigo, int tamanhoRotulo, string campo, string mensagem)
    {
        Result<GrupoAreaEnemSnapshot> resultado = GrupoAreaEnemSnapshot.Criar(new string('C', tamanhoCodigo), new string('r', tamanhoRotulo));

        FieldError erro = resultado.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be(campo);
        erro.Error.Code.Should().Be("GrupoAreaEnemSnapshot.TamanhoInvalido");
        erro.Error.Message.Should().Be(mensagem);
    }

    [Fact(DisplayName = "No limite do tamanho da coluna é aceito")]
    public void Criar_NoLimite_Aceita()
    {
        GrupoAreaEnemSnapshot.Criar(new string('C', 30), new string('r', 60)).IsSuccess.Should().BeTrue();
    }
}
