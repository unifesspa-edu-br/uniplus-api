namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class ListarFatosCandidatoParaManutencaoQueryHandlerTests
{
    private readonly IFatoCandidatoRepository _repository = Substitute.For<IFatoCandidatoRepository>();

    public ListarFatosCandidatoParaManutencaoQueryHandlerTests()
    {
        _repository.ListarPaginadoAsync(default, default, default, default, default, default, default).ReturnsForAnyArgs(
            ((IReadOnlyList<FatoCandidato>)[], ((string SortKey, Guid Id)?)null, ((string SortKey, Guid Id)?)null));
    }

    [Theory]
    [InlineData("declarado")]
    [InlineData(" DECLARADO ")]
    [InlineData("   ")]
    [InlineData("DESCONHECIDO")]
    public async Task Handle_OrigemInvalida_RecusaSemConsultar(string origem)
    {
        Result<ListarFatosCandidatoParaManutencaoResult> resultado = await ListarAsync(origem);
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.FiltroDeOrigemInvalida);
        resultado.Error.Message.Should().Be("Origem inválida. Use um dos códigos publicados para a origem do dado do candidato.");
        await _repository.DidNotReceiveWithAnyArgs().ListarPaginadoAsync(default, default, default, default, default, default, default);
    }

    [Theory]
    [InlineData("DECLARADO", OrigemFato.Declarado)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public async Task Handle_OrigemValida_ConsultaComFiltro(string? origem, OrigemFato? esperado)
    {
        Result<ListarFatosCandidatoParaManutencaoResult> resultado = await ListarAsync(origem);
        resultado.IsSuccess.Should().BeTrue();
        await _repository.Received(1).ListarPaginadoAsync(null, null, 20, PaginationDirection.Next, esperado, null, Arg.Any<CancellationToken>());
    }

    private Task<Result<ListarFatosCandidatoParaManutencaoResult>> ListarAsync(string? origem) =>
        ListarFatosCandidatoParaManutencaoQueryHandler.Handle(
            new ListarFatosCandidatoParaManutencaoQuery(null, null, 20, PaginationDirection.Next, origem, null), _repository, CancellationToken.None);
}
