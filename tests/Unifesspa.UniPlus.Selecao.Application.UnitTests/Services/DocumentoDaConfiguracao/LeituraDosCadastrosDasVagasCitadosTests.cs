namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os cadastros das vagas citados são lidos em lote, uma vez por cadastro e só quando o documento cita código; o código que o
/// cadastro não tem vivo fica fora do dicionário, e a referência demográfica é identificada pelo censo de referência.
/// </summary>
public sealed class LeituraDosCadastrosDasVagasCitadosTests
{
    private static readonly CodigosCitadosPorCadastro SemCodigos = new([], null, [], [], [], null, [], []);

    private readonly IOfertaCursoReader _ofertas = Substitute.For<IOfertaCursoReader>();
    private readonly IModalidadeReader _modalidades = Substitute.For<IModalidadeReader>();
    private readonly IReferenciaReservaDemograficaReader _referencias = Substitute.For<IReferenciaReservaDemograficaReader>();
    private readonly ICondicaoAtendimentoReader _condicoes = Substitute.For<ICondicaoAtendimentoReader>();
    private readonly IRecursoAcessibilidadeReader _recursos = Substitute.For<IRecursoAcessibilidadeReader>();
    private readonly ITipoDeficienciaReader _deficiencias = Substitute.For<ITipoDeficienciaReader>();
    private readonly IBaseLegalDoFadaReader _basesLegais = Substitute.For<IBaseLegalDoFadaReader>();
    private readonly LeituraDosCadastrosDasVagasCitados _leitura;

    public LeituraDosCadastrosDasVagasCitadosTests() =>
        _leitura = new(_ofertas, _modalidades, _referencias, _condicoes, _recursos, _deficiencias, _basesLegais);

    private static OfertaCursoView Oferta(string codigo) => new(
        Guid.CreateVersion7(), codigo, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
        "CTIC", "Centro de Tecnologia", "CAMPUS", "REGULAR", "PRESENCIAL", "EXTENSIVO",
        "REGULAR", ["MATUTINO"], null, null, 80, null, null, new GrupoAreaEnemView("GRUPO_A", "Grupo A"), $"Curso {codigo}", "Bacharelado");

    private static ReferenciaReservaDemograficaView Referencia(string censo, decimal ppi = 50m) =>
        new(Guid.CreateVersion7(), censo, ppi, 10m, 10m, "Censo de teste");

    private static IReadOnlyCollection<string> Lote(params string[] codigos) =>
        Arg.Is<IReadOnlyCollection<string>>(lote => lote.SequenceEqual(codigos));

