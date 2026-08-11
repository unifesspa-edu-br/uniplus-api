namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class TipoEtapaSnapshotTests
{
    private static readonly Guid OrigemId = Guid.CreateVersion7();

    [Fact(DisplayName = "Criar com dados válidos tem sucesso")]
    public void Criar_Valida_Sucesso()
    {
        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(OrigemId, "PROVA_OBJETIVA", "Prova Objetiva");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.OrigemId.Should().Be(OrigemId);
        resultado.Value!.Codigo.Should().Be("PROVA_OBJETIVA");
        resultado.Value!.Nome.Should().Be("Prova Objetiva");
    }

    [Theory(DisplayName = "Criar com espaços nas bordas remove-os (Trim)")]
    [InlineData(" PROVA_OBJETIVA ", "PROVA_OBJETIVA")]
    public void Criar_ComEspacos_Trima(string entrada, string esperado)
    {
        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(OrigemId, entrada, "Prova Objetiva");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Codigo.Should().Be(esperado);
    }

    [Fact(DisplayName = "Criar com OrigemId vazio falha")]
    public void Criar_OrigemIdVazio_Falha()
    {
        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(Guid.Empty, "PROVA_OBJETIVA", "Prova Objetiva");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.OrigemIdObrigatorio");
    }

    [Fact(DisplayName = "Criar com código vazio falha")]
    public void Criar_CodigoVazio_Falha()
    {
        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(OrigemId, "", "Prova Objetiva");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.CodigoObrigatorio");
    }

    [Fact(DisplayName = "Criar com nome vazio falha")]
    public void Criar_NomeVazio_Falha()
    {
        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(OrigemId, "PROVA_OBJETIVA", "");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.NomeObrigatorio");
    }

    [Fact(DisplayName = "Criar com caractere nulo no codigo falha")]
    public void Criar_CodigoComCaractereNulo_Falha()
    {
        string codigoComNulo = "PROVA" + '\0' + "OBJETIVA";

        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(OrigemId, codigoComNulo, "Prova Objetiva");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.CaractereNulo");
    }

    [Fact(DisplayName = "Criar com caractere nulo no nome falha")]
    public void Criar_NomeComCaractereNulo_Falha()
    {
        string nomeComNulo = "Prova" + '\0' + "Objetiva";

        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(OrigemId, "PROVA_OBJETIVA", nomeComNulo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.CaractereNulo");
    }

    [Fact(DisplayName = "Criar com código acima de 64 caracteres falha")]
    public void Criar_CodigoAcimaDoLimite_Falha()
    {
        string codigoLongo = new('A', 65);

        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(OrigemId, codigoLongo, "Prova Objetiva");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.TamanhoInvalido");
    }

    [Fact(DisplayName = "Criar com nome acima de 200 caracteres falha")]
    public void Criar_NomeAcimaDoLimite_Falha()
    {
        string nomeLongo = new('A', 201);

        Result<TipoEtapaSnapshot> resultado = TipoEtapaSnapshot.Criar(OrigemId, "PROVA_OBJETIVA", nomeLongo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("TipoEtapaSnapshot.TamanhoInvalido");
    }

    [Fact(DisplayName = "ToString devolve o codigo")]
    public void ToString_DevolveCodigo()
    {
        TipoEtapaSnapshot snapshot = TipoEtapaSnapshot.Criar(OrigemId, "PROVA_OBJETIVA", "Prova Objetiva").Value!;

        snapshot.ToString().Should().Be("PROVA_OBJETIVA");
    }
}
