namespace Unifesspa.UniPlus.Authorization.UnitTests.Contracts;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class PermissionRequirementTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PermissionRequirement_PermissaoVazia_Rejeita(string? permissao)
    {
        Result<PermissionRequirement> resultado = PermissionRequirement.From(permissao, ClassificacaoDeProtecao.Interno);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.PermissionRequirementPermissaoObrigatoria);
    }

    [Fact]
    public void PermissionRequirement_SensibilidadeNaoInformada_Rejeita()
    {
        Result<PermissionRequirement> resultado = PermissionRequirement.From("selecao:processos:consultar", default(ClassificacaoDeProtecao));

        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.PermissionRequirementSensibilidadeInvalida);
    }

    [Fact]
    public void PermissionRequirement_PermissaoComEspacos_GuardaAparada()
    {
        PermissionRequirement requisito = RequisitoCompleto("  selecao.resultado.homologar  ");

        requisito.Permissao.Should().Be("selecao.resultado.homologar", "a permissão é normalizada com Trim");
    }

    [Fact]
    public void PermissionRequirement_ShapeCompleto_GuardaOsMetadados()
    {
        PermissionRequirement requisito = RequisitoCompleto("selecao.resultado.homologar");

        requisito.Sensibilidade.Should().Be(ClassificacaoDeProtecao.Sensivel);
        requisito.BaseLegalPadrao.Should().Be("LGPD art. 7º, II");
        (requisito.RequerMfa, requisito.RequerDuplaAprovacao).Should().Be((true, true));
        requisito.EscopoContextoObrigatorio.Should().ContainSingle().Which.Should().Be("processoId");
        requisito.VerificacoesDeContexto.Should().Equal("fase_aberta", "estado_resource_compativel");
    }

    [Fact]
    public void PermissionRequirement_BaseLegalEColecoesNulas_ViramVaziasNaoNulas()
    {
        Result<PermissionRequirement> resultado = PermissionRequirement.From(
            "configuracao.cota.listar", ClassificacaoDeProtecao.Publico);

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
            "selecao.editais.publicar", ClassificacaoDeProtecao.Interno, escopoContextoObrigatorio: escopos);

        escopos.Add("processoId"); // muta a origem após a construção

        resultado.Value!.EscopoContextoObrigatorio.Should().ContainSingle("a cópia é defensiva e imutável");
    }

    private static PermissionRequirement RequisitoCompleto(string permissao) =>
        PermissionRequirement.From(
            permissao,
            ClassificacaoDeProtecao.Sensivel,
            baseLegalPadrao: "LGPD art. 7º, II",
            requerMfa: true,
            requerDuplaAprovacao: true,
            escopoContextoObrigatorio: ["processoId"],
            verificacoesDeContexto: ["fase_aberta", "estado_resource_compativel"]).Value!;
}
