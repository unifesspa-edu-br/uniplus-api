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
/// A orquestração do handler: a conferência recusa sem gravar, e a gravação aplica a atualização à
/// Instituição conferida. As regras estão nos testes da conferência e da gravação.
/// </summary>
public sealed class AtualizarInstituicaoCommandHandlerTests
{
    private readonly IInstituicaoRepository _repo = Substitute.For<IInstituicaoRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IInstituicaoCacheInvalidator _cache = Substitute.For<IInstituicaoCacheInvalidator>();

    private Task<Result> AtualizarAsync(AtualizarInstituicaoCommand command) =>
        AtualizarInstituicaoCommandHandler.Handle(
            command,
            new ConferenciaDoCadastroDaInstituicao(_repo, Substitute.For<IUnidadeRepository>()),
            new GravacaoDaInstituicao(_repo, _uow, _cache),
            new RelogioFixo(new DateTimeOffset(2026, 6, 22, 17, 10, 0, TimeSpan.Zero)),
            CancellationToken.None);

    [Fact(DisplayName = "Handle com campos válidos atualiza a Instituição conferida, salva e invalida o cache")]
    public async Task Handle_ComCamposValidos_AtualizaSalvaEInvalidaCache()
    {
        Instituicao existente = InstituicaoDeTeste.Criar(
            InstituicaoDeTeste.IdentificacaoDaUnifesspa with { Sigla = "Antiga" }).Value!;
        _repo.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarAsync(ComandosDeInstituicaoDeTeste.Atualizacao(existente.Id));

        resultado.IsSuccess.Should().BeTrue();
        existente.Sigla.Should().Be("Unifesspa");
        await _uow.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await _cache.Received(1).InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com Instituição inexistente devolve NaoEncontrada sem gravar")]
    public async Task Handle_ComInstituicaoInexistente_DevolveNaoEncontradaSemGravar()
    {
        Result resultado = await AtualizarAsync(ComandosDeInstituicaoDeTeste.Atualizacao(Guid.CreateVersion7()));

        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.NaoEncontrada);
        await _uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com payload inválido devolve o erro do payload sem gravar")]
    public async Task Handle_ComPayloadInvalido_DevolveOErroSemGravar()
    {
        Result resultado = await AtualizarAsync(ComandosDeInstituicaoDeTeste.Atualizacao(Guid.CreateVersion7()) with { Nome = "" });

        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.NomeObrigatorio);
        await _uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
