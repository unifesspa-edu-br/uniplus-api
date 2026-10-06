namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// O formato do texto delega a máscara ao tipo de valor do Kernel (ADR-0136); o texto livre, que
/// pode conter qualquer coisa, nunca aparece nem parcialmente. A conferência da resposta no formato é
/// da avaliação do formulário.
/// </summary>
public sealed class FormatosTextoTests
{
    [Theory(DisplayName = "A máscara expõe só a parte que o formato permite; texto livre ou inválido some por inteiro")]
    [InlineData(FormatoTexto.Cpf, "529.982.247-25", "***.982.247-**")]
    [InlineData(FormatoTexto.Livre, "qualquer coisa", "***")]
    [InlineData(FormatoTexto.Cpf, "123", "***")]
    public void Mascarar_ExpoeSoOPermitido(FormatoTexto formato, string texto, string esperado) =>
        FormatosTexto.Mascarar(formato, texto).Should().Be(esperado);
}
