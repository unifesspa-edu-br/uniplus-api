namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A regra do que um anexo precisa ter para provar o ato (hash, nome, tipo de mídia,
/// unicidade do nome no ato) e a ordem em que o ato os guarda.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class AnexoDoAtoTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string OutroHash = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
    private static readonly AtoNormativoDeTeste Base = new();

    [Theory(DisplayName = "Hash que não é SHA-256 em hexadecimal minúsculo é recusado")]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\n")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
    public void Recusas_HashForaDoFormato_Recusa(string hash)
    {
        IReadOnlyList<FieldError> recusas = AnexoDoAto.Recusas([("Anexo I.pdf", "application/pdf", hash)]);

        ResumoDasRecusas.De(recusas).Should().Equal(("anexos[0].hash", AnexoDoAtoErrorCodes.HashInvalido));
    }

    [Theory(DisplayName = "Tipo de mídia fora da forma tipo/subtipo é recusado")]
    [InlineData("")]
    [InlineData("pdf")]
    [InlineData("application/")]
    public void Recusas_TipoMidiaInvalido_Recusa(string tipoMidia)
    {
        IReadOnlyList<FieldError> recusas = AnexoDoAto.Recusas([("Anexo I.pdf", tipoMidia, HashValido)]);

        ResumoDasRecusas.De(recusas).Should().Equal(("anexos[0].tipoMidia", AnexoDoAtoErrorCodes.TipoMidiaInvalido));
    }

    [Fact(DisplayName = "Registrar guarda os anexos na ordem recebida, com o tipo de mídia em minúsculas")]
    public void Registrar_ComAnexos_GuardaNaOrdemRecebida()
    {
        AtoNormativo ato = (Base with
        {
            Anexos = [("Anexo II.pdf", "Application/PDF", OutroHash), ("Anexo I.pdf", "application/pdf", HashValido)],
        }).Registrar();

        ato.Anexos.OrderBy(a => a.Ordem).Select(a => (a.Ordem, a.Nome, a.TipoMidia, a.Hash))
            .Should().Equal(
                (0, "Anexo II.pdf", "application/pdf", OutroHash),
                (1, "Anexo I.pdf", "application/pdf", HashValido));
        ato.Anexos.Should().OnlyContain(a => a.AtoId == ato.Id);
    }

    [Fact(DisplayName = "Registrar sem anexos deixa a lista vazia")]
    public void Registrar_SemAnexos_ListaVazia() =>
        Base.Registrar().Anexos.Should().BeEmpty();

    [Fact(DisplayName = "Registrar com anexo inválido lança — última linha das invariantes")]
    public void Registrar_ComAnexoInvalido_Lanca()
    {
        Action acao = () => (Base with { Anexos = [("Anexo I.pdf", "application/pdf", "abc")] }).Registrar();

        acao.Should().Throw<ArgumentException>();
    }
}
