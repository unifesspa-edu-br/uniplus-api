namespace Unifesspa.UniPlus.Authorization.UnitTests.Contracts;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ResourceContextTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResourceContext_ResourceTipoVazio_Rejeita(string? resourceTipo)
    {
        Result<ResourceContext> resultado = ResourceContext.From(resourceTipo, ClassificacaoDeProtecao.Interno);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.ResourceContextResourceTipoObrigatorio);
    }

    [Theory]
    [InlineData("TiposDocumento")]
    [InlineData("tipo_documento")]
    [InlineData("tipo documento")]
    [InlineData("-tipo")]
    [InlineData("selecao:inscricoes:consultar")]
    [InlineData("situação")]
    [InlineData("  edital  ")]
    [InlineData("concessao\n")]
    public void ResourceContext_ResourceTipoForaDaConvencao_Rejeita(string resourceTipo)
    {
        Result<ResourceContext> resultado = ResourceContext.From(resourceTipo, ClassificacaoDeProtecao.Interno);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.ResourceContextResourceTipoForaDaConvencao);
    }

    [Theory]
    [InlineData("tipo-documento")]
    [InlineData(ResourceTipos.Operacao)]
    public void ResourceContext_ResourceTipoNaConvencao_Constroi(string resourceTipo)
    {
        Result<ResourceContext> resultado = ResourceContext.From(resourceTipo, ClassificacaoDeProtecao.Interno);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ResourceTipo.Should().Be(resourceTipo);
    }

    [Fact]
    public void ResourceContext_SensibilidadeNaoInformada_Rejeita()
    {
        Result<ResourceContext> resultado = ResourceContext.From("processo", ClassificacaoDeProtecao.Nenhuma);

        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.ResourceContextSensibilidadeInvalida);
    }

    [Theory]
    [InlineData("unidade")]
    [InlineData("processo")]
    [InlineData("chamada")]
    public void ResourceContext_EscopoGuidVazio_Rejeita(string escopo)
    {
        Result<ResourceContext> resultado = ResourceContext.From(
            "edital",
            ClassificacaoDeProtecao.Interno,
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
            "edital",
            ClassificacaoDeProtecao.Pessoal,
            unidadeProprietariaId: unidade,
            processoId: processo,
            chamadaId: chamada);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ResourceTipo.Should().Be("edital");
        resultado.Value.Sensibilidade.Should().Be(ClassificacaoDeProtecao.Pessoal);
        resultado.Value.UnidadeProprietariaId.Should().Be(unidade);
        resultado.Value.ProcessoId.Should().Be(processo);
        resultado.Value.ChamadaId.Should().Be(chamada);
    }

    [Fact]
    public void ResourceContext_SemEscopos_Constroi()
    {
        Result<ResourceContext> resultado = ResourceContext.From("inscricao", ClassificacaoDeProtecao.Publico);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.UnidadeProprietariaId.Should().BeNull();
        resultado.Value.ProcessoId.Should().BeNull();
        resultado.Value.ChamadaId.Should().BeNull();
    }
}
