namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

public sealed class BloqueioCompartilhadoDoProcessoSeletivoTests
{
    private readonly CenarioDoDocumentoAPublicar _cenario = new();

    private Task<bool> Bloquear() => _cenario.Bloqueio.BloquearSeExisteAsync(_cenario.ProcessoId, CancellationToken.None);

    [Fact(DisplayName = "O rascunho existente é o dono e o processo nem é consultado")]
    public async Task RascunhoExistente_EODono()
    {
        _cenario.ComRascunhoComoDono();

        (await Bloquear()).Should().BeTrue();
        await _cenario.Processos.DidNotReceiveWithAnyArgs().ExisteAsync(default, default);
    }

    [Fact(DisplayName = "Sem rascunho, o processo publicado de mesmo identificador é o dono")]
    public async Task SemRascunho_ProcessoPublicadoEODono()
    {
        _cenario.ComProcessoPublicadoComoDono();

        (await Bloquear()).Should().BeTrue();
    }

    [Fact(DisplayName = "Sem rascunho nem processo não há dono")]
    public async Task SemRascunhoNemProcesso_NaoHaDono()
    {
        (await Bloquear()).Should().BeFalse();
        await _cenario.Rascunhos.Received(1).BloquearCompartilhadoAsync(_cenario.ProcessoId, Arg.Any<CancellationToken>());
    }
}
