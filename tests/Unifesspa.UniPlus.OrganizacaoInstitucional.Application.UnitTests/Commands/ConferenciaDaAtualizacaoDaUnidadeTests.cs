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

public sealed class ConferenciaDaAtualizacaoDaUnidadeTests
{
    private readonly CadastroDeUnidadeSimulado _cadastro = new();

    private Task<Result<Unidade>> ConferirAsync(AtualizarUnidadeCommand command) =>
        _cadastro.Conferencia.ConferirAtualizacaoAsync(command, CancellationToken.None);

    private static AtualizarUnidadeCommand SemMudanca(Unidade unidade) =>
        CadastroDeUnidadeSimulado.AtualizacaoSemMudancaDeIdentificador(unidade.Id);

    [Fact(DisplayName = "Motivo com mais de 500 caracteres e sigla ausente acumulam os dois erros sem buscar a Unidade")]
    public async Task PayloadInvalido_AcumulaOsErrosSemBuscar()
    {
        AtualizarUnidadeCommand command = CadastroDeUnidadeSimulado.AtualizacaoSemMudancaDeIdentificador(Guid.NewGuid())
            with
        { Sigla = null, MotivoMudancaIdentificador = new string('m', 501) };

        Result<Unidade> resultado = await ConferirAsync(command);

        resultado.Errors.Select(e => e.Error.Code).Should()
            .BeEquivalentTo([UnidadeErrorCodes.SiglaObrigatoria, UnidadeErrorCodes.MotivoMudancaTamanho]);
        _cadastro.Unidades.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "Tipo desconhecido recusa TipoInvalido sem buscar a Unidade")]
    public async Task TipoDesconhecido_RecusaSemBuscar()
    {
        Result<Unidade> resultado = await ConferirAsync(
            CadastroDeUnidadeSimulado.AtualizacaoSemMudancaDeIdentificador(Guid.NewGuid()) with { Tipo = "NENHUM" });

        resultado.Errors.Should().ContainSingle(e => e.Field == "tipo" && e.Error.Code == UnidadeErrorCodes.TipoInvalido);
        _cadastro.Unidades.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "Unidade inexistente recusa NaoEncontrada antes de conferir a unicidade")]
    public async Task UnidadeInexistente_RecusaAntesDaUnicidade()
    {
        _cadastro.SlugEmUso();

        Result<Unidade> resultado = await ConferirAsync(
            CadastroDeUnidadeSimulado.AtualizacaoSemMudancaDeIdentificador(Guid.NewGuid()) with { Slug = "cga" });

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.NaoEncontrada);
        await _cadastro.SlugNaoConsultadoAsync();
    }

    [Fact(DisplayName = "Com os mesmos identificadores devolve a Unidade e não consulta nenhum")]
    public async Task IdentificadoresInalterados_NaoConsultaNenhum()
    {
        Unidade unidade = _cadastro.UnidadeCadastrada();

        Result<Unidade> resultado = await ConferirAsync(SemMudanca(unidade) with { Codigo = " 0001 " });

        resultado.Value.Should().BeSameAs(unidade);
        await _cadastro.SlugNaoConsultadoAsync();
        await _cadastro.SiglaNaoConsultadaAsync();
        await _cadastro.CodigoNaoConsultadoAsync();
    }

    [Fact(DisplayName = "Mudar só o slug consulta só o slug, excluindo a própria Unidade")]
    public async Task SlugNovo_ConsultaSoOSlug()
    {
        Unidade unidade = _cadastro.UnidadeCadastrada();
        _cadastro.SlugEmUso();

        Result<Unidade> resultado = await ConferirAsync(SemMudanca(unidade) with { Slug = "ceps-unifesspa" });

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SlugJaExiste);
        await _cadastro.Unidades.Received(1).SlugExisteEntreVivosAsync(Arg.Any<Slug>(), unidade.Id, Arg.Any<CancellationToken>());
        await _cadastro.SiglaNaoConsultadaAsync();
        await _cadastro.CodigoNaoConsultadoAsync();
    }

    [Fact(DisplayName = "Mudar só a sigla consulta só a sigla, excluindo a própria Unidade")]
    public async Task SiglaNova_ConsultaSoASigla()
    {
        Unidade unidade = _cadastro.UnidadeCadastrada();
        _cadastro.SiglaEmUso();

        Result<Unidade> resultado = await ConferirAsync(SemMudanca(unidade) with { Sigla = "CGA" });

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SiglaJaExiste);
        await _cadastro.Unidades.Received(1).SiglaExisteEntreVivosAsync("CGA", unidade.Id, Arg.Any<CancellationToken>());
        await _cadastro.SlugNaoConsultadoAsync();
        await _cadastro.CodigoNaoConsultadoAsync();
    }

    [Fact(DisplayName = "Mudar a sigla só na caixa não a consulta: a sigla não distingue maiúsculas")]
    public async Task SiglaSoNaCaixa_NaoConsulta()
    {
        Unidade unidade = _cadastro.UnidadeCadastrada();

        Result<Unidade> resultado = await ConferirAsync(SemMudanca(unidade) with { Sigla = "CePs" });

        resultado.IsSuccess.Should().BeTrue();
        await _cadastro.SiglaNaoConsultadaAsync();
    }

    [Fact(DisplayName = "Mudar o código só na caixa (ABC→abc) o consulta: o código distingue maiúsculas")]
    public async Task CodigoSoNaCaixa_Consulta()
    {
        Unidade unidade = _cadastro.UnidadeCadastrada(codigo: "ABC");
        _cadastro.CodigoEmUso();

        Result<Unidade> resultado = await ConferirAsync(SemMudanca(unidade) with { Codigo = "abc" });

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.CodigoJaExiste);
        await _cadastro.Unidades.Received(1).CodigoExisteEntreVivosAsync("abc", unidade.Id, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Superior igual à própria Unidade recusa SuperiorFormaCiclo sem consultar a existência")]
    public async Task SuperiorIgualAUnidade_RecusaSemConsultarExistencia()
    {
        Unidade unidade = _cadastro.UnidadeCadastrada();

        Result<Unidade> resultado = await ConferirAsync(SemMudanca(unidade) with { UnidadeSuperiorId = unidade.Id });

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SuperiorFormaCiclo);
        await _cadastro.Unidades.Received(1).ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Superior inexistente recusa SuperiorNaoEncontrado sem verificar a descendência")]
    public async Task SuperiorInexistente_RecusaSemVerificarDescendencia()
    {
        Unidade unidade = _cadastro.UnidadeCadastrada();
        Guid superiorId = Guid.NewGuid();
        _cadastro.Unidades.ObterPorIdAsync(superiorId, Arg.Any<CancellationToken>()).Returns((Unidade?)null);

        Result<Unidade> resultado = await ConferirAsync(SemMudanca(unidade) with { UnidadeSuperiorId = superiorId });

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SuperiorNaoEncontrado);
        await _cadastro.Unidades.DidNotReceive().EhDescendenteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Superior descendente da Unidade editada recusa SuperiorFormaCiclo")]
    public async Task SuperiorDescendente_RecusaCiclo()
    {
        Unidade unidade = _cadastro.UnidadeCadastrada();
        Guid superiorId = _cadastro.SuperiorExistente();
        _cadastro.Unidades.EhDescendenteAsync(superiorId, unidade.Id, Arg.Any<CancellationToken>()).Returns(true);

        Result<Unidade> resultado = await ConferirAsync(SemMudanca(unidade) with { UnidadeSuperiorId = superiorId });

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SuperiorFormaCiclo);
        resultado.Error.Message.Should().Contain("descendente");
    }

    [Fact(DisplayName = "Superior existente e fora da descendência é aceito")]
    public async Task SuperiorForaDaDescendencia_Aceito()
    {
        Unidade unidade = _cadastro.UnidadeCadastrada();
        Guid superiorId = _cadastro.SuperiorExistente();

        Result<Unidade> resultado = await ConferirAsync(SemMudanca(unidade) with { UnidadeSuperiorId = superiorId });

        resultado.Value.Should().BeSameAs(unidade);
    }

    [Fact(DisplayName = "Identificador em uso é recusado antes de conferir o superior")]
    public async Task IdentificadorEmUso_RecusadoAntesDoSuperior()
    {
        Unidade unidade = _cadastro.UnidadeCadastrada();
        _cadastro.SlugEmUso();

        Result<Unidade> resultado = await ConferirAsync(
            SemMudanca(unidade) with { Slug = "cga", UnidadeSuperiorId = unidade.Id });

        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SlugJaExiste);
    }
}
