namespace Unifesspa.UniPlus.Authorization.UnitTests.Contracts;

using System.ComponentModel;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;

public sealed class AuthorizationDecisionTests
{
    private const string Permissao = "selecao.editais.publicar";

    private static EffectiveGrant Grant()
        => EffectiveGrant.DaConcessaoDePerfil(Permissao, EscopoDaPermissaoEfetiva.Instituicao).Value!;


    [Fact]
    public void AuthorizationDecision_Permitida_RegistraGrantUsed()
    {
        EffectiveGrant grant = Grant();

        AuthorizationDecision decisao = AuthorizationDecision.Permitir(grant);

        decisao.Allowed.Should().BeTrue();
        decisao.GrantUsed.Should().Be(grant, "a auditoria precisa saber qual concessão autorizou");
        decisao.DenyReason.Should().BeNull();
    }


    [Fact]
    public void AuthorizationDecision_PermitidaSemGrantUsed_Rejeita()
    {
        Action act = () => AuthorizationDecision.Permitir(null!);

        act.Should().Throw<ArgumentNullException>();
    }


    [Fact]
    public void AuthorizationDecision_PermitidaComDenyReason_Rejeita()
    {
        // O invariante é estrutural: a fábrica Permitir não oferece caminho para
        // injetar um DenyReason numa decisão permitida — ela sempre o zera.
        AuthorizationDecision decisao = AuthorizationDecision.Permitir(Grant());

        decisao.Allowed.Should().BeTrue();
        decisao.DenyReason.Should().BeNull("uma decisão permitida não pode carregar motivo de negativa");
    }


    [Fact]
    public void AuthorizationDecision_Negada_CarregaCodigoFechadoSemTextoLivre()
    {
        AuthorizationDecision decisao = AuthorizationDecision.Negar(MotivoNegativa.FaseFechada);

        decisao.Allowed.Should().BeFalse();
        decisao.DenyReason.Should().NotBeNull();
        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.FaseFechada);
        decisao.GrantUsed.Should().BeNull("uma decisão negada nunca registra concessão usada");
    }


    [Fact]
    public void AuthorizationDecision_NegadaSemMotivo_Rejeita()
    {
        // "Sem motivo" = código fora do conjunto fechado; a fábrica recusa.
        Action act = () => AuthorizationDecision.Negar((MotivoNegativa)999);

        act.Should().Throw<InvalidEnumArgumentException>();
    }
}
