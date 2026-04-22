namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Middleware;

using FluentAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Middleware;

public class CorrelationIdAccessorTests
{
    [Fact]
    public void SetCorrelationId_ComValorValido_DeveArmazenarValor()
    {
        CorrelationIdAccessor accessor = new();
        const string esperado = "abc-123";

        accessor.SetCorrelationId(esperado);

        accessor.CorrelationId.Should().Be(esperado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SetCorrelationId_ComValorVazioOuEmBranco_DeveLancarArgumentException(string valor)
    {
        CorrelationIdAccessor accessor = new();

        Action acao = () => accessor.SetCorrelationId(valor);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetCorrelationId_ComValorNulo_DeveLancarArgumentNullException()
    {
        CorrelationIdAccessor accessor = new();

        Action acao = () => accessor.SetCorrelationId(null!);

        acao.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task CorrelationId_DeveFluirEntreContinuacoesAsync()
    {
        CorrelationIdAccessor accessor = new();
        const string id = "flow-test";

        accessor.SetCorrelationId(id);
        await Task.Yield();

        accessor.CorrelationId.Should().Be(id);
    }

    [Fact]
    public async Task CorrelationId_EmContextosParalelos_DeveIsolarValores()
    {
        CorrelationIdAccessor accessor = new();
        string? observadoEmCtx1 = null;
        string? observadoEmCtx2 = null;

        await Task.WhenAll(
            Task.Run(async () =>
            {
                accessor.SetCorrelationId("ctx-1");
                await Task.Delay(10);
                observadoEmCtx1 = accessor.CorrelationId;
            }),
            Task.Run(async () =>
            {
                accessor.SetCorrelationId("ctx-2");
                await Task.Delay(10);
                observadoEmCtx2 = accessor.CorrelationId;
            })
        );

        observadoEmCtx1.Should().Be("ctx-1");
        observadoEmCtx2.Should().Be("ctx-2");
    }
}
