namespace Unifesspa.UniPlus.Authorization.UnitTests.Contracts;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class VinculosDaBancaTests
{
    [Fact(DisplayName = "Monta os vínculos com fase e banca, com ou sem etapa")]
    public void From_Valido_Monta()
    {
        Guid fase = Guid.CreateVersion7();
        Guid banca = Guid.CreateVersion7();

        VinculosDaBanca.From(fase, banca).Value!.EtapaId.Should().BeNull();
        VinculosDaBanca.From(fase, banca, Guid.CreateVersion7()).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Rejeita fase, banca ou etapa vazias")]
    public void From_GuidVazio_Rejeita()
    {
        Guid id = Guid.CreateVersion7();

        VinculosDaBanca.From(Guid.Empty, id).Error!.Code.Should().Be(AuthorizationErrorCodes.VinculosDaBancaInvalidos);
        VinculosDaBanca.From(id, Guid.Empty).IsFailure.Should().BeTrue();
        VinculosDaBanca.From(id, id, Guid.Empty).IsFailure.Should().BeTrue();
    }

    [Fact(DisplayName = "Os vínculos sobrevivem à troca de sensibilidade do resource")]
    public void ComSensibilidade_PreservaOsVinculos()
    {
        VinculosDaBanca vinculos = VinculosDaBanca.From(Guid.CreateVersion7(), Guid.CreateVersion7()).Value!;
        ResourceContext resource = ResourceContext.From("avaliacao-da-inscricao", ClassificacaoDeProtecao.Interno).Value!
            .ComVinculosDaBanca(vinculos);

        ResourceContext outro = resource.ComSensibilidade(ClassificacaoDeProtecao.Pessoal);

        outro.Banca.Should().Be(vinculos);
        outro.Sensibilidade.Should().Be(ClassificacaoDeProtecao.Pessoal);
    }
}
