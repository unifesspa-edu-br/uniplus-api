namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// Invariantes do par por valor {id, hash} da versão de configuração invocada.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class ReferenciaVersaoConfiguracaoTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact(DisplayName = "Criar aceita id v7 e hash SHA-256 hex minúsculo")]
    public void Criar_ComParValido_Sucesso()
    {
        Guid id = Guid.CreateVersion7();

        Result<ReferenciaVersaoConfiguracao> resultado = ReferenciaVersaoConfiguracao.Criar(id, HashValido);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Id.Should().Be(id);
        resultado.Value.Hash.Should().Be(HashValido);
    }

    [Fact(DisplayName = "Id vazio é recusado")]
    public void Criar_ComIdVazio_Falha()
    {
        Result<ReferenciaVersaoConfiguracao> resultado = ReferenciaVersaoConfiguracao.Criar(Guid.Empty, HashValido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ReferenciaVersaoConfiguracao.IdObrigatorio");
    }

    [Theory(DisplayName = "Hash fora do formato SHA-256 é recusado")]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef")]
    public void Criar_ComHashInvalido_Falha(string hash)
    {
        Result<ReferenciaVersaoConfiguracao> resultado = ReferenciaVersaoConfiguracao.Criar(Guid.CreateVersion7(), hash);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ReferenciaVersaoConfiguracao.HashInvalido");
    }
}
