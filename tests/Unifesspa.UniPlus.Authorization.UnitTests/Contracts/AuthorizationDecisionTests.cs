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
        => EffectiveGrant.From(Permissao, FonteGrant.Token).Value!;

    // ─── CA-03: permitida registra o grant usado (contraprova) ─────────────

    [Fact]
    public void AuthorizationDecision_Permitida_RegistraGrantUsed()
    {
        EffectiveGrant grant = Grant();

        AuthorizationDecision decisao = AuthorizationDecision.Permitir(grant);

        decisao.Allowed.Should().BeTrue();
        decisao.GrantUsed.Should().Be(grant, "a auditoria precisa saber qual concessão autorizou");
        decisao.DenyReason.Should().BeNull();
    }

    // ─── CA-03: permitida sem grant é rejeitada pela fábrica (prova negativa) ──

    [Fact]
    public void AuthorizationDecision_PermitidaSemGrantUsed_Rejeita()
    {
        Action act = () => AuthorizationDecision.Permitir(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ─── CA-04: permitida nunca carrega motivo de negativa (prova negativa) ──

    [Fact]
    public void AuthorizationDecision_PermitidaComDenyReason_Rejeita()
    {
        // O invariante é estrutural: a fábrica Permitir não oferece caminho para
        // injetar um DenyReason numa decisão permitida — ela sempre o zera.
        AuthorizationDecision decisao = AuthorizationDecision.Permitir(Grant());

        decisao.Allowed.Should().BeTrue();
        decisao.DenyReason.Should().BeNull("uma decisão permitida não pode carregar motivo de negativa");
    }

    // ─── CA-04: negada carrega código fechado, sem texto livre (contraprova) ──

    [Fact]
    public void AuthorizationDecision_Negada_CarregaCodigoFechadoSemTextoLivre()
    {
        AuthorizationDecision decisao = AuthorizationDecision.Negar(MotivoNegativa.FaseFechada);

        decisao.Allowed.Should().BeFalse();
        decisao.DenyReason.Should().NotBeNull();
        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.FaseFechada);
        decisao.GrantUsed.Should().BeNull("uma decisão negada nunca registra concessão usada");

        // Sem campo de texto livre: a única informação do motivo é o código do
        // conjunto fechado — estruturalmente incapaz de veicular PII.
        typeof(DenyReason).GetProperties()
            .Should().ContainSingle()
            .Which.PropertyType.Should().Be<MotivoNegativa>();
    }

    // ─── CA-04: negada sem código válido é rejeitada (prova negativa) ──────

    [Fact]
    public void AuthorizationDecision_NegadaSemMotivo_Rejeita()
    {
        // "Sem motivo" = código fora do conjunto fechado; a fábrica recusa.
        Action act = () => AuthorizationDecision.Negar((MotivoNegativa)999);

        act.Should().Throw<InvalidEnumArgumentException>();
    }
}
