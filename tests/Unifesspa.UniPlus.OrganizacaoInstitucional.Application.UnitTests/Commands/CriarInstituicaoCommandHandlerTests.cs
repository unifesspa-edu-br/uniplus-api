namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A orquestração do handler: a conferência recusa sem gravar, e a gravação devolve o Id. As regras estão
/// nos testes da conferência e da gravação.
/// </summary>
public sealed class CriarInstituicaoCommandHandlerTests
{
    private readonly IInstituicaoRepository _repo = Substitute.For<IInstituicaoRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IInstituicaoCacheInvalidator _cache = Substitute.For<IInstituicaoCacheInvalidator>();

    private Task<Result<Guid>> CriarAsync(CriarInstituicaoCommand command) =>
        CriarInstituicaoCommandHandler.Handle(
            command,
            new ConferenciaDoCadastroDaInstituicao(_repo, Substitute.For<IUnidadeRepository>()),
            new GravacaoDaInstituicao(_repo, _uow, _cache),
            new RelogioFixo(new DateTimeOffset(2026, 6, 22, 17, 10, 0, TimeSpan.Zero)),
            CancellationToken.None);

    [Fact(DisplayName = "Handle sem Instituição existente cria, persiste e devolve o Id da Instituição")]
    public async Task Handle_SemInstituicaoExistente_CriaEDevolveOId()
    {
        Instituicao? adicionada = null;
        await _repo.AdicionarAsync(Arg.Do<Instituicao>(i => adicionada = i), Arg.Any<CancellationToken>());

        Result<Guid> resultado = await CriarAsync(ComandosDeInstituicaoDeTeste.Criacao());

        resultado.Value.Should().Be(adicionada!.Id);
        await _uow.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com a conferência recusada devolve a recusa sem gravar")]
    public async Task Handle_ComConferenciaRecusada_DevolveARecusaSemGravar()
    {
        _repo.ExisteAlgumaVivaAsync(Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await CriarAsync(ComandosDeInstituicaoDeTeste.Criacao());

        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.JaExisteInstituicaoViva);
        await _repo.DidNotReceive().AdicionarAsync(Arg.Any<Instituicao>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com payload inválido devolve todos os erros do payload sem gravar")]
    public async Task Handle_ComPayloadInvalido_DevolveOsErrosSemGravar()
    {
        Result<Guid> resultado = await CriarAsync(ComandosDeInstituicaoDeTeste.Criacao() with { CodigoEmec = "", Sigla = "" });

        resultado.Errors.Select(erro => erro.Field).Should().Equal("codigoEmec", "sigla");
        await _uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
