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

public sealed class ConferenciaDaNovaUnidadeTests
{
    private readonly CadastroDeUnidadeSimulado _cadastro = new();

    private Task<Result<Unidade>> ConferirAsync(CriarUnidadeCommand command) =>
        _cadastro.Conferencia.ConferirNovaAsync(command, CancellationToken.None);

    [Fact(DisplayName = "Payload com nome ausente e slug inválido acumula os dois erros sem consultar o repositório")]
    public async Task PayloadInvalido_AcumulaOsErrosSemConsultar()
    {
        Result<Unidade> resultado = await ConferirAsync(
            CadastroDeUnidadeSimulado.Criacao() with { Nome = "", Slug = "SLUG-COM-MAIUSCULAS" });

        resultado.Errors.Should().HaveCount(2);
        resultado.Errors.Should().Contain(e => e.Field == "nome" && e.Error.Code == UnidadeErrorCodes.NomeObrigatorio);
        resultado.Errors.Should().Contain(e => e.Field == "slug" && e.Error.Code == UnidadeErrorCodes.SlugFormatoInvalido);
        _cadastro.Unidades.ReceivedCalls().Should().BeEmpty();
    }

    [Theory(DisplayName = "Tipo ausente, desconhecido ou em outra grafia acumula TipoInvalido com o nome ausente sem consultar o repositório")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("centro")]
    [InlineData("Centro")]
    [InlineData("3")]
    public async Task TipoQueNaoEhCodigo_AcumulaTipoInvalidoSemConsultar(string? tipo)
    {
        Result<Unidade> resultado = await ConferirAsync(CadastroDeUnidadeSimulado.Criacao() with { Nome = "", Tipo = tipo });

        resultado.Errors.Should().HaveCount(2);
        resultado.Errors.Should().Contain(e => e.Field == "nome" && e.Error.Code == UnidadeErrorCodes.NomeObrigatorio);
        resultado.Errors.Should().Contain(e => e.Field == "tipo" && e.Error.Code == UnidadeErrorCodes.TipoInvalido);
        _cadastro.Unidades.ReceivedCalls().Should().BeEmpty();
    }

    [Theory(DisplayName = "Cada código de tipo cria a Unidade do tipo correspondente")]
    [InlineData("REITORIA", TipoUnidade.Reitoria)]
    [InlineData("PRO_REITORIA", TipoUnidade.ProReitoria)]
    [InlineData("OUTRO", TipoUnidade.Outro)]
    public async Task CodigoDeTipo_CriaAUnidadeDoTipo(string codigo, TipoUnidade esperado)
    {
        Result<Unidade> resultado = await ConferirAsync(CadastroDeUnidadeSimulado.Criacao() with { Tipo = codigo });

        resultado.Value!.Tipo.Should().Be(esperado);
    }

    [Fact(DisplayName = "Sem identificador em uso nem superior devolve a Unidade e consulta só os três identificadores")]
    public async Task SemConflito_DevolveAUnidadeEConsultaOsTresIdentificadores()
    {
        Result<Unidade> resultado = await ConferirAsync(CadastroDeUnidadeSimulado.Criacao());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Sigla.Should().Be("CEPS");
        await _cadastro.Unidades.Received(1).SlugExisteEntreVivosAsync(UnidadeDeTeste.SlugDoCeps, null, Arg.Any<CancellationToken>());
        await _cadastro.Unidades.Received(1).SiglaExisteEntreVivosAsync("CEPS", null, Arg.Any<CancellationToken>());
        await _cadastro.Unidades.Received(1).CodigoExisteEntreVivosAsync("0001", null, Arg.Any<CancellationToken>());
        await _cadastro.Unidades.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Slug em uso recusa SlugJaExiste citando o slug, sem consultar sigla nem código")]
    public async Task SlugEmUso_RecusaSemConsultarOsDemais()
    {
        _cadastro.SlugEmUso();

        Result<Unidade> resultado = await ConferirAsync(CadastroDeUnidadeSimulado.Criacao());

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SlugJaExiste);
        resultado.Error.Message.Should().Contain("'ceps'");
        await _cadastro.SiglaNaoConsultadaAsync();
        await _cadastro.CodigoNaoConsultadoAsync();
    }

    [Fact(DisplayName = "Slug, sigla e código em uso recusam o slug primeiro")]
    public async Task TudoEmUso_RecusaOSlugPrimeiro()
    {
        _cadastro.SlugEmUso();
        _cadastro.SiglaEmUso();
        _cadastro.CodigoEmUso();

        Result<Unidade> resultado = await ConferirAsync(CadastroDeUnidadeSimulado.Criacao());

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SlugJaExiste);
    }

    [Fact(DisplayName = "Sigla e código em uso recusam a sigla antes do código")]
    public async Task SiglaECodigoEmUso_RecusaASiglaAntes()
    {
        _cadastro.SiglaEmUso();
        _cadastro.CodigoEmUso();

        Result<Unidade> resultado = await ConferirAsync(CadastroDeUnidadeSimulado.Criacao());

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SiglaJaExiste);
        resultado.Error.Message.Should().Contain("'CEPS'");
        await _cadastro.CodigoNaoConsultadoAsync();
    }

    [Fact(DisplayName = "Só o código em uso recusa CodigoJaExiste citando o código")]
    public async Task SoCodigoEmUso_RecusaOCodigo()
    {
        _cadastro.CodigoEmUso();

        Result<Unidade> resultado = await ConferirAsync(CadastroDeUnidadeSimulado.Criacao());

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.CodigoJaExiste);
        resultado.Error.Message.Should().Contain("'0001'");
    }

    [Fact(DisplayName = "Superior inexistente recusa SuperiorNaoEncontrado")]
    public async Task SuperiorInexistente_Recusa()
    {
        Guid superiorId = Guid.NewGuid();
        _cadastro.Unidades.ObterPorIdAsync(superiorId, Arg.Any<CancellationToken>()).Returns((Unidade?)null);

        Result<Unidade> resultado = await ConferirAsync(
            CadastroDeUnidadeSimulado.Criacao() with { UnidadeSuperiorId = superiorId });

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SuperiorNaoEncontrado);
    }

    [Fact(DisplayName = "Superior existente é aceito na criação sem verificar a descendência")]
    public async Task SuperiorExistente_AceitoSemVerificarDescendencia()
    {
        Guid superiorId = _cadastro.SuperiorExistente();

        Result<Unidade> resultado = await ConferirAsync(
            CadastroDeUnidadeSimulado.Criacao() with { UnidadeSuperiorId = superiorId });

        resultado.IsSuccess.Should().BeTrue();
        await _cadastro.Unidades.DidNotReceive().EhDescendenteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Identificador em uso é recusado antes de conferir o superior")]
    public async Task IdentificadorEmUso_RecusadoAntesDoSuperior()
    {
        _cadastro.SlugEmUso();

        Result<Unidade> resultado = await ConferirAsync(
            CadastroDeUnidadeSimulado.Criacao() with { UnidadeSuperiorId = Guid.NewGuid() });

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SlugJaExiste);
        await _cadastro.Unidades.DidNotReceive().ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
