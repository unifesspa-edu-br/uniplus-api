namespace Unifesspa.UniPlus.ArchTests.Fixtures;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;

/// <summary>
/// Contraprova da regra da titularidade só no registro de acesso (ADR-0166): os dois tipos montam, fora do verificador
/// de acesso, o que só ele pode montar, e têm de ser rejeitados.
/// </summary>
internal static class FixturesDeTitularidade
{
    internal static class MontaConcessaoComFonteEscolhida
    {
        public static EffectiveGrant? Montar() =>
            EffectiveGrant.From("autorizacao:pedidos:ler-proprios", FonteGrant.Titularidade).Value;
    }

    internal static class PermiteForaDoPontoDeDecisao
    {
        public static AuthorizationDecision Permitir(EffectiveGrant concessao) => AuthorizationDecision.Permitir(concessao);
    }
}
