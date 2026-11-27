namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class GravacaoDaUnidadeTests
{
    private static readonly DateOnly Dia = new(2026, 3, 15);

    private readonly CadastroDeUnidadeSimulado _cadastro = new();

    private void SalvarFalha() =>
        _cadastro.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new InvalidOperationException("falha ao salvar")));

    /// <summary>A gravação recusada pelo índice único como o provedor a reporta, com a restrição dada.</summary>
    private void GravacaoRecusadaPelaRestricao(string restricao) =>
        _cadastro.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException("violação sintética", new ViolacaoDeUnicidadeDeTeste(restricao)));

    private Task<Result<Guid>> GravarNovaAsync() =>
        _cadastro.Gravacao.GravarNovaAsync(UnidadeDeTeste.Criar().Value!, CancellationToken.None);

    private Task<Result> GravarAtualizacaoAsync() =>
        GravarAtualizacaoAsync(UnidadeDeTeste.Criar().Value!);

    private Task<Result> GravarAtualizacaoAsync(Unidade unidade) =>
        _cadastro.Gravacao.GravarAtualizacaoAsync(
            unidade, CadastroDeUnidadeSimulado.AtualizacaoSemMudancaDeIdentificador(unidade.Id), Dia, CancellationToken.None);

    private async Task ConferirQueDescartouSemInvalidarOCacheAsync()
    {
        Received.InOrder(() =>
        {
            _ = _cadastro.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
            _cadastro.UnitOfWork.DescartarAlteracoesNaoSalvas();
        });
        await _cadastro.Cache.DidNotReceive().InvalidarAsync(Arg.Any<CancellationToken>());
    }

    private async Task ConferirQueNaoDescartouNemInvalidouOCacheAsync()
    {
        _cadastro.UnitOfWork.DidNotReceive().DescartarAlteracoesNaoSalvas();
        await _cadastro.Cache.DidNotReceive().InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Gravar a nova Unidade adiciona, salva e invalida o cache, nesta ordem, e devolve o Id")]
    public async Task GravarNova_AdicionaSalvaEInvalidaEmOrdem()
    {
        Unidade nova = UnidadeDeTeste.Criar().Value!;

        Result<Guid> resultado = await _cadastro.Gravacao.GravarNovaAsync(nova, CancellationToken.None);

        resultado.Value.Should().Be(nova.Id);
        Received.InOrder(() =>
        {
            _ = _cadastro.Unidades.AdicionarAsync(nova, Arg.Any<CancellationToken>());
            _ = _cadastro.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
            _ = _cadastro.Cache.InvalidarAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "Falha ao salvar a nova Unidade propaga sem invalidar o cache")]
    public async Task GravarNova_FalhaAoSalvar_PropagaSemInvalidar()
    {
        SalvarFalha();

        Func<Task> act = () => _cadastro.Gravacao.GravarNovaAsync(UnidadeDeTeste.Criar().Value!, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _cadastro.Cache.DidNotReceive().InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "Corrida no índice único na criação vira a recusa do identificador, descarta o rastreamento e não invalida o cache")]
    [InlineData(RestricoesDoBanco.UnidadeSlugVivo, UnidadeErrorCodes.SlugJaExiste, "'ceps'")]
    [InlineData(RestricoesDoBanco.UnidadeSiglaAtivos, UnidadeErrorCodes.SiglaJaExiste, "'CEPS'")]
    [InlineData(RestricoesDoBanco.UnidadeCodigoAtivos, UnidadeErrorCodes.CodigoJaExiste, "'0001'")]
    public async Task GravarNova_ConflitoDoIndiceDoIdentificador_DevolveARecusaDescartandoSemInvalidarCache(
        string restricao, string codigoEsperado, string valorCitado)
    {
        GravacaoRecusadaPelaRestricao(restricao);

        Result<Guid> resultado = await GravarNovaAsync();

        resultado.Error!.Code.Should().Be(codigoEsperado);
        resultado.Error.Message.Should().Contain(valorCitado);
        await ConferirQueDescartouSemInvalidarOCacheAsync();
    }

    [Fact(DisplayName = "Violação de outra restrição única na criação propaga, sem descartar nem invalidar o cache")]
    public async Task GravarNova_ConflitoDeOutraRestricao_Propaga()
    {
        GravacaoRecusadaPelaRestricao("ix_outra_restricao");

        Func<Task> act = () => GravarNovaAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
        await ConferirQueNaoDescartouNemInvalidouOCacheAsync();
    }

    [Fact(DisplayName = "Gravar a atualização aplica o nome, salva e invalida o cache, nesta ordem")]
    public async Task GravarAtualizacao_AplicaSalvaEInvalida()
    {
        Unidade unidade = UnidadeDeTeste.Criar().Value!;
        AtualizarUnidadeCommand command = CadastroDeUnidadeSimulado.AtualizacaoSemMudancaDeIdentificador(unidade.Id);

        Result resultado = await _cadastro.Gravacao.GravarAtualizacaoAsync(unidade, command, Dia, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        unidade.Nome.Should().Be("Centro de Processos Seletivos (novo nome)");
        Received.InOrder(() =>
        {
            _ = _cadastro.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
            _ = _cadastro.Cache.InvalidarAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "Vigência final anterior ao início gravado não salva nem invalida o cache")]
    public async Task GravarAtualizacao_VigenciaIncoerente_NaoSalvaNemInvalida()
    {
        Unidade unidade = UnidadeDeTeste.Criar().Value!;
        AtualizarUnidadeCommand command = CadastroDeUnidadeSimulado.AtualizacaoSemMudancaDeIdentificador(unidade.Id)
            with
        { VigenciaFim = UnidadeDeTeste.InicioDaVigencia.AddDays(-1) };

        Result resultado = await _cadastro.Gravacao.GravarAtualizacaoAsync(unidade, command, Dia, CancellationToken.None);

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.VigenciaFimAnteriorAoInicio);
        await _cadastro.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await _cadastro.Cache.DidNotReceive().InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Mudar o slug registra a troca no histórico no dia informado")]
    public async Task GravarAtualizacao_NovoSlug_RegistraATrocaNoDiaInformado()
    {
        Unidade unidade = UnidadeDeTeste.Criar().Value!;
        AtualizarUnidadeCommand command = CadastroDeUnidadeSimulado.AtualizacaoSemMudancaDeIdentificador(unidade.Id)
            with
        { Slug = "ceps-unifesspa", MotivoMudancaIdentificador = "Adequação de naming" };

        await _cadastro.Gravacao.GravarAtualizacaoAsync(unidade, command, Dia, CancellationToken.None);

        unidade.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.IdentificadorNoEnderecoWeb && h.Valor == "ceps" && h.VigenciaFim == new DateOnly(2026, 3, 15));
        unidade.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.IdentificadorNoEnderecoWeb && h.Valor == "ceps-unifesspa" && h.VigenciaFim == null);
    }

    [Fact(DisplayName = "Falha ao salvar a atualização propaga sem invalidar o cache")]
    public async Task GravarAtualizacao_FalhaAoSalvar_PropagaSemInvalidar()
    {
        Unidade unidade = UnidadeDeTeste.Criar().Value!;
        SalvarFalha();

        Func<Task> act = () => _cadastro.Gravacao.GravarAtualizacaoAsync(
            unidade, CadastroDeUnidadeSimulado.AtualizacaoSemMudancaDeIdentificador(unidade.Id), Dia, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _cadastro.Cache.DidNotReceive().InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "Corrida no índice único na atualização vira a recusa do identificador, descarta o rastreamento e não invalida o cache")]
    [InlineData(RestricoesDoBanco.UnidadeSlugVivo, UnidadeErrorCodes.SlugJaExiste, "'ceps'")]
    [InlineData(RestricoesDoBanco.UnidadeSiglaAtivos, UnidadeErrorCodes.SiglaJaExiste, "'CEPS'")]
    [InlineData(RestricoesDoBanco.UnidadeCodigoAtivos, UnidadeErrorCodes.CodigoJaExiste, "'0001'")]
    public async Task GravarAtualizacao_ConflitoDoIndiceDoIdentificador_DevolveARecusaDescartandoSemInvalidarCache(
        string restricao, string codigoEsperado, string valorCitado)
    {
        GravacaoRecusadaPelaRestricao(restricao);

        Result resultado = await GravarAtualizacaoAsync();

        resultado.Error!.Code.Should().Be(codigoEsperado);
        resultado.Error.Message.Should().Contain(valorCitado);
        await ConferirQueDescartouSemInvalidarOCacheAsync();
    }

    [Fact(DisplayName = "Violação de outra restrição única na atualização propaga, sem descartar nem invalidar o cache")]
    public async Task GravarAtualizacao_ConflitoDeOutraRestricao_Propaga()
    {
        GravacaoRecusadaPelaRestricao("ix_outra_restricao");

        Func<Task> act = () => GravarAtualizacaoAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
        await ConferirQueNaoDescartouNemInvalidouOCacheAsync();
    }

    [Fact(DisplayName = "Gravar a remoção remove, salva e invalida o cache, nesta ordem")]
    public async Task GravarRemocao_RemoveSalvaEInvalida()
    {
        Unidade unidade = UnidadeDeTeste.Criar().Value!;

        await _cadastro.Gravacao.GravarRemocaoAsync(unidade, CancellationToken.None);

        Received.InOrder(() =>
        {
            _cadastro.Unidades.Remover(unidade);
            _ = _cadastro.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
            _ = _cadastro.Cache.InvalidarAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "Falha ao salvar a remoção propaga sem invalidar o cache")]
    public async Task GravarRemocao_FalhaAoSalvar_PropagaSemInvalidar()
    {
        SalvarFalha();

        Func<Task> act = () => _cadastro.Gravacao.GravarRemocaoAsync(UnidadeDeTeste.Criar().Value!, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _cadastro.Cache.DidNotReceive().InvalidarAsync(Arg.Any<CancellationToken>());
    }
}
