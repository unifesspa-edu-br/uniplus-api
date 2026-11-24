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
    [InlineData(FormaAceite.RegistroSemEnderecoIp, "7ea3c78f8ec02f4c29e490892c632ff777e502234ba20ef29a1ec0cd24948280")]
    [InlineData(FormaAceite.RegistroComEnderecoIp, "9d4ca81eed6bd3fbf328733b7427de93254c0f39c6ce95fa7fdc8af617c01157")]
    public void Promover_FormaDeAceite_ResumoIgualAoLiteral(FormaAceite formaAceite, string resumoEsperado)
    {
        TermoConsentimentoVersao versao = TermoConsentimentoVersao.Promover(
            new Guid("0199a000-0000-7000-8000-00000000a001"), 1, Texto, BaseLegal, formaAceite, PromovidaEm, "usuario.revisor");

        versao.Hash.Should().Be(resumoEsperado);
    }
}
