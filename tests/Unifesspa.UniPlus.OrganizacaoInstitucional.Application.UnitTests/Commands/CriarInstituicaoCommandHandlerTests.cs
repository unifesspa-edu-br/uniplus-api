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

public sealed class CriarInstituicaoCommandHandlerTests
{
    private static CriarInstituicaoCommand CommandValido(Guid? unidadeRaizId = null) => new(
        "3990",
        "Universidade Federal do Sul e Sudeste do Pará",
        "Unifesspa",
        "Universidade",
        "Pública Federal",
        Cnpj: null,
        Mantenedora: null,
        CodigoMantenedoraEmec: null,
        Situacao: null,
        AtoCredenciamento: null,
        AtoRecredenciamento: null,
        ConceitoInstitucional: null,
        Igc: null,
        Website: null,
        EnderecoSede: null,
        MunicipioSede: null,
        unidadeRaizId);

    private static Unidade NovaUnidade(TipoUnidade tipo) =>
        Unidade.Criar(
            "Reitoria", null, Slug.From("reitoria").Value!, "REIT", "REIT001",
            null, tipo, false, new DateOnly(2026, 1, 1), null, OrigemUnidade.CriadoNoUniPlus).Value!;

    [Fact(DisplayName = "Handle sem Instituição existente cria, persiste e invalida cache (CA-01)")]
    public async Task Handle_SemInstituicaoExistente_CriaInstituicaoAtiva()
    {
        IInstituicaoRepository repo = Substitute.For<IInstituicaoRepository>();
        IUnidadeRepository unidadeRepo = Substitute.For<IUnidadeRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IInstituicaoCacheInvalidator cache = Substitute.For<IInstituicaoCacheInvalidator>();
        repo.ExisteAlgumaVivaAsync(Arg.Any<CancellationToken>()).Returns(false);

        Result<Guid> resultado = await CriarInstituicaoCommandHandler.Handle(
            CommandValido(), repo, unidadeRepo, uow, cache, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBeEmpty();
        await repo.Received(1).AdicionarAsync(Arg.Any<Instituicao>(), Arg.Any<CancellationToken>());
        await uow.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await cache.Received(1).InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com Instituição viva existente retorna JaExiste e NÃO persiste (CA-02)")]
    public async Task Handle_ComInstituicaoVivaExistente_RetornaJaExiste()
    {
        IInstituicaoRepository repo = Substitute.For<IInstituicaoRepository>();
        IUnidadeRepository unidadeRepo = Substitute.For<IUnidadeRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IInstituicaoCacheInvalidator cache = Substitute.For<IInstituicaoCacheInvalidator>();
        repo.ExisteAlgumaVivaAsync(Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await CriarInstituicaoCommandHandler.Handle(
            CommandValido(), repo, unidadeRepo, uow, cache, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.JaExisteInstituicaoViva);
        await repo.DidNotReceive().AdicionarAsync(Arg.Any<Instituicao>(), Arg.Any<CancellationToken>());
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com unidade raiz inexistente retorna UnidadeRaizNaoEncontrada (CA-04)")]
    public async Task Handle_ComUnidadeRaizInexistente_RetornaErro()
    {
        IInstituicaoRepository repo = Substitute.For<IInstituicaoRepository>();
        IUnidadeRepository unidadeRepo = Substitute.For<IUnidadeRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IInstituicaoCacheInvalidator cache = Substitute.For<IInstituicaoCacheInvalidator>();
        repo.ExisteAlgumaVivaAsync(Arg.Any<CancellationToken>()).Returns(false);
        Guid raizId = Guid.CreateVersion7();
        unidadeRepo.ObterPorIdParaLeituraAsync(raizId, Arg.Any<CancellationToken>()).Returns((Unidade?)null);

        Result<Guid> resultado = await CriarInstituicaoCommandHandler.Handle(
            CommandValido(raizId), repo, unidadeRepo, uow, cache, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.UnidadeRaizNaoEncontrada);
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com unidade raiz de outro tipo retorna UnidadeRaizNaoEhReitoria (CA-04)")]
    public async Task Handle_ComUnidadeRaizNaoReitoria_RetornaErro()
    {
        IInstituicaoRepository repo = Substitute.For<IInstituicaoRepository>();
        IUnidadeRepository unidadeRepo = Substitute.For<IUnidadeRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IInstituicaoCacheInvalidator cache = Substitute.For<IInstituicaoCacheInvalidator>();
        repo.ExisteAlgumaVivaAsync(Arg.Any<CancellationToken>()).Returns(false);
        Unidade centro = NovaUnidade(TipoUnidade.Centro);
        unidadeRepo.ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(centro);

        Result<Guid> resultado = await CriarInstituicaoCommandHandler.Handle(
            CommandValido(Guid.CreateVersion7()), repo, unidadeRepo, uow, cache, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.UnidadeRaizNaoEhReitoria);
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com unidade raiz do tipo reitoria cria com sucesso (CA-04 contraprova)")]
    public async Task Handle_ComUnidadeRaizReitoria_Cria()
    {
        IInstituicaoRepository repo = Substitute.For<IInstituicaoRepository>();
        IUnidadeRepository unidadeRepo = Substitute.For<IUnidadeRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IInstituicaoCacheInvalidator cache = Substitute.For<IInstituicaoCacheInvalidator>();
        repo.ExisteAlgumaVivaAsync(Arg.Any<CancellationToken>()).Returns(false);
        Unidade reitoria = NovaUnidade(TipoUnidade.Reitoria);
        unidadeRepo.ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(reitoria);

        Result<Guid> resultado = await CriarInstituicaoCommandHandler.Handle(
            CommandValido(reitoria.Id), repo, unidadeRepo, uow, cache, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        await repo.Received(1).AdicionarAsync(Arg.Any<Instituicao>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com campo obrigatório vazio retorna erro de domínio e NÃO persiste")]
    public async Task Handle_ComCampoObrigatorioVazio_RetornaErro()
    {
        IInstituicaoRepository repo = Substitute.For<IInstituicaoRepository>();
        IUnidadeRepository unidadeRepo = Substitute.For<IUnidadeRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IInstituicaoCacheInvalidator cache = Substitute.For<IInstituicaoCacheInvalidator>();
        repo.ExisteAlgumaVivaAsync(Arg.Any<CancellationToken>()).Returns(false);
        CriarInstituicaoCommand command = CommandValido() with { CodigoEmec = "" };

        Result<Guid> resultado = await CriarInstituicaoCommandHandler.Handle(
            command, repo, unidadeRepo, uow, cache, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.CodigoEmecObrigatorio);
        await repo.DidNotReceive().AdicionarAsync(Arg.Any<Instituicao>(), Arg.Any<CancellationToken>());
    }
}
