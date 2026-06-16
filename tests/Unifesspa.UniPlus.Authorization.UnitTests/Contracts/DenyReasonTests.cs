namespace Unifesspa.UniPlus.Authorization.UnitTests.Contracts;

using System.ComponentModel;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;

public sealed class DenyReasonTests
{
    // ─── CA-04: código fechado — valor fora do conjunto é rejeitado ────────

    [Fact]
    public void DenyReason_CodigoForaDoConjuntoFechado_Rejeita()
    {
        // Cast de um inteiro arbitrário não pertence ao conjunto fechado.
        Action act = () => DenyReason.De((MotivoNegativa)999);

        act.Should().Throw<InvalidEnumArgumentException>();
    }

    // ─── CA-04: contraprova — código válido constrói ───────────────────────

    [Fact]
    public void DenyReason_CodigoValido_Constroi()
    {
        DenyReason motivo = DenyReason.De(MotivoNegativa.SemConcessaoAplicavel);

        motivo.Codigo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
    }
}
