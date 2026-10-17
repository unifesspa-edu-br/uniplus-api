namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

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

    [Theory(DisplayName = "Hash que não é SHA-256 em hexadecimal minúsculo é recusado")]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\n")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
    public void Conferir_HashForaDoFormato_Recusa(string hash)
    {
        DomainError? erro = AnexoDoAto.Conferir([("Anexo I.pdf", "application/pdf", hash)]);

        erro!.Code.Should().Be(AnexoDoAtoErrorCodes.HashInvalido);
    }

    [Theory(DisplayName = "Nome vazio ou só de espaços é recusado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Conferir_NomeVazio_Recusa(string nome)
    {
        DomainError? erro = AnexoDoAto.Conferir([(nome, "application/pdf", HashValido)]);

        erro!.Code.Should().Be(AnexoDoAtoErrorCodes.NomeInvalido);
    }

    [Fact(DisplayName = "Nome acima do limite é recusado")]
    public void Conferir_NomeLongoDemais_Recusa()
    {
        DomainError? erro = AnexoDoAto.Conferir(
            [(new string('x', AnexoDoAto.NomeMaxLength + 1), "application/pdf", HashValido)]);

        erro!.Code.Should().Be(AnexoDoAtoErrorCodes.NomeInvalido);
    }

    [Theory(DisplayName = "Tipo de mídia fora da forma tipo/subtipo é recusado")]
    [InlineData("")]
    [InlineData("pdf")]
    [InlineData("application/")]
    public void Conferir_TipoMidiaInvalido_Recusa(string tipoMidia)
    {
        DomainError? erro = AnexoDoAto.Conferir([("Anexo I.pdf", tipoMidia, HashValido)]);

        erro!.Code.Should().Be(AnexoDoAtoErrorCodes.TipoMidiaInvalido);
    }

    [Fact(DisplayName = "Dois anexos com o mesmo nome (após o trim) no mesmo ato são recusados")]
    public void Conferir_NomeDuplicado_Recusa()
    {
        DomainError? erro = AnexoDoAto.Conferir(
        [
            ("Anexo I.pdf", "application/pdf", HashValido),
            ("  Anexo I.pdf ", "application/pdf", OutroHash),
        ]);

        erro!.Code.Should().Be(AnexoDoAtoErrorCodes.NomeDuplicado);
    }

    [Fact(DisplayName = "Mais de 50 anexos no mesmo ato são recusados")]
    public void Conferir_AcimaDoLimite_Recusa()
    {
        (string, string, string)[] anexos =
            [.. Enumerable.Range(0, AnexoDoAto.QuantidadeMaxima + 1).Select(i => ($"Anexo {i}.pdf", "application/pdf", HashValido))];

        AnexoDoAto.Conferir(anexos)!.Code.Should().Be(AnexoDoAtoErrorCodes.QuantidadeExcedida);
        AnexoDoAto.Conferir(anexos[..AnexoDoAto.QuantidadeMaxima]).Should().BeNull();
    }

    [Fact(DisplayName = "Registrar guarda os anexos na ordem recebida, com o tipo de mídia em minúsculas")]
    public void Registrar_ComAnexos_GuardaNaOrdemRecebida()
    {
        AtoNormativo ato = Registrar(
            ("Anexo II.pdf", "Application/PDF", OutroHash),
            ("Anexo I.pdf", "application/pdf", HashValido));

        ato.Anexos.OrderBy(a => a.Ordem).Select(a => (a.Ordem, a.Nome, a.TipoMidia, a.Hash))
            .Should().Equal(
                (0, "Anexo II.pdf", "application/pdf", OutroHash),
                (1, "Anexo I.pdf", "application/pdf", HashValido));
        ato.Anexos.Should().OnlyContain(a => a.AtoId == ato.Id);
    }

    [Fact(DisplayName = "Registrar sem anexos deixa a lista vazia")]
    public void Registrar_SemAnexos_ListaVazia() =>
        Registrar().Anexos.Should().BeEmpty();

    [Fact(DisplayName = "Registrar com anexo inválido lança — última linha das invariantes")]
    public void Registrar_ComAnexoInvalido_Lanca()
    {
        Action acao = () => Registrar(("Anexo I.pdf", "application/pdf", "abc"));

        acao.Should().Throw<ArgumentException>();
    }

    private static AtoNormativo Registrar(params (string Nome, string TipoMidia, string Hash)[] anexos) =>
        AtoNormativo.Registrar(
            Guid.CreateVersion7(),
            "CEPS", "EDITAL", 2026, "13", "EDITAL_ABERTURA",
            congelaConfiguracao: false,
            efeitoIrreversivel: false,
            unicoPorObjeto: false,
            dataPublicacao: new DateOnly(2026, 3, 13),
            documentoHash: HashValido,
            assinante: "Jairo Belchior",
            registradoEm: new DateTimeOffset(2026, 3, 13, 19, 0, 0, TimeSpan.Zero),
            versaoInvocada: null,
            anexos: anexos);
}
