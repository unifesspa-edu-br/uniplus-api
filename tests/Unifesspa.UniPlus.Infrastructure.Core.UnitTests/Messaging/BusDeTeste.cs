namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging;

using NSubstitute;

internal static class BusDeTeste
{
    internal static Wolverine.IMessageBus ComResposta(object mensagem, string resposta)
    {
        Wolverine.IMessageBus bus = Substitute.For<Wolverine.IMessageBus>();
        bus.InvokeAsync<string>(mensagem, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(resposta));
        return bus;
    }
}
