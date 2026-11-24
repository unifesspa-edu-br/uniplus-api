namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class AtualizarUnidadeCommandHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly CadastroDeUnidadeSimulado _cadastro = new();

    private Task<Result> AtualizarAsync(AtualizarUnidadeCommand command) =>
        AtualizarUnidadeCommandHandler.Handle(
            command, _cadastro.Conferencia, _cadastro.Gravacao, new RelogioFixo(Agora), CancellationToken.None);

    [Fact(DisplayName = "Unidade inexistente devolve NaoEncontrada sem tocar a unidade de trabalho nem o cache")]
    public async Task UnidadeInexistente_NaoGrava()
    {
        Result resultado = await AtualizarAsync(
            CadastroDeUnidadeSimulado.AtualizacaoSemMudancaDeIdentificador(Guid.NewGuid()));

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.NaoEncontrada);
        _cadastro.UnitOfWork.ReceivedCalls().Should().BeEmpty();
        _cadastro.Cache.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "Payload inválido devolve o erro sem buscar a Unidade")]
    public async Task PayloadInvalido_NaoBusca()
    {
        Result resultado = await AtualizarAsync(
            CadastroDeUnidadeSimulado.AtualizacaoSemMudancaDeIdentificador(Guid.NewGuid()) with { Sigla = "" });

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SiglaObrigatoria);
        _cadastro.Unidades.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "Conferência aceita grava e invalida o cache")]
    public async Task ConferenciaAceita_GravaEInvalida()
    {
        Unidade unidade = _cadastro.UnidadeCadastrada();

        Result resultado = await AtualizarAsync(
            CadastroDeUnidadeSimulado.AtualizacaoSemMudancaDeIdentificador(unidade.Id));

        resultado.IsSuccess.Should().BeTrue();
        await _cadastro.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await _cadastro.Cache.Received(1).InvalidarAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A troca de slug é registrada na data do relógio injetado")]
    public async Task TrocaDeSlug_UsaADataDoRelogio()
    {
        Unidade unidade = _cadastro.UnidadeCadastrada();

        await AtualizarAsync(
            CadastroDeUnidadeSimulado.AtualizacaoSemMudancaDeIdentificador(unidade.Id) with { Slug = "ceps-unifesspa" });

        unidade.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.IdentificadorNoEnderecoWeb && h.VigenciaFim == new DateOnly(2026, 3, 15));
    }
}
