namespace Unifesspa.UniPlus.Authorization.UnitTests.Contracts;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class PermissionRequirementTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PermissionRequirement_PermissaoVazia_Rejeita(string? permissao)
    {
        Result<PermissionRequirement> resultado = PermissionRequirement.From(permissao, Sensibilidade.Interna);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.PermissionRequirementPermissaoObrigatoria);
    }

    [Fact]
    public void PermissionRequirement_ShapeCompleto_Constroi()
    {
        Result<PermissionRequirement> resultado = PermissionRequirement.From(
            "  selecao.resultado.homologar  ",
            Sensibilidade.Sensivel,
            baseLegalPadrao: "LGPD art. 7º, II",
            requerMfa: true,
            requerDuplaAprovacao: true,
            escopoContextoObrigatorio: ["processoId"],
            verificacoesDeContexto: ["fase_aberta", "estado_recurso_compativel"]);

        resultado.IsSuccess.Should().BeTrue();
        PermissionRequirement req = resultado.Value!;
        req.Permissao.Should().Be("selecao.resultado.homologar", "a permissão é normalizada com Trim");
        req.Sensibilidade.Should().Be(Sensibilidade.Sensivel);
        req.BaseLegalPadrao.Should().Be("LGPD art. 7º, II");
        req.RequerMfa.Should().BeTrue();
        req.RequerDuplaAprovacao.Should().BeTrue();
        req.EscopoContextoObrigatorio.Should().ContainSingle().Which.Should().Be("processoId");
        req.VerificacoesDeContexto.Should().Equal("fase_aberta", "estado_recurso_compativel");
    }

    [Fact]
    public void PermissionRequirement_BaseLegalEColecoesNulas_ViramVaziasNaoNulas()
    {
        Result<PermissionRequirement> resultado = PermissionRequirement.From(
            "configuracao.cota.listar", Sensibilidade.Publica);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.BaseLegalPadrao.Should().BeEmpty("dado público pode não ter base legal");
        resultado.Value.EscopoContextoObrigatorio.Should().BeEmpty();
        resultado.Value.VerificacoesDeContexto.Should().BeEmpty();
    }

    [Fact]
    public void PermissionRequirement_CopiaDefensivaDeColecoes_NaoRefleteMutacaoExterna()
    {
        var escopos = new List<string> { "unidadeProprietariaId" };

        Result<PermissionRequirement> resultado = PermissionRequirement.From(
            "selecao.editais.publicar", Sensibilidade.Interna, escopoContextoObrigatorio: escopos);

        escopos.Add("processoId"); // muta a origem após a construção

        resultado.Value!.EscopoContextoObrigatorio.Should().ContainSingle("a cópia é defensiva e imutável");
    }
}
