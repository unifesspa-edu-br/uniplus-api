namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// O formato do texto delega a validação e a máscara ao tipo de valor do Kernel (ADR-0136); o
/// texto livre, que pode conter qualquer coisa, nunca aparece nem parcialmente.
/// </summary>
public sealed class FormatosTextoTests
{
    [Theory(DisplayName = "Cada formato recusa o texto que o tipo de valor do Kernel recusa")]
    [InlineData(FormatoTexto.Cpf, "111.111.111-11", "Cpf.Invalido")]
    [InlineData(FormatoTexto.Email, "sem-arroba", "Email.Invalido")]
    [InlineData(FormatoTexto.Telefone, "3322-1234", "Telefone.Invalido")]
    [InlineData(FormatoTexto.Cep, "123", "Cep.Invalido")]
    [InlineData(FormatoTexto.NomePessoa, "Maria", "NomePessoa.Invalido")]
    [InlineData(FormatoTexto.Livre, "  ", "FatoCandidato.TextoLivreVazio")]
    public void Validar_TextoForaDoFormato_Recusa(FormatoTexto formato, string texto, string codigoEsperado) =>
        FormatosTexto.Validar(formato, texto).Error!.Code.Should().Be(codigoEsperado);

    [Theory(DisplayName = "A máscara expõe só a parte que o formato permite; texto livre ou inválido some por inteiro")]
    [InlineData(FormatoTexto.Cpf, "529.982.247-25", "***.982.247-**")]
    [InlineData(FormatoTexto.Livre, "qualquer coisa", "***")]
    [InlineData(FormatoTexto.Cpf, "123", "***")]
    public void Mascarar_ExpoeSoOPermitido(FormatoTexto formato, string texto, string esperado) =>
        FormatosTexto.Mascarar(formato, texto).Should().Be(esperado);
}
