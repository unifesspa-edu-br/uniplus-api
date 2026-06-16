namespace Unifesspa.UniPlus.Authorization.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class EscopoAuditoriaVigenteTests
{
    [Fact]
    public void EscopoAuditoriaVigente_DadosValidos_Constroi()
    {
        Guid escopoId = Guid.CreateVersion7();
        Guid unidadeId = Guid.CreateVersion7();
        DateTimeOffset validoAte = DateTimeOffset.UtcNow.AddDays(30);

        Result<EscopoAuditoriaVigente> resultado = EscopoAuditoriaVigente.From(escopoId, validoAte, unidadeId);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.EscopoId.Should().Be(escopoId);
        resultado.Value.UnidadeId.Should().Be(unidadeId);
        resultado.Value.ValidoAte.Should().Be(validoAte);
    }

    [Fact]
    public void EscopoAuditoriaVigente_UnidadeOpcional_AceitaNulo()
    {
        Result<EscopoAuditoriaVigente> resultado =
            EscopoAuditoriaVigente.From(Guid.CreateVersion7(), DateTimeOffset.UtcNow.AddDays(30));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.UnidadeId.Should().BeNull();
    }

    [Fact]
    public void EscopoAuditoriaVigente_EscopoVazio_Rejeita()
    {
        Result<EscopoAuditoriaVigente> resultado =
            EscopoAuditoriaVigente.From(Guid.Empty, DateTimeOffset.UtcNow.AddDays(30));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.EscopoAuditoriaEscopoObrigatorio);
    }
}
