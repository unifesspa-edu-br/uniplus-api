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
        Result<ResourceContext> resultado = ResourceContext.From("processo", default(ClassificacaoDeProtecao));

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
        (Guid unidade, Guid processo, Guid chamada) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        ResourceContext resource = ComOsTresEscopos(unidade, processo, chamada);

        resource.ResourceTipo.Should().Be("edital");
        resource.Sensibilidade.Should().Be(ClassificacaoDeProtecao.Pessoal);
        (resource.UnidadeProprietariaId, resource.ProcessoId, resource.ChamadaId).Should().Be((unidade, processo, chamada));
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

    [Fact(DisplayName = "O campo do registro alvo leva o código do fato e a ocorrência, e mantém o resto do resource")]
    public void ComCampo_FatoEOcorrencia_LevaOsDoisEMantemOResource()
    {
        ResourceContext resource = ComOsTresEscopos(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        ResourceContext comCampo = resource.ComCampo("CPF", "membro-1").Value!;

        (comCampo.FatoCodigo, comCampo.Ocorrencia).Should().Be(("CPF", "membro-1"));
        comCampo.Should().BeEquivalentTo(resource, opcoes => opcoes.Excluding(c => c.FatoCodigo).Excluding(c => c.Ocorrencia));
        (resource.FatoCodigo, resource.Ocorrencia).Should().Be(((string?)null, (string?)null));
    }

    [Fact(DisplayName = "O campo fora de grupo que se repete não leva ocorrência")]
    public void ComCampo_SemOcorrencia_FicaSemOcorrencia()
    {
        ResourceContext comCampo = ResourceContext.From("inscricao", ClassificacaoDeProtecao.Identificador).Value!.ComCampo("CPF", null).Value!;

        comCampo.FatoCodigo.Should().Be("CPF");
        comCampo.Ocorrencia.Should().BeNull();
    }

    [Theory(DisplayName = "Código do fato vazio ou em branco é recusado")]
    [InlineData("")]
    [InlineData(" ")]
    public void ComCampo_FatoEmBranco_Recusa(string fatoCodigo)
    {
        Result<ResourceContext> resultado = ResourceContext.From("inscricao", ClassificacaoDeProtecao.Interno).Value!.ComCampo(fatoCodigo, null);

        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.ResourceContextFatoInvalido);
    }

    [Theory(DisplayName = "Ocorrência informada e em branco é recusada")]
    [InlineData("")]
    [InlineData(" ")]
    public void ComCampo_OcorrenciaEmBranco_Recusa(string ocorrencia)
    {
        Result<ResourceContext> resultado = ResourceContext.From("inscricao", ClassificacaoDeProtecao.Interno).Value!.ComCampo("CPF", ocorrencia);

        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.ResourceContextOcorrenciaInvalida);
    }

    [Fact(DisplayName = "A recusa do tipo vem antes da da sensibilidade, e a desta antes da dos escopos")]
    public void ResourceContext_VariasRecusas_ApontaAPrimeiraNaOrdem()
    {
        ResourceContext.From(null, default(ClassificacaoDeProtecao), Guid.Empty).Error!.Code
            .Should().Be(AuthorizationErrorCodes.ResourceContextResourceTipoObrigatorio);
        ResourceContext.From("edital", default(ClassificacaoDeProtecao), Guid.Empty).Error!.Code
            .Should().Be(AuthorizationErrorCodes.ResourceContextSensibilidadeInvalida);
    }

    [Theory(DisplayName = "O registro alvo e a inscrição informados como Guid vazio são recusados")]
    [InlineData("resourceId")]
    [InlineData("inscricaoId")]
    public void ResourceContext_RegistroEInscricaoGuidVazio_Rejeita(string escopo)
    {
        Result<ResourceContext> resultado = ResourceContext.From(
            "inscricao",
            ClassificacaoDeProtecao.Interno,
            resourceId: escopo == "resourceId" ? Guid.Empty : null,
            inscricaoId: escopo == "inscricaoId" ? Guid.Empty : null);

        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.ResourceContextEscopoInvalido);
    }

    private static ResourceContext ComOsTresEscopos(Guid unidade, Guid processo, Guid chamada) =>
        ResourceContext.From(
            "edital",
            ClassificacaoDeProtecao.Pessoal,
            unidadeProprietariaId: unidade,
            processoId: processo,
            chamadaId: chamada).Value!;
}
