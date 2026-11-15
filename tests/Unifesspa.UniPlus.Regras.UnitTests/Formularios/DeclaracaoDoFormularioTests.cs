namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O que o formulário declara de si (UNI-REQ-0144): se serve à fase que coleta a inscrição e se é
/// obrigatório. Ele coleta a inscrição quando serve a essa fase e é obrigatório.
/// </summary>
public sealed class DeclaracaoDoFormularioTests
{
    [Theory(DisplayName = "O formulário coleta a inscrição só quando serve à fase de inscrição e é obrigatório")]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ColetaAInscricao_ExigeAsDuasDeclaracoes(bool serveAFaseDeInscricao, bool obrigatorio, bool esperado)
    {
        DeclaracaoDoFormulario declaracao = new(serveAFaseDeInscricao, obrigatorio);

        declaracao.ColetaAInscricao.Should().Be(esperado);
    }

    [Fact(DisplayName = "A declaração do formulário que o candidato responde para se inscrever serve à inscrição e é obrigatória")]
    public void DaInscricao_ServeEEObrigatoria()
    {
        DeclaracaoDoFormulario.DaInscricao.Should().Be(new DeclaracaoDoFormulario(ServeAFaseDeInscricao: true, Obrigatorio: true));
        DeclaracaoDoFormulario.DaInscricao.ColetaAInscricao.Should().BeTrue();
    }
}
