namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
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
    private static readonly DateTimeOffset CidadeCarimbadaEm = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Instituicao InstituicaoExistente(
        string? cidadeCodigoIbge = null,
        string? cidadeNome = null,
        string? cidadeUf = null,
        string? cidadeOrigem = null,
        DateTimeOffset? cidadeDisplayAtualizadoEm = null) =>
        Instituicao.Criar(
            "3990", "Universidade Federal do Sul e Sudeste do Pará", "Unifesspa", "Universidade", "Pública Federal",
            cnpj: null, mantenedora: null, codigoMantenedoraEmec: null, situacao: null, atoCredenciamento: null,
            atoRecredenciamento: null, conceitoInstitucional: null, igc: null, website: null, enderecoSede: null,
            cidadeCodigoIbge, cidadeNome, cidadeUf, cidadeOrigem, cidadeDisplayAtualizadoEm, unidadeRaizId: null).Value!;

    private static AtualizarInstituicaoCommand CommandValido(
        Guid id,
        Guid? unidadeRaizId = null,
        string? cidadeCodigoIbge = null,
        string? cidadeNome = null,
        string? cidadeUf = null) => new(
        id, "3990", "Universidade Federal do Sul e Sudeste do Pará", "Unifesspa", "Universidade", "Pública Federal",
        Cnpj: null, Mantenedora: null, CodigoMantenedoraEmec: null, Situacao: null, AtoCredenciamento: null,
        AtoRecredenciamento: null, ConceitoInstitucional: null, Igc: null, Website: null, EnderecoSede: null,
        CidadeCodigoIbge: cidadeCodigoIbge, CidadeNome: cidadeNome, CidadeUf: cidadeUf, unidadeRaizId);

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
            CommandValido(Guid.CreateVersion7()), repo, unidadeRepo, uow, cache, TimeProvider.System, CancellationToken.None);

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
            CommandValido(existente.Id), repo, unidadeRepo, uow, cache, TimeProvider.System, CancellationToken.None);

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
            CommandValido(existente.Id, reitoria.Id), repo, unidadeRepo, uow, cache, TimeProvider.System, CancellationToken.None);

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
            CommandValido(existente.Id, Guid.CreateVersion7()), repo, unidadeRepo, uow, cache, TimeProvider.System, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.UnidadeRaizNaoEhReitoria);
        await uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "PUT sem mudar a cidade preserva cidade_display_atualizado_em (ADR-0090)")]
    public async Task Handle_CidadeInalterada_PreservaCarimboDeCidade()
    {
        IInstituicaoRepository repo = Substitute.For<IInstituicaoRepository>();
        IUnidadeRepository unidadeRepo = Substitute.For<IUnidadeRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IInstituicaoCacheInvalidator cache = Substitute.For<IInstituicaoCacheInvalidator>();
        Instituicao existente = InstituicaoExistente(
            "1504208", "Marabá", "PA", ReferenciaCidadeGeo.OrigemGeoApi, CidadeCarimbadaEm);
        repo.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        // Muda só o vínculo de raiz (deixado nulo aqui); o trio de cidade permanece igual.
        AtualizarInstituicaoCommand comando = CommandValido(
            existente.Id, cidadeCodigoIbge: "1504208", cidadeNome: "Marabá", cidadeUf: "PA");

        Result resultado = await AtualizarInstituicaoCommandHandler.Handle(
            comando, repo, unidadeRepo, uow, cache, TimeProvider.System, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.CidadeDisplayAtualizadoEm.Should().Be(CidadeCarimbadaEm,
            "a cidade não mudou, então o carimbo de frescura do display cache é preservado");
        existente.CidadeOrigem.Should().Be(ReferenciaCidadeGeo.OrigemGeoApi);
    }

    [Fact(DisplayName = "PUT trocando a cidade recarimba cidade_display_atualizado_em (ADR-0090)")]
    public async Task Handle_CidadeAlterada_RecarimbaCidade()
    {
        IInstituicaoRepository repo = Substitute.For<IInstituicaoRepository>();
        IUnidadeRepository unidadeRepo = Substitute.For<IUnidadeRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IInstituicaoCacheInvalidator cache = Substitute.For<IInstituicaoCacheInvalidator>();
        Instituicao existente = InstituicaoExistente(
            "1504208", "Marabá", "PA", ReferenciaCidadeGeo.OrigemGeoApi, CidadeCarimbadaEm);
        repo.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        AtualizarInstituicaoCommand comando = CommandValido(
            existente.Id, cidadeCodigoIbge: "1501402", cidadeNome: "Belém", cidadeUf: "PA");

        Result resultado = await AtualizarInstituicaoCommandHandler.Handle(
            comando, repo, unidadeRepo, uow, cache, TimeProvider.System, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.CidadeNome.Should().Be("Belém");
        existente.CidadeDisplayAtualizadoEm.Should().NotBe(CidadeCarimbadaEm,
            "a cidade mudou, então o carimbo é renovado a partir do TimeProvider");
        existente.CidadeOrigem.Should().Be(ReferenciaCidadeGeo.OrigemGeoApi);
    }

    [Fact(DisplayName = "PUT removendo a cidade zera o trio e o display cache (all-or-nothing)")]
    public async Task Handle_CidadeRemovida_ZeraTrioEDisplayCache()
    {
        IInstituicaoRepository repo = Substitute.For<IInstituicaoRepository>();
        IUnidadeRepository unidadeRepo = Substitute.For<IUnidadeRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IInstituicaoCacheInvalidator cache = Substitute.For<IInstituicaoCacheInvalidator>();
        Instituicao existente = InstituicaoExistente(
            "1504208", "Marabá", "PA", ReferenciaCidadeGeo.OrigemGeoApi, CidadeCarimbadaEm);
        repo.ObterPorIdAsync(existente.Id, Arg.Any<CancellationToken>()).Returns(existente);

        // Trio ausente no payload → remove a referência de cidade.
        AtualizarInstituicaoCommand comando = CommandValido(existente.Id);

        Result resultado = await AtualizarInstituicaoCommandHandler.Handle(
            comando, repo, unidadeRepo, uow, cache, TimeProvider.System, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        existente.CidadeCodigoIbge.Should().BeNull();
        existente.CidadeNome.Should().BeNull();
        existente.CidadeUf.Should().BeNull();
        existente.CidadeOrigem.Should().BeNull();
        existente.CidadeDisplayAtualizadoEm.Should().BeNull();
    }
}
