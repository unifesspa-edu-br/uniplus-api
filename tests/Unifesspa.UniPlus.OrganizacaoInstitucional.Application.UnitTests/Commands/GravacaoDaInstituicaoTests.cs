namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class GravacaoDaInstituicaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 6, 22, 17, 10, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CidadeCarimbadaEm = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>A sede em Marabá já gravada com a proveniência do Geo e o carimbo anterior.</summary>
    private static readonly CidadeDaSede MarabaCarimbada =
        InstituicaoDeTeste.SedeEmMaraba with { Origem = ReferenciaCidadeGeo.OrigemGeoApi, DisplayAtualizadoEm = CidadeCarimbadaEm };

    private readonly IInstituicaoRepository _repo = Substitute.For<IInstituicaoRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IInstituicaoCacheInvalidator _cache = Substitute.For<IInstituicaoCacheInvalidator>();
    private readonly GravacaoDaInstituicao _gravacao;

    public GravacaoDaInstituicaoTests()
    {
        _gravacao = new GravacaoDaInstituicao(_repo, _uow, _cache);
    }

    /// <summary>A gravação recusada pelo índice único como o provedor a reporta, com a restrição dada.</summary>
    private void GravacaoRecusadaPelaRestricao(string restricao) =>
        _uow.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException("violação sintética", new ViolacaoDeUnicidadeDeTeste(restricao)));

    private Task<Result<Guid>> GravarNovaAsync() =>
        _gravacao.GravarNovaAsync(InstituicaoDeTeste.Criar().Value!, CancellationToken.None);

    private Task<Result> GravarAtualizacaoAsync(Instituicao existente, AtualizarInstituicaoCommand command) =>
        _gravacao.GravarAtualizacaoAsync(existente, command, Agora, CancellationToken.None);

    [Fact(DisplayName = "Gravar a nova Instituição adiciona, salva e só depois invalida o cache")]
    public async Task GravarNova_Sucesso_AdicionaSalvaEInvalidaNessaOrdem()
    {
        Instituicao nova = InstituicaoDeTeste.Criar().Value!;

        Result<Guid> resultado = await _gravacao.GravarNovaAsync(nova, CancellationToken.None);

        resultado.Value.Should().Be(nova.Id);
        Received.InOrder(() =>
        {
            _ = _repo.AdicionarAsync(nova, Arg.Any<CancellationToken>());
            _ = _uow.SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
            _ = _cache.InvalidarAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "Corrida no índice único do singleton vira JaExiste, descarta o rastreamento e não invalida o cache")]
    public async Task GravarNova_ConflitoDoIndiceDoSingleton_DevolveJaExisteDescartandoSemInvalidarCache()
    {
        GravacaoRecusadaPelaRestricao(RestricoesDoBanco.InstituicaoUnicaAtiva);

        Result<Guid> resultado = await GravarNovaAsync();

        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.JaExisteInstituicaoViva);
        _uow.Received(1).DescartarAlteracoesNaoSalvas();
        await _cache.DidNotReceive().InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Violação de outra restrição única propaga, sem descartar nem invalidar o cache")]
    public async Task GravarNova_ConflitoDeOutraRestricao_Propaga()
    {
        GravacaoRecusadaPelaRestricao("ix_outra_restricao");

        Func<Task> act = GravarNovaAsync;

        await act.Should().ThrowAsync<DbUpdateException>();
        _uow.DidNotReceive().DescartarAlteracoesNaoSalvas();
        await _cache.DidNotReceive().InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Outra exceção na gravação propaga, sem descartar nem invalidar o cache")]
    public async Task GravarNova_OutraExcecao_Propaga()
    {
        _uow.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("falha"));

        Func<Task> act = GravarNovaAsync;

        await act.Should().ThrowAsync<InvalidOperationException>();
        _uow.DidNotReceive().DescartarAlteracoesNaoSalvas();
        await _cache.DidNotReceive().InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A falha ao adicionar, mesmo parecida com o conflito do singleton, propaga sem tentar salvar")]
    public async Task GravarNova_FalhaAoAdicionar_PropagaSemSalvar()
    {
        _repo.AdicionarAsync(Arg.Any<Instituicao>(), Arg.Any<CancellationToken>()).ThrowsAsync(
            new DbUpdateException("violação sintética", new ViolacaoDeUnicidadeDeTeste(RestricoesDoBanco.InstituicaoUnicaAtiva)));

        Func<Task> act = GravarNovaAsync;

        await act.Should().ThrowAsync<DbUpdateException>();
        await _uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Gravar a atualização altera a Instituição, salva e só depois invalida o cache")]
    public async Task GravarAtualizacao_Sucesso_AplicaSalvaEInvalidaNessaOrdem()
    {
        Instituicao existente = InstituicaoDeTeste.Criar(InstituicaoDeTeste.IdentificacaoDaUnifesspa with { Sigla = "Antiga" }).Value!;

        Result resultado = await GravarAtualizacaoAsync(existente, ComandosDeInstituicaoDeTeste.Atualizacao(existente.Id));

        resultado.IsSuccess.Should().BeTrue();
        existente.Sigla.Should().Be("Unifesspa");
        Received.InOrder(() =>
        {
            _ = _uow.SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
            _ = _cache.InvalidarAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "Atualização que o domínio recusa devolve o erro sem salvar nem invalidar o cache")]
    public async Task GravarAtualizacao_RecusadaPeloDominio_DevolveOErroSemSalvar()
    {
        Instituicao existente = InstituicaoDeTeste.Criar().Value!;

        Result resultado = await GravarAtualizacaoAsync(
            existente, ComandosDeInstituicaoDeTeste.Atualizacao(existente.Id) with { Nome = "" });

        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.NomeObrigatorio);
        await _uow.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await _cache.DidNotReceive().InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Atualização sem mudar a cidade preserva a proveniência e o carimbo gravados (ADR-0090)")]
    public async Task GravarAtualizacao_CidadeInalterada_PreservaOCarimbo()
    {
        Instituicao existente = InstituicaoDeTeste.Criar(cidade: MarabaCarimbada).Value!;

        await GravarAtualizacaoAsync(existente, ComandosDeInstituicaoDeTeste.Atualizacao(
            existente.Id, cidadeCodigoIbge: "1504208", cidadeNome: "Marabá", cidadeUf: "PA"));

        existente.CidadeDaSede.Should().Be(MarabaCarimbada);
    }

    [Fact(DisplayName = "Atualização com a cidade só diferente em caixa e espaços preserva o carimbo")]
    public async Task GravarAtualizacao_CidadeSoDiferenteEmCaixaEEspacos_PreservaOCarimbo()
    {
        Instituicao existente = InstituicaoDeTeste.Criar(cidade: MarabaCarimbada).Value!;

        await GravarAtualizacaoAsync(existente, ComandosDeInstituicaoDeTeste.Atualizacao(
            existente.Id, cidadeCodigoIbge: " 1504208 ", cidadeNome: " Marabá ", cidadeUf: "pa"));

        existente.CidadeDaSede.Should().Be(MarabaCarimbada);
    }

    [Fact(DisplayName = "Atualização trocando a cidade recarimba a proveniência e o instante (ADR-0090)")]
    public async Task GravarAtualizacao_CidadeAlterada_Recarimba()
    {
        Instituicao existente = InstituicaoDeTeste.Criar(cidade: MarabaCarimbada).Value!;

        await GravarAtualizacaoAsync(existente, ComandosDeInstituicaoDeTeste.Atualizacao(
            existente.Id, cidadeCodigoIbge: "1501402", cidadeNome: "Belém", cidadeUf: "PA"));

        existente.CidadeDaSede.Should().Be(new CidadeDaSede("1501402", "Belém", "PA", ReferenciaCidadeGeo.OrigemGeoApi, Agora));
    }

    [Fact(DisplayName = "Atualização incluindo a cidade numa Instituição sem cidade a carimba")]
    public async Task GravarAtualizacao_CidadeIncluida_Carimba()
    {
        Instituicao existente = InstituicaoDeTeste.Criar().Value!;

        await GravarAtualizacaoAsync(existente, ComandosDeInstituicaoDeTeste.Atualizacao(
            existente.Id, cidadeCodigoIbge: "1504208", cidadeNome: "Marabá", cidadeUf: "PA"));

        existente.CidadeDaSede.Should().Be(MarabaCarimbada with { DisplayAtualizadoEm = Agora });
    }

    [Fact(DisplayName = "Atualização removendo a cidade zera o trio, a proveniência e o carimbo")]
    public async Task GravarAtualizacao_CidadeRemovida_ZeraTudo()
    {
        Instituicao existente = InstituicaoDeTeste.Criar(cidade: MarabaCarimbada).Value!;

        await GravarAtualizacaoAsync(existente, ComandosDeInstituicaoDeTeste.Atualizacao(existente.Id));

        existente.CidadeDaSede.Should().Be(CidadeDaSede.Nenhuma);
    }

    [Fact(DisplayName = "Gravar a remoção remove a Instituição, salva e só depois invalida o cache")]
    public async Task GravarRemocao_RemoveSalvaEInvalidaNessaOrdem()
    {
        Instituicao existente = InstituicaoDeTeste.Criar().Value!;

        await _gravacao.GravarRemocaoAsync(existente, CancellationToken.None);

        Received.InOrder(() =>
        {
            _repo.Remover(existente);
            _ = _uow.SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
            _ = _cache.InvalidarAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "Falha ao gravar a remoção propaga e não invalida o cache")]
    public async Task GravarRemocao_FalhaAoSalvar_PropagaSemInvalidarCache()
    {
        _uow.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("falha"));

        Func<Task> act = () => _gravacao.GravarRemocaoAsync(InstituicaoDeTeste.Criar().Value!, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _cache.DidNotReceive().InvalidarAsync(Arg.Any<CancellationToken>());
    }
}
