namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenarioDaObrigatoriedadeLegal;

/// <summary>
/// Os campos são conferidos primeiro, pelo domínio e todos juntos; só um lote sem violação de campo chega às
/// consultas. Um campo em branco nunca vira busca por texto vazio, que sairia como "não encontrado".
/// </summary>
public sealed class ObrigatoriedadeLegalOrdemDasConferenciasTests
{
    private readonly CenarioDaObrigatoriedadeLegal _cenario = new();

    [Fact(DisplayName = "Criar com vários campos inválidos devolve todas as violações e não consulta nada")]
    public async Task Criar_CamposInvalidos_AcumulaESemConsultas()
    {
        ITipoProcessoReader tipos = _cenario.TiposDeProcesso = Substitute.For<ITipoProcessoReader>();
        CriarObrigatoriedadeLegalCommand comando = ComandoDeCriar() with
        {
            TipoProcessoCodigo = "  ",
            RegraCodigo = null,
            DescricaoHumana = null,
            VigenciaInicio = null,
        };

        Result<Guid> resultado = await _cenario.CriarAsync(comando);

        resultado.Errors.Select(e => e.Field).Should().Equal("tipoProcessoCodigo", "regraCodigo", "descricaoHumana", "vigenciaInicio");
        await tipos.DidNotReceiveWithAnyArgs().ObterAtivoPorCodigoAsync(default!, default);
        await _cenario.Repositorio.DidNotReceiveWithAnyArgs().ExisteRegraCodigoAtivoAsync(default!, default, default);
    }

    [Fact(DisplayName = "Tipo de processo em branco é recusado pelo campo, não como tipo inexistente")]
    public async Task Criar_TipoProcessoEmBranco_RecusaPeloCampo()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(ComandoDeCriar() with { TipoProcessoCodigo = null });

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ObrigatoriedadeLegal.TipoProcessoCodigoObrigatorio");
    }

    [Fact(DisplayName = "Predicado ausente é violação de campo, não erro de leitura do corpo")]
    public async Task Criar_PredicadoAusente_ViolacaoDeCampo()
    {
        Result<Guid> resultado = await _cenario.CriarAsync(ComandoDeCriar() with { Predicado = null });

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("predicado");
    }

    [Fact(DisplayName = "Atualizar com campos inválidos recusa antes de buscar a regra")]
    public async Task Atualizar_CamposInvalidos_RecusaAntesDeBuscarARegra()
    {
        Result resultado = await _cenario.AtualizarAsync(ComandoDeAtualizar(Guid.Empty) with { Categoria = CategoriaObrigatoriedade.Nenhuma });

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("categoria");
        await _cenario.Repositorio.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
    }

    [Fact(DisplayName = "Atualizar regra inexistente com campos válidos é não encontrada, mesmo com Id vazio")]
    public async Task Atualizar_RegraInexistente_NaoEncontrada()
    {
        Result resultado = await _cenario.AtualizarAsync(ComandoDeAtualizar(Guid.Empty));

        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.NaoEncontrada");
    }

    [Fact(DisplayName = "Atualizar só muda a regra rastreada depois de todas as recusas")]
    public async Task Atualizar_ReferenciaInexistente_NaoMudaARegra()
    {
        ObrigatoriedadeLegal regra = RegraGravada();
        PredicadoObrigatoriedade anterior = regra.Predicado;
        _cenario.ComRegraGravada(regra);
        _cenario.TiposDeEtapa = LeitoresDeCadastroParaObrigatoriedadeLegal.TipoEtapaReaderComResposta(null);

        Result resultado = await _cenario.AtualizarAsync(ComandoDeAtualizar(regra.Id));

        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.TipoEtapaNaoEncontradoOuInativo");
        regra.Predicado.Should().Be(anterior);
        _cenario.Repositorio.DidNotReceive().Atualizar(Arg.Any<ObrigatoriedadeLegal>());
    }
}
