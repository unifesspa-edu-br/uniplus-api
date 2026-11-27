namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Fada;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fada;

using Xunit;

public sealed class DeclaracaoDoFadaTests
{
    [Theory(DisplayName = "A declaração e a configuração do FADA que se contradizem são recusadas; as que concordam não")]
    [InlineData(true, false, ProcessoSeletivoErrorCodes.FadaAplicaSemConfiguracao)]
    [InlineData(false, true, ProcessoSeletivoErrorCodes.FadaNaoAplicaComConfiguracao)]
    [InlineData(true, true, null)]
    [InlineData(false, false, null)]
    public void Recusa_SoQuandoADeclaracaoContradizAConfiguracao(bool aplica, bool temConfiguracao, string? codigo) =>
        (DeclaracaoDoFada.Recusa(aplica, temConfiguracao)?.Code).Should().Be(codigo);
}
