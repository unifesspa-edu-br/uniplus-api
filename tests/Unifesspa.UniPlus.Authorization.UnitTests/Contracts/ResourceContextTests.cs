namespace Unifesspa.UniPlus.Authorization.UnitTests.Contracts;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ResourceContextTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResourceContext_RecursoTipoVazio_Rejeita(string? recursoTipo)
    {
        Result<ResourceContext> resultado = ResourceContext.From(recursoTipo, Sensibilidade.Interna);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.ResourceContextRecursoTipoObrigatorio);
    }

    [Theory]
    [InlineData("unidade")]
    [InlineData("processo")]
    [InlineData("chamada")]
    public void ResourceContext_EscopoGuidVazio_Rejeita(string escopo)
    {
        Result<ResourceContext> resultado = ResourceContext.From(
            "Edital",
            Sensibilidade.Interna,
            unidadeProprietariaId: escopo == "unidade" ? Guid.Empty : null,
            processoId: escopo == "processo" ? Guid.Empty : null,
            chamadaId: escopo == "chamada" ? Guid.Empty : null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.ResourceContextEscopoInvalido);
    }

    [Fact]
    public void ResourceContext_ComEscoposEValores_Constroi()
    {
        Guid unidade = Guid.CreateVersion7();
        Guid processo = Guid.CreateVersion7();
        Guid chamada = Guid.CreateVersion7();

        Result<ResourceContext> resultado = ResourceContext.From(
            "  Edital  ",
            Sensibilidade.Pessoal,
            unidadeProprietariaId: unidade,
            processoId: processo,
            chamadaId: chamada);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.RecursoTipo.Should().Be("Edital", "o tipo do recurso é normalizado com Trim");
        resultado.Value.Sensibilidade.Should().Be(Sensibilidade.Pessoal);
        resultado.Value.UnidadeProprietariaId.Should().Be(unidade);
        resultado.Value.ProcessoId.Should().Be(processo);
        resultado.Value.ChamadaId.Should().Be(chamada);
    }

    [Fact]
    public void ResourceContext_SemEscopos_Constroi()
    {
        Result<ResourceContext> resultado = ResourceContext.From("Inscricao", Sensibilidade.Publica);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.UnidadeProprietariaId.Should().BeNull();
        resultado.Value.ProcessoId.Should().BeNull();
        resultado.Value.ChamadaId.Should().BeNull();
    }
}
