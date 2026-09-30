namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public sealed class TermoConsentimentoVersaoTests
{
    private static readonly DateTimeOffset Agora = new(2027, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Promover deriva o hash do próprio conteúdo — a factory não aceita hash de fora")]
    public void Promover_DerivaHashDoConteudo_NaoAceitaHashExterno()
    {
        // Tornar a factory pública (exigência do
        // fitness test de entidades forenses, ADR-0063) abriria brecha para um
        // chamador gravar um hash divergente do conteúdo se a factory ainda
        // aceitasse `hash` por parâmetro. A assinatura atual não tem esse
        // parâmetro — o hash só pode vir do cálculo interno.
        TermoConsentimentoVersao versaoA = TermoConsentimentoVersao.Promover(
            Guid.CreateVersion7(), "Texto do termo", "Lei 13.709/2018", FormaAceite.RegistroDigitalSemLogIp,
            Agora, "usuario.revisor");

        TermoConsentimentoVersao versaoB = TermoConsentimentoVersao.Promover(
            Guid.CreateVersion7(), "Texto do termo", "Lei 13.709/2018", FormaAceite.RegistroDigitalSemLogIp,
            Agora, "usuario.revisor");

        versaoA.Hash.Should().NotBeNullOrWhiteSpace();
        versaoA.Hash.Should().HaveLength(64, "hash é SHA-256 hex (64 caracteres)");

        // Mesmo conteúdo semântico, IDs distintos (TermoConsentimentoId não entra
        // no hash) — mesmo hash, provando que ele deriva só de texto/base
        // legal/forma de aceite, calculado dentro da própria factory.
        versaoA.Hash.Should().Be(versaoB.Hash);
    }

    [Fact(DisplayName = "Promover com conteúdo diferente produz hash diferente")]
    public void Promover_ConteudoDiferente_HashDiferente()
    {
        TermoConsentimentoVersao versaoA = TermoConsentimentoVersao.Promover(
            Guid.CreateVersion7(), "Texto A", "Base legal A", FormaAceite.RegistroDigitalSemLogIp,
            Agora, "usuario.revisor");

        TermoConsentimentoVersao versaoB = TermoConsentimentoVersao.Promover(
            Guid.CreateVersion7(), "Texto B", "Base legal B", FormaAceite.RegistroDigitalSemLogIp,
            Agora, "usuario.revisor");

        versaoA.Hash.Should().NotBe(versaoB.Hash);
    }

    [Fact(DisplayName = "Texto e base legal decompostos são guardados em NFC, e o hash é o do conteúdo guardado")]
    public void Promover_Decomposto_GuardaNfcComHashDoGuardado()
    {
        // "declaração" com o "ç" e o "ã" decompostos (letra + diacrítico combinante).
        const string decomposto = "declarac\u0327a\u0303o";

        TermoConsentimentoVersao decompostaVersao = TermoConsentimentoVersao.Promover(
            Guid.CreateVersion7(), decomposto, decomposto, FormaAceite.RegistroDigitalSemLogIp, Agora, "usuario.revisor");
        TermoConsentimentoVersao composta = TermoConsentimentoVersao.Promover(
            Guid.CreateVersion7(), "declaração", "declaração", FormaAceite.RegistroDigitalSemLogIp, Agora, "usuario.revisor");

        decompostaVersao.Texto.Should().Be("declaração");
        decompostaVersao.BaseLegal.Should().Be("declaração");
        decompostaVersao.Hash.Should().Be(composta.Hash, "o hash cobre o conteúdo que a publicação congela, em NFC");
    }
}
