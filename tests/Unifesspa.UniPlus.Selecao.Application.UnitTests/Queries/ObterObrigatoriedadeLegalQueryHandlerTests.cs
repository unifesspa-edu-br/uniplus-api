namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>A leitura de uma obrigatoriedade legal por Id recusa com o código próprio quando a regra não existe.</summary>
public sealed class ObterObrigatoriedadeLegalQueryHandlerTests
{
    [Fact(DisplayName = "A leitura por Id recusa com o código de obrigatoriedade legal não encontrada para regra inexistente")]
    public async Task Obter_Inexistente_RecusaComObrigatoriedadeNaoEncontrada()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        repository.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ObrigatoriedadeLegal?)null);

        Result<ObrigatoriedadeLegalDto> lido = await ObterObrigatoriedadeLegalQueryHandler.Handle(
            new ObterObrigatoriedadeLegalQuery(Guid.CreateVersion7()),
            repository,
            Substitute.For<ITipoProcessoReader>(),
            CancellationToken.None);

        lido.IsFailure.Should().BeTrue();
        lido.Error!.Code.Should().Be("ObrigatoriedadeLegal.NaoEncontrada");
    }
}
