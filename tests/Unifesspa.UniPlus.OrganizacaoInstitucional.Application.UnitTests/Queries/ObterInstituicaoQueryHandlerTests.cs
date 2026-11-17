namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Instituicoes;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ObterInstituicaoQueryHandlerTests
{
    private readonly IInstituicaoRepository _repositorio = Substitute.For<IInstituicaoRepository>();

    [Fact(DisplayName = "Sem Instituição cadastrada, falha com o código de Instituição não encontrada")]
    public async Task SemInstituicao_FalhaComNaoEncontrada()
    {
        Result<InstituicaoDto> resultado = await ObterAsync();

        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.NaoEncontrada);
    }

    [Fact(DisplayName = "Com a Instituição cadastrada, devolve o DTO")]
    public async Task ComInstituicao_DevolveODto()
    {
        Instituicao unifesspa = InstituicaoDeTeste.Criar().Value!;
        _repositorio.ObterParaLeituraAsync(Arg.Any<CancellationToken>()).Returns(unifesspa);

        Result<InstituicaoDto> resultado = await ObterAsync();

        resultado.Value!.Id.Should().Be(unifesspa.Id);
        resultado.Value.CodigoEmec.Should().Be(unifesspa.CodigoEmec);
    }

    private Task<Result<InstituicaoDto>> ObterAsync() =>
        ObterInstituicaoQueryHandler.Handle(new ObterInstituicaoQuery(), _repositorio, CancellationToken.None);
}
