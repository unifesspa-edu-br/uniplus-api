namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ObterUnidadePorIdQueryHandlerTests
{
    private readonly IUnidadeRepository _repositorio = Substitute.For<IUnidadeRepository>();

    [Fact(DisplayName = "A Unidade inexistente falha com o código de Unidade não encontrada")]
    public async Task Inexistente_FalhaComNaoEncontrada()
    {
        Result<UnidadeDto> resultado = await ObterAsync(Guid.NewGuid());

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.NaoEncontrada);
    }

    [Fact(DisplayName = "A Unidade existente devolve o DTO")]
    public async Task Existente_DevolveODto()
    {
        Unidade ceps = UnidadeDeTeste.Criar().Value!;
        _repositorio.ObterPorIdParaLeituraAsync(ceps.Id, Arg.Any<CancellationToken>()).Returns(ceps);

        Result<UnidadeDto> resultado = await ObterAsync(ceps.Id);

        resultado.Value!.Id.Should().Be(ceps.Id);
        resultado.Value.Sigla.Should().Be(ceps.Sigla);
    }

    private Task<Result<UnidadeDto>> ObterAsync(Guid id) =>
        ObterUnidadePorIdQueryHandler.Handle(new ObterUnidadePorIdQuery(id), _repositorio, CancellationToken.None);
}
