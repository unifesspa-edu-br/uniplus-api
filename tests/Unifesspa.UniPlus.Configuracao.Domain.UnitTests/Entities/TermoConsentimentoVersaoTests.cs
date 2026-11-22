namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class TermoConsentimentoVersaoTests
{
    private static readonly DateTimeOffset Agora = new(2027, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Promover deriva o hash do próprio conteúdo — a factory não aceita hash de fora")]
    public void Promover_DerivaHashDoConteudo_NaoAceitaHashExterno()
    {
        TermoConsentimentoVersao versaoA = TermoConsentimentoVersao.Promover(
            Guid.CreateVersion7(), 1, "Texto do termo", "Lei 13.709/2018", FormaAceite.RegistroDigitalSemLogIp,
            Agora, "usuario.revisor");

        TermoConsentimentoVersao versaoB = TermoConsentimentoVersao.Promover(
            Guid.CreateVersion7(), 1, "Texto do termo", "Lei 13.709/2018", FormaAceite.RegistroDigitalSemLogIp,
            Agora, "usuario.revisor");

        versaoA.Hash.Should().NotBeNullOrWhiteSpace();
        versaoA.Hash.Should().HaveLength(64, "hash é SHA-256 hex (64 caracteres)");

        versaoA.Hash.Should().Be(versaoB.Hash);
    }

    [Theory(DisplayName = "Cada campo do conteúdo altera o hash")]
    [InlineData("Texto B", "Base legal A", FormaAceite.RegistroDigitalSemLogIp)]
    [InlineData("Texto A", "Base legal B", FormaAceite.RegistroDigitalSemLogIp)]
    [InlineData("Texto A", "Base legal A", FormaAceite.RegistroDigitalComLogIp)]
    public void Promover_CampoAlterado_HashDiferente(string texto, string baseLegal, FormaAceite formaAceite)
    {
        TermoConsentimentoVersao original = TermoConsentimentoVersao.Promover(
            Guid.CreateVersion7(), 1, "Texto A", "Base legal A", FormaAceite.RegistroDigitalSemLogIp, Agora, "usuario.revisor");
        TermoConsentimentoVersao alterada = TermoConsentimentoVersao.Promover(
            Guid.CreateVersion7(), 1, texto, baseLegal, formaAceite, Agora, "usuario.revisor");

        original.Hash.Should().Be("1a500092c8330a8b1c9ec3e9a948718f2161f093117282597e4e2edc38f5d70d");
        alterada.Hash.Should().NotBe(original.Hash);
    }

    [Fact(DisplayName = "Texto e base legal decompostos são guardados em NFC, e o hash é o do conteúdo guardado")]
    public void Promover_Decomposto_GuardaNfcComHashDoGuardado()
    {
        // "declaração" com o "ç" e o "ã" decompostos (letra + diacrítico combinante).
        const string decomposto = "declarac\u0327a\u0303o";

        TermoConsentimentoVersao decompostaVersao = TermoConsentimentoVersao.Promover(
            Guid.CreateVersion7(), 1, decomposto, decomposto, FormaAceite.RegistroDigitalSemLogIp, Agora, "usuario.revisor");
        TermoConsentimentoVersao composta = TermoConsentimentoVersao.Promover(
            Guid.CreateVersion7(), 1, "declaração", "declaração", FormaAceite.RegistroDigitalSemLogIp, Agora, "usuario.revisor");

        decompostaVersao.Texto.Should().Be("declaração");
        decompostaVersao.BaseLegal.Should().Be("declaração");
        decompostaVersao.Hash.Should().Be(composta.Hash, "o hash cobre o conteúdo que a publicação congela, em NFC");
    }
}