    [Fact(DisplayName = "Cada cadastro com código citado é lido numa só chamada, com os códigos distintos, e cada código volta pelo cadastro")]
    public async Task CadaCadastro_ELidoEmUmaChamadaComOsCodigosDistintos()
    {
        _ofertas.ListarVivasPorCodigosAsync(Lote("OFERTA_A", "OFERTA_B"), Arg.Any<CancellationToken>()).Returns([Oferta("OFERTA_A"), Oferta("OFERTA_B")]);
        _modalidades.ListarVivosPorCodigosAsync(Lote("AMPLA"), Arg.Any<CancellationToken>()).Returns([CadastrosVivos.Modalidade("AMPLA")]);
        _referencias.ListarVivasPorCodigosAsync(Lote("2022"), Arg.Any<CancellationToken>()).Returns([Referencia("2022")]);
        _condicoes.ListarVivosPorCodigosAsync(Lote("PCD"), Arg.Any<CancellationToken>()).Returns([new CondicaoAtendimentoView(Guid.CreateVersion7(), "PCD", "Pessoa com deficiência")]);
        _recursos.ListarVivosPorCodigosAsync(Lote("LIBRAS"), Arg.Any<CancellationToken>()).Returns([new RecursoAcessibilidadeView(Guid.CreateVersion7(), "LIBRAS", "Intérprete de Libras")]);
        _deficiencias.ListarVivosPorCodigosAsync(Lote("VISUAL"), Arg.Any<CancellationToken>()).Returns([new TipoDeficienciaView(Guid.CreateVersion7(), "VISUAL", "Visual", "Descrição", null)]);

        CadastrosDasVagasEDasExigenciasCitados lidos = await _leitura.LerAsync(
            SemCodigos with
            {
                OfertasDeCurso = ["OFERTA_A", "OFERTA_B"],
                Modalidades = ["AMPLA"],
                ReferenciasDemograficas = ["2022"],
                CondicoesDeAtendimento = ["PCD"],
                RecursosDeAcessibilidade = ["LIBRAS"],
                TiposDeDeficiencia = ["VISUAL"],
            },
            CancellationToken.None);

        lidos.OfertasDeCurso.Keys.Should().Equal("OFERTA_A", "OFERTA_B");
        lidos.OfertasDeCurso["OFERTA_A"].Should().Match<OfertaDeCursoCitada>(
            oferta => oferta.CursoNome == "Curso OFERTA_A" && oferta.VagasAnuaisAutorizadas == 80 && oferta.GrupoAreaEnemCodigo == "GRUPO_A" && oferta.GrupoAreaEnemRotulo == "Grupo A");
        lidos.Modalidades["AMPLA"].Codigo.Should().Be("AMPLA");
        lidos.ReferenciasDemograficas.Keys.Should().Equal("2022");
        lidos.CondicoesDeAtendimento["PCD"].Nome.Should().Be("Pessoa com deficiência");
        lidos.RecursosDeAcessibilidade.Keys.Should().Equal("LIBRAS");
        lidos.TiposDeDeficiencia.Keys.Should().Equal("VISUAL");
        await _ofertas.Received(1).ListarVivasPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
        await _modalidades.Received(1).ListarVivosPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O cadastro de que o documento não cita código não é consultado")]
    public async Task CadastroSemCodigoCitado_NaoEConsultado()
    {
        CadastrosDasVagasEDasExigenciasCitados lidos = await _leitura.LerAsync(SemCodigos, CancellationToken.None);

        lidos.OfertasDeCurso.Should().BeEmpty();
        lidos.BasesLegaisDoFada.Should().BeEmpty();
        await _ofertas.DidNotReceiveWithAnyArgs().ListarVivasPorCodigosAsync(default!, default);
        await _modalidades.DidNotReceiveWithAnyArgs().ListarVivosPorCodigosAsync(default!, default);
        await _referencias.DidNotReceiveWithAnyArgs().ListarVivasPorCodigosAsync(default!, default);
        await _condicoes.DidNotReceiveWithAnyArgs().ListarVivosPorCodigosAsync(default!, default);
        await _recursos.DidNotReceiveWithAnyArgs().ListarVivosPorCodigosAsync(default!, default);
        await _deficiencias.DidNotReceiveWithAnyArgs().ListarVivosPorCodigosAsync(default!, default);
        await _basesLegais.DidNotReceiveWithAnyArgs().ObterPorCodigoAsync(default!, default);
    }

    [Fact(DisplayName = "O código que o cadastro não tem vivo fica fora do dicionário, sem recusar a leitura")]
    public async Task CodigoQueOCadastroNaoTem_FicaFora()
    {
        _modalidades.ListarVivosPorCodigosAsync(default!, default).ReturnsForAnyArgs([CadastrosVivos.Modalidade("AMPLA")]);
        _basesLegais.ObterPorCodigoAsync(default!, default).ReturnsForAnyArgs((BaseLegalDoFadaView?)null);

        CadastrosDasVagasEDasExigenciasCitados lidos = await _leitura.LerAsync(
            SemCodigos with { Modalidades = ["AMPLA", "INEXISTENTE"], BaseLegalDoFada = "SEM_CADASTRO" }, CancellationToken.None);

        lidos.Modalidades.Keys.Should().Equal("AMPLA");
        lidos.BasesLegaisDoFada.Should().BeEmpty();
    }

    [Fact(DisplayName = "A referência demográfica que o domínio recusa copiar fica fora do dicionário")]
    public async Task ReferenciaDemografica_RecusadaPeloDominio_FicaFora()
    {
        _referencias.ListarVivasPorCodigosAsync(default!, default).ReturnsForAnyArgs([Referencia("2010", ppi: 150m), Referencia("2022")]);

        CadastrosDasVagasEDasExigenciasCitados lidos = await _leitura.LerAsync(SemCodigos with { ReferenciasDemograficas = ["2010", "2022"] }, CancellationToken.None);

        lidos.ReferenciasDemograficas.Keys.Should().Equal("2022");
    }

    [Fact(DisplayName = "A base legal do FADA é lida pelo código, uma vez, e congela o identificador, o instrumento e os municípios")]
    public async Task BaseLegalDoFada_ECongeladaPeloCodigo()
    {
        BaseLegalDoFadaView baseLegal = new(
            Guid.CreateVersion7(), "PORTARIA_2514", "PORTARIA", "Portaria de teste", "Descrição de teste", [new("1500000", "Município de teste", "PA")]);
        _basesLegais.ObterPorCodigoAsync("PORTARIA_2514", Arg.Any<CancellationToken>()).Returns(baseLegal);

        CadastrosDasVagasEDasExigenciasCitados lidos = await _leitura.LerAsync(SemCodigos with { BaseLegalDoFada = "PORTARIA_2514" }, CancellationToken.None);

        BaseLegalCongelada congelada = lidos.BasesLegaisDoFada["PORTARIA_2514"];
        congelada.Id.Should().Be(baseLegal.Id);
        congelada.TipoInstrumento.Should().Be("PORTARIA");
        congelada.Municipios.Should().Equal([("1500000", "Município de teste", "PA")]);
        await _basesLegais.Received(1).ObterPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
