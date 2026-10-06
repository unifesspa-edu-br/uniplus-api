namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O formato do texto delega a máscara ao tipo de valor do Kernel (ADR-0136); o texto livre, que
/// pode conter qualquer coisa, nunca aparece nem parcialmente. A conferência da resposta no formato é
/// da avaliação do formulário.
/// </summary>
public sealed class FormatosTextoTests
{
    [Fact(DisplayName = "Todo formato do catálogo é conferido pela avaliação do formulário")]
    public void TodoFormato_EhConferidoPelaAvaliacao()
    {
        // O vocabulário vive aqui e a conferência, no projeto de regras: um formato novo sem a
        // conferência derrubaria a montagem do formulário que o usa.
        foreach (FormatoTexto formato in Enum.GetValues<FormatoTexto>().Where(static f => f != FormatoTexto.Nenhum))
        {
            Action conferir = () => _ = new FormatoDeTexto(FormatosTexto.ParaTokenCanonico(formato));

            conferir.Should().NotThrow($"o formato {formato} tem de ter conferência na avaliação");
        }
    }

    [Theory(DisplayName = "A máscara expõe só a parte que o formato permite; texto livre ou inválido some por inteiro")]
    [InlineData(FormatoTexto.Cpf, "529.982.247-25", "***.982.247-**")]
    [InlineData(FormatoTexto.Livre, "qualquer coisa", "***")]
    [InlineData(FormatoTexto.Cpf, "123", "***")]
    public void Mascarar_ExpoeSoOPermitido(FormatoTexto formato, string texto, string esperado) =>
        FormatosTexto.Mascarar(formato, texto).Should().Be(esperado);
}
