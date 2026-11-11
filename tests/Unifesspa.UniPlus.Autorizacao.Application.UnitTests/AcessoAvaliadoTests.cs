namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;

public sealed class AcessoAvaliadoTests
{
    [Fact]
    public void Recusa_Permitido_NaoRecusa() =>
        AcessoAvaliado.Permitido.Recusa(PerfilErros.LeituraNegada).Should().BeNull();

    [Fact]
    public void Recusa_Negado_UsaARecusaDaOperacao() =>
        AcessoAvaliado.Negado.Recusa(PerfilErros.LeituraNegada)!.Code.Should().Be(PerfilErrorCodes.LeituraNegada);

    [Fact]
    public void Recusa_SemAutorIdentificado_RecusaPeloToken() =>
        AcessoAvaliado.SemAutorIdentificado.Recusa(PerfilErros.LeituraNegada)!.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
}
