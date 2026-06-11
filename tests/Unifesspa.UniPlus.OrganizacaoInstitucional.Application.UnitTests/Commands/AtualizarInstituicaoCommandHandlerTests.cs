namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

public sealed class AtualizarInstituicaoCommandHandlerTests
{
    private static Instituicao InstituicaoExistente() =>
        Instituicao.Criar(
            "3990", "Universidade Federal do Sul e Sudeste do Pará", "Unifesspa", "Universidade", "Pública Federal",
            null, null, null, null, null, null, null, null, null, null, null, null).Value!;

    private static AtualizarInstituicaoCommand CommandValido(Guid id, Guid? unidadeRaizId = null) => new(
        id, "3990", "Universidade Federal do Sul e Sudeste do Pará", "Unifesspa", "Universidade", "Pública Federal",
        Cnpj: null, Mantenedora: null, CodigoMantenedoraEmec: null, Situacao: null, AtoCredenciamento: null,
        AtoRecredenciamento: null, ConceitoInstitucional: null, Igc: null, Website: null, EnderecoSede: null,
        MunicipioSede: null, unidadeRaizId);

    private static Unidade NovaUnidade(TipoUnidade tipo) =>
        Unidade.Criar(
            "Reitoria", null, Slug.From("reitoria").Value!, "REIT", "REIT001",
            null, tipo, false, new DateOnly(2026, 1, 1), null, OrigemUnidade.CriadoNoUniPlus).Value!;

    [Fact(DisplayName = "Handle com Instituição inexistente retorna NaoEncontrada e NÃO persiste")]
    public async Task Handle_ComInstituicaoInexistente_RetornaNaoEncontrada()
    {
        IInstituicaoRepository repo = Substitute.For<IInstituicaoRepository>();
        IUnidadeRepository unidadeRepo = Substitute.For<IUnidadeRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IInstituicaoCacheInvalidator cache = Substitute.For<IInstituicaoCacheInvalidator>();
        repo.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Instituicao?)null);

        Result resultado = await AtualizarInstituicaoCommandHandler.Handle(
            CommandValido(Guid.CreateVersion7()), repo, unidadeRepo, uow, cache, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.NaoEncontrada);
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com campos válidos atualiza e invalida cache")]
    public async Task Handle_ComCamposValidos_AtualizaEInvalidaCache()
    {
        IInstituicaoRepository repo = Substitute.For<IInstituicaoRepository>();
        IUnidadeRepository unidadeRepo = Substitute.For<IUnidadeRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IInstituicaoCacheInvalidator cache = Substitute.For<IInstituicaoCacheInvalidator>();
        Instituicao existente = InstituicaoExistente();
        repo.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        Result resultado = await AtualizarInstituicaoCommandHandler.Handle(
            CommandValido(existente.Id), repo, unidadeRepo, uow, cache, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await uow.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await cache.Received(1).InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com unidade raiz reitoria aceita o vínculo (CA-04)")]
    public async Task Handle_ComUnidadeRaizReitoria_Aceita()
    {
        IInstituicaoRepository repo = Substitute.For<IInstituicaoRepository>();
        IUnidadeRepository unidadeRepo = Substitute.For<IUnidadeRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IInstituicaoCacheInvalidator cache = Substitute.For<IInstituicaoCacheInvalidator>();
        Instituicao existente = InstituicaoExistente();
        repo.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        Unidade reitoria = NovaUnidade(TipoUnidade.Reitoria);
        unidadeRepo.ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(reitoria);

        Result resultado = await AtualizarInstituicaoCommandHandler.Handle(
            CommandValido(existente.Id, reitoria.Id), repo, unidadeRepo, uow, cache, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.UnidadeRaizId.Should().Be(reitoria.Id);
    }

    [Fact(DisplayName = "Handle com unidade raiz de outro tipo retorna UnidadeRaizNaoEhReitoria (CA-04)")]
    public async Task Handle_ComUnidadeRaizNaoReitoria_RetornaErro()
    {
        IInstituicaoRepository repo = Substitute.For<IInstituicaoRepository>();
        IUnidadeRepository unidadeRepo = Substitute.For<IUnidadeRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IInstituicaoCacheInvalidator cache = Substitute.For<IInstituicaoCacheInvalidator>();
        Instituicao existente = InstituicaoExistente();
        repo.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);
        Unidade centro = NovaUnidade(TipoUnidade.Centro);
        unidadeRepo.ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(centro);

        Result resultado = await AtualizarInstituicaoCommandHandler.Handle(
            CommandValido(existente.Id, Guid.CreateVersion7()), repo, unidadeRepo, uow, cache, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.UnidadeRaizNaoEhReitoria);
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
