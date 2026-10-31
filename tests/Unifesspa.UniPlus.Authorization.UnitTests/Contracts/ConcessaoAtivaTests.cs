namespace Unifesspa.UniPlus.Authorization.UnitTests.Contracts;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;

public sealed class ConcessaoAtivaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private static ConcessaoAtiva Concessao(DateTimeOffset? validaAte) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), "Perfil", null, true, validaAte, []);

    [Fact]
    public void VigenteEm_SemPrazo_EstaVigente() =>
        Concessao(null).VigenteEm(Agora).Should().BeTrue();

    [Fact]
    public void VigenteEm_PrazoPorVencer_EstaVigente() =>
        Concessao(Agora.AddTicks(1)).VigenteEm(Agora).Should().BeTrue();

    [Fact]
    public void VigenteEm_PrazoIgualAoInstanteDaConsulta_EstaVencida() =>
        Concessao(Agora).VigenteEm(Agora).Should().BeFalse();

    [Fact]
    public void VigenteEm_PrazoPassado_EstaVencida() =>
        Concessao(Agora.AddTicks(-1)).VigenteEm(Agora).Should().BeFalse();
}
