namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenarioDaObrigatoriedadeLegal;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.LeitoresDeCadastroParaObrigatoriedadeLegal;

/// <summary>
/// Criar confere os campos pelas regras do domínio, só depois as referências e a unicidade, e persiste a regra com
/// o predicado já normalizado.
/// </summary>
public sealed class CriarObrigatoriedadeLegalCommandHandlerTests
{
    private readonly CenarioDaObrigatoriedadeLegal _cenario = new();

    [Fact(DisplayName = "Código de tipo ativo é aceito e a regra é persistida")]
    public async Task Handle_TipoAtivo_Persiste()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(ComandoDeCriar());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await _cenario.Repositorio.Received(1).AdicionarAsync(
            Arg.Is<ObrigatoriedadeLegal>(regra => regra.TipoProcessoCodigo == TipoProcessoDoCenario), Arg.Any<CancellationToken>());
        await _cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo de processo inexistente ou desativado é recusado sem persistir")]
    public async Task Handle_TipoProcessoInativo_Recusa()
    {
        _cenario.TiposDeProcesso = Substitute.For<ITipoProcessoReader>();

        Result<Guid> resultado = await _cenario.CriarAsync(ComandoDeCriar());

        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.TipoProcessoNaoEncontradoOuInativo");
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O sentinela universal é aceito sem consultar os tipos de processo")]
    public async Task Handle_TipoUniversal_NaoConsultaTiposAtivos()
    {
        ITipoProcessoReader tipos = _cenario.TiposDeProcesso = Substitute.For<ITipoProcessoReader>();

        Result<Guid> resultado = await _cenario.CriarAsync(ComandoDeCriar() with { TipoProcessoCodigo = "*" });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await tipos.DidNotReceiveWithAnyArgs().ObterAtivoPorCodigoAsync(default!, default);
    }

    [Fact(DisplayName = "Tipo de etapa inexistente é recusado sem persistir")]
    public async Task Handle_TipoEtapaInativo_Recusa()
    {
        _cenario.TiposDeEtapa = TipoEtapaReaderComResposta(null);

        Result<Guid> resultado = await _cenario.CriarAsync(ComandoDeCriar());

        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.TipoEtapaNaoEncontradoOuInativo");
        await _cenario.Repositorio.DidNotReceive().AdicionarAsync(Arg.Any<ObrigatoriedadeLegal>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Código de tipo de etapa nulo é defeito de campo, não de cadastro, e não chega ao cadastro")]
    public async Task Handle_TipoEtapaCodigoNulo_RecusaPelaFormaSemConsultar()
    {
        ITipoEtapaReader tipos = _cenario.TiposDeEtapa = Substitute.For<ITipoEtapaReader>();

        Result<Guid> resultado = await _cenario.CriarAsync(ComandoDeCriar(new EtapaObrigatoria(null!)));

        resultado.Errors.Should().ContainSingle().Which.Should()
            .Match<FieldError>(e => e.Error.Code == "ObrigatoriedadeLegal.PredicadoComCodigoEmBranco" && e.Field == "predicado");
        await tipos.DidNotReceiveWithAnyArgs().ObterAtivoPorCodigoAsync(default!, default);
    }

    [Fact(DisplayName = "O predicado persistido é o normalizado e o hash nasce dele")]
    public async Task Handle_TipoEtapaComEspacos_PersisteNormalizadoComHashDoConteudoFinal()
    {
        ObrigatoriedadeLegal? persistida = null;
        await _cenario.Repositorio.AdicionarAsync(Arg.Do<ObrigatoriedadeLegal>(r => persistida = r), Arg.Any<CancellationToken>());

        await _cenario.CriarAsync(ComandoDeCriar(new EtapaObrigatoria($"  {TipoEtapaCodigo}  ")));

        persistida!.Predicado.Should().Be(new EtapaObrigatoria(TipoEtapaCodigo));
        persistida.Hash.Should().Be(NovaRegraComPredicado(new EtapaObrigatoria(TipoEtapaCodigo)).Hash);
    }

    [Fact(DisplayName = "Predicado sem referência a cadastro não consulta os tipos de etapa")]
    public async Task Handle_PredicadoNaoEEtapa_NaoConsultaTiposDeEtapa()
    {
        ITipoEtapaReader tipos = _cenario.TiposDeEtapa = Substitute.For<ITipoEtapaReader>();

        Result<Guid> resultado = await _cenario.CriarAsync(ComandoDeCriar(new ConcorrenciaDuplaObrigatoria()));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await tipos.DidNotReceiveWithAnyArgs().ObterAtivoPorCodigoAsync(default!, default);
    }

    [Fact(DisplayName = "Código de regra já usado por regra ativa é recusado como duplicado")]
    public async Task Handle_RegraCodigoDuplicado_Recusa()
    {
        _cenario.Repositorio.ExisteRegraCodigoAtivoAsync("REGRA_NOVA", null, Arg.Any<CancellationToken>()).Returns(true);

        Result<Guid> resultado = await _cenario.CriarAsync(ComandoDeCriar());

        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.RegraCodigoDuplicada");
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private static ObrigatoriedadeLegal NovaRegraComPredicado(PredicadoObrigatoriedade predicado) =>
        ObrigatoriedadeLegal.Criar(
            TipoProcessoDoCenario,
            CategoriaObrigatoriedade.Etapa,
            "REGRA_NOVA",
            predicado,
            new TextoNormativo("Descrição da regra.", "Lei de teste.", null, null),
            new DateOnly(2026, 1, 1),
            null).Value!;
}
