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

    [Fact(DisplayName = "Com mais de 50 anexos e um inválido, a recusa é pela quantidade")]
    public void Conferir_AcimaDoLimiteComUmInvalido_RecusaPelaQuantidade()
    {
        (string, string, string)[] anexos =
            [("Anexo 0.pdf", "application/pdf", "abc"), .. Enumerable.Range(1, AnexoDoAto.QuantidadeMaxima).Select(i => ($"Anexo {i}.pdf", "application/pdf", HashValido))];

        AnexoDoAto.Conferir(anexos)!.Code.Should().Be(AnexoDoAtoErrorCodes.QuantidadeExcedida);
    }

    [Fact(DisplayName = "Anexo com nome, tipo de mídia e hash inválidos é recusado pelo nome")]
    public void Conferir_NomeTipoEHashInvalidos_RecusaPeloNome()
    {
        DomainError? erro = AnexoDoAto.Conferir([("  ", "pdf", "abc")]);

        erro!.Code.Should().Be(AnexoDoAtoErrorCodes.NomeInvalido);
    }

    [Fact(DisplayName = "Nome nulo é recusado pelo nome")]
    public void Conferir_NomeNulo_RecusaPeloNome()
    {
        DomainError? erro = AnexoDoAto.Conferir([(null!, "application/pdf", HashValido)]);

        erro!.Code.Should().Be(AnexoDoAtoErrorCodes.NomeInvalido);
    }

    [Fact(DisplayName = "Nome repetido antes de um hash inválido é recusado pelo nome duplicado")]
    public void Conferir_NomeDuplicadoAntesDeHashInvalido_RecusaPelaDuplicidade()
    {
        DomainError? erro = AnexoDoAto.Conferir(
        [
            ("A.pdf", "application/pdf", HashValido),
            ("A.pdf", "application/pdf", OutroHash),
            ("C.pdf", "application/pdf", "abc"),
        ]);

        erro!.Code.Should().Be(AnexoDoAtoErrorCodes.NomeDuplicado);
    }

    [Fact(DisplayName = "Hash inválido no primeiro anexo é recusado antes da duplicidade do segundo")]
    public void Conferir_HashInvalidoAntesDeNomeDuplicado_RecusaPeloHash()
    {
        DomainError? erro = AnexoDoAto.Conferir(
        [
            ("A.pdf", "application/pdf", "abc"),
            ("A.pdf", "application/pdf", OutroHash),
        ]);

        erro!.Code.Should().Be(AnexoDoAtoErrorCodes.HashInvalido);
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
