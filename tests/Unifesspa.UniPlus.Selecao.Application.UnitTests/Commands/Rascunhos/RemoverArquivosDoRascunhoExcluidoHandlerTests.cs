namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.Rascunhos;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;

public sealed class RemoverArquivosDoRascunhoExcluidoHandlerTests
{
    private static readonly RemoverArquivosDoRascunhoExcluido Mensagem = new(["chave-de-envio", "chave-definitiva", "outra-chave"]);

    private readonly IArquivoArmazenadoStorage _storage = Substitute.For<IArquivoArmazenadoStorage>();

    private Task Entregar() => RemoverArquivosDoRascunhoExcluidoHandler.Handle(Mensagem, _storage, CancellationToken.None);

    [Fact(DisplayName = "Remove do armazenamento todas as chaves da mensagem")]
    public async Task RemoveTodasAsChaves()
    {
        await Entregar();

        foreach (string chave in Mensagem.Chaves)
        {
            await _storage.Received(1).RemoverAsync(chave, Arg.Any<CancellationToken>());
        }
    }

    [Fact(DisplayName = "A reentrega repete as remoções sem efeito: o armazenamento trata o objeto ausente como concluído")]
    public async Task Reentrega_RepeteAsRemocoesSemFalhar()
    {
        await Entregar();

        await Entregar();

        await _storage.Received(2).RemoverAsync("chave-de-envio", Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A falha do armazenamento propaga para a retentativa e a seguinte remove o que restou")]
    public async Task Falha_PropagaEARetentativaConclui()
    {
        _storage.RemoverAsync("chave-definitiva", Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("Armazenamento indisponível (simulado)")));

        Func<Task> primeiraEntrega = Entregar;
        await primeiraEntrega.Should().ThrowAsync<InvalidOperationException>();
        _storage.RemoverAsync("chave-definitiva", Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        await Entregar();

        await _storage.Received(2).RemoverAsync("chave-definitiva", Arg.Any<CancellationToken>());
        await _storage.Received(1).RemoverAsync("outra-chave", Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Os argumentos nulos são recusados")]
    public async Task ArgumentosNulos_Lancam()
    {
        Func<Task> semStorage = () => RemoverArquivosDoRascunhoExcluidoHandler.Handle(Mensagem, null!, CancellationToken.None);
        Func<Task> semMensagem = () => RemoverArquivosDoRascunhoExcluidoHandler.Handle(null!, _storage, CancellationToken.None);

        await semStorage.Should().ThrowAsync<ArgumentNullException>();
        await semMensagem.Should().ThrowAsync<ArgumentNullException>();
    }
}
