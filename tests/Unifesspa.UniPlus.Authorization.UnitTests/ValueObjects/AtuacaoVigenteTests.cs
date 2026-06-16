namespace Unifesspa.UniPlus.Authorization.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AtuacaoVigenteTests
{
    [Fact]
    public void AtuacaoVigente_DadosValidos_Constroi()
    {
        Guid unidade = Guid.CreateVersion7();
        DateTimeOffset validoAte = DateTimeOffset.UtcNow.AddHours(8);

        Result<AtuacaoVigente> resultado = AtuacaoVigente.From(unidade, validoAte);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.UnidadeRepresentadaId.Should().Be(unidade);
        resultado.Value.ValidoAte.Should().Be(validoAte);
    }

    [Fact]
    public void AtuacaoVigente_UnidadeVazia_Rejeita()
    {
        Result<AtuacaoVigente> resultado = AtuacaoVigente.From(Guid.Empty, DateTimeOffset.UtcNow.AddHours(8));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.AtuacaoUnidadeObrigatoria);
    }
}
