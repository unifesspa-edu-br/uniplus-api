namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>O documento do ato: o hash em SHA-256 e o assinante, na ordem do corpo.</summary>
public sealed class DocumentoDoAtoTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Assinante = PersonasDeTeste.NomeKevinCarvalho;

    [Fact(DisplayName = "Hash válido e assinante com espaços nas pontas são aceitos, com o assinante aparado")]
    public void ConferirDocumento_Valido_AceitoComAssinanteAparado()
    {
        ParteConferida<(string Hash, string Assinante)> parte =
            EssenciaDocumentalDoAto.ConferirDocumento(HashValido, $"  {Assinante} ");

        parte.Valor.Should().Be((HashValido, Assinante));
    }

    [Theory(DisplayName = "Hash que não é SHA-256 em hexadecimal minúsculo é recusado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\n")]
    public void ConferirDocumento_HashForaDoFormato_Recusa(string? hash)
    {
        ParteConferida<(string Hash, string Assinante)> parte = EssenciaDocumentalDoAto.ConferirDocumento(hash, Assinante);

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(("documentoHash", AtoNormativoErrorCodes.HashDoDocumentoInvalido));
        parte.Recusas[0].Error.Message.Should().Be(
            "O hash do documento deve ser um SHA-256 em hexadecimal minúsculo (64 caracteres).");
    }

    [Theory(DisplayName = "Assinante nulo ou em branco é recusado")]
    [InlineData(null)]
    [InlineData("  ")]
    public void ConferirDocumento_AssinanteVazio_Recusa(string? assinante)
    {
        ParteConferida<(string Hash, string Assinante)> parte = EssenciaDocumentalDoAto.ConferirDocumento(HashValido, assinante);

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(("assinante", AtoNormativoErrorCodes.TextoInvalido));
    }

    [Fact(DisplayName = "Assinante com 201 caracteres é recusado e com 200 é aceito")]
    public void ConferirDocumento_AssinanteNoLimite_RecusaAcimaDe200()
    {
        EssenciaDocumentalDoAto.ConferirDocumento(HashValido, new string('a', 201)).EstaAceita.Should().BeFalse();
        EssenciaDocumentalDoAto.ConferirDocumento(HashValido, new string('a', 200)).EstaAceita.Should().BeTrue();
    }

    [Fact(DisplayName = "Hash inválido e assinante vazio são recusados juntos, o hash primeiro")]
    public void ConferirDocumento_HashEAssinanteInvalidos_AcumulaNaOrdem()
    {
        ParteConferida<(string Hash, string Assinante)> parte = EssenciaDocumentalDoAto.ConferirDocumento("abc", "");

        ResumoDasRecusas.De(parte.Recusas).Should().Equal(
            ("documentoHash", AtoNormativoErrorCodes.HashDoDocumentoInvalido),
            ("assinante", AtoNormativoErrorCodes.TextoInvalido));
    }
}
