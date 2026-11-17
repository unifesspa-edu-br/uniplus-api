namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class GravacaoDaUnidadeTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly CadastroDeUnidadeSimulado _cadastro = new();

    private void SalvarFalha() =>
        _cadastro.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new InvalidOperationException("falha ao salvar")));

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

    [Fact(DisplayName = "Gravar a atualização aplica o nome, salva e invalida o cache, nesta ordem")]
    public async Task GravarAtualizacao_AplicaSalvaEInvalida()
    {
        Unidade unidade = UnidadeDeTeste.Criar().Value!;
        AtualizarUnidadeCommand command = CadastroDeUnidadeSimulado.AtualizacaoSemMudancaDeIdentificador(unidade.Id);

        Result resultado = await _cadastro.Gravacao.GravarAtualizacaoAsync(unidade, command, Agora, CancellationToken.None);

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

        Result resultado = await _cadastro.Gravacao.GravarAtualizacaoAsync(unidade, command, Agora, CancellationToken.None);

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.VigenciaFimAnteriorAoInicio);
        await _cadastro.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await _cadastro.Cache.DidNotReceive().InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Mudar o slug registra a troca no histórico na data de agora")]
    public async Task GravarAtualizacao_NovoSlug_RegistraATrocaNaDataDeAgora()
    {
        Unidade unidade = UnidadeDeTeste.Criar().Value!;
        AtualizarUnidadeCommand command = CadastroDeUnidadeSimulado.AtualizacaoSemMudancaDeIdentificador(unidade.Id)
            with
        { Slug = "ceps-unifesspa", MotivoMudancaIdentificador = "Adequação de naming" };

        await _cadastro.Gravacao.GravarAtualizacaoAsync(unidade, command, Agora, CancellationToken.None);

        unidade.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Slug && h.Valor == "ceps" && h.VigenciaFim == new DateOnly(2026, 3, 15));
        unidade.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Slug && h.Valor == "ceps-unifesspa" && h.VigenciaFim == null);
    }

    [Fact(DisplayName = "Falha ao salvar a atualização propaga sem invalidar o cache")]
    public async Task GravarAtualizacao_FalhaAoSalvar_PropagaSemInvalidar()
    {
        Unidade unidade = UnidadeDeTeste.Criar().Value!;
        SalvarFalha();

        Func<Task> act = () => _cadastro.Gravacao.GravarAtualizacaoAsync(
            unidade, CadastroDeUnidadeSimulado.AtualizacaoSemMudancaDeIdentificador(unidade.Id), Agora, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _cadastro.Cache.DidNotReceive().InvalidarAsync(Arg.Any<CancellationToken>());
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
