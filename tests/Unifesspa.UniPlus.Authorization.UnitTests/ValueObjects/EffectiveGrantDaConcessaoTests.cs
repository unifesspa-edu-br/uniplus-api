namespace Unifesspa.UniPlus.Authorization.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Authorization.UnitTests.Decisao.CenariosDeDecisao;

public sealed class EffectiveGrantDaConcessaoTests
{
    private const string Permissao = "selecao.editais.publicar";

    private static readonly Guid VersaoId = Guid.CreateVersion7();

    private static ConcessaoAtiva Gravada(Guid? versaoId, Guid? unidadeId = null) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), versaoId, "Perfil", unidadeId, unidadeId is null, null, [Permissao]);

    [Fact(DisplayName = "A concessão efetiva leva a concessão, o perfil, o escopo e a versão da concessão gravada")]
    public void DaConcessao_LevaAOrigemEAVersao()
    {
        ConcessaoAtiva gravada = Gravada(VersaoId);

        EffectiveGrant grant = EffectiveGrant.DaConcessao(Permissao, gravada).Value!;

        grant.VersaoId.Should().Be(VersaoId);
        grant.GrantId.Should().Be(gravada.ConcessaoId);
        grant.PerfilId.Should().Be(gravada.PerfilId);
        grant.Fonte.Should().Be(FonteGrant.Concessao);
        grant.EscopoInstitucional.Should().BeTrue();
    }

    [Fact(DisplayName = "A concessão sem versão, como a de governança, fica sem versão")]
    public void DaConcessao_SemVersao_FicaSemVersao() =>
        EffectiveGrant.DaConcessao(Permissao, Gravada(null)).Value!.VersaoId.Should().BeNull();

    [Theory(DisplayName = "A permissão em branco é recusada sem efeito sobre as demais")]
    [InlineData(null)]
    [InlineData("  ")]
    public void DaConcessao_PermissaoEmBranco_Recusa(string? permissao)
    {
        Result<EffectiveGrant> resultado = EffectiveGrant.DaConcessao(permissao, Gravada(VersaoId));

        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.EffectiveGrantPermissaoObrigatoria);
    }

    [Fact(DisplayName = "O escopo de unidade vazio é recusado")]
    public void DaConcessao_EscopoInvalido_Recusa() =>
        EffectiveGrant.DaConcessao(Permissao, Gravada(VersaoId, Guid.Empty)).Error!.Code
            .Should().Be(AuthorizationErrorCodes.EffectiveGrantEscopoInvalido);

    [Fact(DisplayName = "O registro da decisão leva a versão da concessão usada e vai nulo sem ela")]
    public void Registro_LevaAVersaoDaConcessaoUsada()
    {
        ResourceContext resource = Resource(ClassificacaoDeProtecao.Pessoal);
        AuthorizationRequestContext requisicao = AuthorizationRequestContext.From("req-2277", Agora, OrigemRequisicao.Api).Value!;
        EffectiveGrant presa = EffectiveGrant.DaConcessao(Permissao, Gravada(VersaoId)).Value!;

        RegistroDecisaoAcesso comVersao = RegistroDecisaoAcesso.De(
            Sujeito(concessoes: presa), Requisito(resource.Sensibilidade), resource, requisicao, AuthorizationDecision.Permitir(presa));
        RegistroDecisaoAcesso semVersao = RegistroDecisaoAcesso.De(
            Sujeito(concessoes: Concessao()), Requisito(resource.Sensibilidade), resource, requisicao, AuthorizationDecision.Permitir(Concessao()));

        comVersao.VersaoId.Should().Be(VersaoId);
        semVersao.VersaoId.Should().BeNull();
    }
}
