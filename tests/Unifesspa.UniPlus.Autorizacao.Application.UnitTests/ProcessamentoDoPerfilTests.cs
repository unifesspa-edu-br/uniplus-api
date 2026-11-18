namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.Commands;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ProcessamentoDoPerfilTests
{
    private static readonly MaterializarRevisaoVencidaCommand Comando = new(Guid.NewGuid());

    private readonly ICommandBus _bus = Substitute.For<ICommandBus>();
    private readonly List<Exception?> _falhas = [];

    [Theory(DisplayName = "Só o perfil que teve o que fazer conta como processado")]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task Executar_Sucesso_ContaSoOQueTeveOQueFazer(bool tratado, int esperado)
    {
        _bus.Send(Comando, CancellationToken.None).Returns(Task.FromResult(Result<bool>.Success(tratado)));

        int contagem = await ProcessamentoDoPerfil.ExecutarAsync(_bus, Comando, _falhas.Add, CancellationToken.None);

        contagem.Should().Be(esperado);
        _falhas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O resultado de falha avisa a falha sem exceção e não conta")]
    public async Task Executar_ResultadoDeFalha_AvisaSemExcecaoENaoConta()
    {
        _bus.Send(Comando, CancellationToken.None).Returns(Task.FromResult(Result<bool>.Failure(new DomainError("Teste.Falha", "Falha de teste."))));

        int contagem = await ProcessamentoDoPerfil.ExecutarAsync(_bus, Comando, _falhas.Add, CancellationToken.None);

        contagem.Should().Be(0);
        _falhas.Should().ContainSingle().Which.Should().BeNull();
    }

    [Fact(DisplayName = "A exceção do comando é isolada, avisada com a exceção e não conta")]
    public async Task Executar_Excecao_IsolaAvisaENaoConta()
    {
        InvalidOperationException erro = new("Falha de teste.");
        _bus.Send(Comando, CancellationToken.None).Returns<Task<Result<bool>>>(_ => throw erro);

        int contagem = await ProcessamentoDoPerfil.ExecutarAsync(_bus, Comando, _falhas.Add, CancellationToken.None);

        contagem.Should().Be(0);
        _falhas.Should().ContainSingle().Which.Should().BeSameAs(erro);
    }

    [Fact(DisplayName = "O cancelamento não é falha e se propaga")]
    public async Task Executar_Cancelamento_Propaga()
    {
        using CancellationTokenSource cancelamento = new();
        await cancelamento.CancelAsync();
        _bus.Send(Comando, cancelamento.Token).Returns(Task.FromCanceled<Result<bool>>(cancelamento.Token));

        Func<Task> executar = () => ProcessamentoDoPerfil.ExecutarAsync(_bus, Comando, _falhas.Add, cancelamento.Token);

        await executar.Should().ThrowAsync<OperationCanceledException>();
        _falhas.Should().BeEmpty();
    }
}
