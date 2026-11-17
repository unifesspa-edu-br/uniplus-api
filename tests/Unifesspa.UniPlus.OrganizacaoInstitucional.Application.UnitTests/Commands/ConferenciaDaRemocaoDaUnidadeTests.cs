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

public sealed class ConferenciaDaRemocaoDaUnidadeTests
{
    private readonly CadastroDeUnidadeSimulado _cadastro = new();

    private Task<Result<Unidade>> ConferirAsync(Guid id) =>
        _cadastro.Conferencia.ConferirRemocaoAsync(id, CancellationToken.None);

    [Fact(DisplayName = "Unidade inexistente recusa NaoEncontrada sem consultar subordinadas nem Instituição")]
    public async Task UnidadeInexistente_Recusa()
    {
        Result<Unidade> resultado = await ConferirAsync(Guid.NewGuid());

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.NaoEncontrada);
        await _cadastro.Unidades.DidNotReceive().PossuiSubordinadasVivasAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _cadastro.Instituicoes.DidNotReceive().ExisteComUnidadeRaizAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Subordinadas vivas recusam RemocaoBloqueadaPorSubordinadas")]
    public async Task ComSubordinadas_Recusa()
    {
        Unidade unidade = _cadastro.UnidadeCadastrada();
        _cadastro.Unidades.PossuiSubordinadasVivasAsync(unidade.Id, Arg.Any<CancellationToken>()).Returns(true);

        Result<Unidade> resultado = await ConferirAsync(unidade.Id);

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.RemocaoBloqueadaPorSubordinadas);
    }

    [Fact(DisplayName = "Subordinadas vivas e raiz de Instituição recusam pelas subordinadas, sem consultar a Instituição")]
    public async Task ComSubordinadasERaiz_RecusaPelasSubordinadasSemConsultarAInstituicao()
    {
        Unidade unidade = _cadastro.UnidadeCadastrada();
        _cadastro.Unidades.PossuiSubordinadasVivasAsync(unidade.Id, Arg.Any<CancellationToken>()).Returns(true);
        _cadastro.Instituicoes.ExisteComUnidadeRaizAsync(unidade.Id, Arg.Any<CancellationToken>()).Returns(true);

        Result<Unidade> resultado = await ConferirAsync(unidade.Id);

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.RemocaoBloqueadaPorSubordinadas);
        await _cadastro.Instituicoes.DidNotReceive().ExisteComUnidadeRaizAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Raiz de Instituição viva recusa RemocaoBloqueadaPorInstituicao")]
    public async Task RaizDeInstituicao_Recusa()
    {
        Unidade unidade = _cadastro.UnidadeCadastrada();
        _cadastro.Instituicoes.ExisteComUnidadeRaizAsync(unidade.Id, Arg.Any<CancellationToken>()).Returns(true);

        Result<Unidade> resultado = await ConferirAsync(unidade.Id);

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.RemocaoBloqueadaPorInstituicao);
    }

    [Fact(DisplayName = "Sem subordinadas nem raiz de Instituição devolve a Unidade")]
    public async Task SemRecusa_DevolveAUnidade()
    {
        Unidade unidade = _cadastro.UnidadeCadastrada();

        Result<Unidade> resultado = await ConferirAsync(unidade.Id);

        resultado.Value.Should().BeSameAs(unidade);
    }
}
