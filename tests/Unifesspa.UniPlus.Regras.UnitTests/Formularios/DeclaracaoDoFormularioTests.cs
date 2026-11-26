namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O que o formulário declara de si (UNI-REQ-0144): se serve à fase que coleta a inscrição e se é
/// obrigatório.
/// </summary>
public sealed class DeclaracaoDoFormularioTests
{
    [Fact(DisplayName = "A declaração do formulário que o candidato responde para se inscrever serve à inscrição e é obrigatória")]
    public void DaInscricao_ServeEEObrigatoria() =>
        DeclaracaoDoFormulario.DaInscricao.Should().Be(new DeclaracaoDoFormulario(ServeAFaseDeInscricao: true, Obrigatorio: true));
}
