namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Contracts;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;

public sealed class FaseCanonicaLidaPelaSelecaoTests
{
    [Theory(DisplayName = "O contrato que a Seleção lê da fase canônica não carrega a unidade sugerida")]
    [InlineData(typeof(FaseCanonicaView))]
    [InlineData(typeof(FaseCanonicaSnapshot))]
    public void Contrato_NaoCarregaAUnidadeSugerida(Type contrato)
    {
        ArgumentNullException.ThrowIfNull(contrato);

        contrato.GetProperties().Select(p => p.Name)
            .Should().NotContain(nome => nome.Contains("Unidade", StringComparison.Ordinal),
                "a Seleção resolve a unidade responsável no cadastro e nunca lê a sugestão copiada na fase canônica");
    }
}
