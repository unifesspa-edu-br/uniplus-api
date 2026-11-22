namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Fixa o resumo SHA-256 da versão do termo de consentimento (texto, base legal e forma de aceite)
/// para cada forma. É outro resumo que o do conteúdo congelado do processo seletivo: este nasce na
/// promoção da versão e é gravado junto dela; o que muda aqui altera o resumo de toda versão já promovida.
/// </summary>
public sealed class ResumoDaVersaoDoTermoTests
{
    private const string Texto = "Declaro que li e aceito os termos de uso dos meus dados.";
    private const string BaseLegal = "Lei 13.709/2018, art. 7º, I";

    private static readonly DateTimeOffset PromovidaEm = new(2027, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory(DisplayName = "Promover calcula o resumo da versão com o texto da forma de aceite, igual ao literal fixado")]
    [InlineData(FormaAceite.ADefinir, "eaa904e168d78d479bed16a975db0ee02523520ade5933b733fefeec12c57728")]
    [InlineData(FormaAceite.RegistroDigitalSemLogIp, "819ba5d2c9846d3b21e901b39033a0e4930b0d2db5c37a70fb48ca3d3880c85b")]
    [InlineData(FormaAceite.RegistroDigitalComLogIp, "9c3417d55a91e6ccc867a1b3dadbff2fad6d8ec741de226f2a0bc9e3ea05ef3b")]
    public void Promover_FormaDeAceite_ResumoIgualAoLiteral(FormaAceite formaAceite, string resumoEsperado)
    {
        TermoConsentimentoVersao versao = TermoConsentimentoVersao.Promover(
            new Guid("0199a000-0000-7000-8000-00000000a001"), 1, Texto, BaseLegal, formaAceite, PromovidaEm, "usuario.revisor");

        versao.Hash.Should().Be(resumoEsperado);
    }
}
