namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os cadastros citados são lidos num instante único, de uma só leitura do relógio, e cada código vai uma vez ao cadastro. A
/// localidade que o Geo não confirma fica fora do dicionário, e a leitura não falha por isso: o rascunho é salvo mesmo sem o Geo.
/// Só a resposta de município inexistente e o código sem forma de município ficam sem causa; o Geo fora do ar ou com resposta
/// inconsistente (outra UF, nome recusado, erro desconhecido) deixa a causa da falta de conferência.
/// </summary>
public sealed class LeituraDosCadastrosCitadosTests
{
    // 23h30 de 16/11 em Belém: o dia civil do fuso institucional é 16, o dia UTC é 17.
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 2, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly DiaCivil = new(2026, 11, 16);
    private static readonly CodigosCitadosPorCadastro SemCodigos = new([], null, [], [], [], null, [], []);

    private readonly IUnidadeReader _unidades = Substitute.For<IUnidadeReader>();
    private readonly ITipoAtoPublicadoReader _tiposDeAto = Substitute.For<ITipoAtoPublicadoReader>();
    private readonly IRegraCatalogoReader _regras = Substitute.For<IRegraCatalogoReader>();
    private readonly IConferenciaEnderecoGeo _geo = Substitute.For<IConferenciaEnderecoGeo>();
    private readonly TimeProvider _relogio = Substitute.For<TimeProvider>();
    private readonly LeituraDosCadastrosCitados _leitura;

    public LeituraDosCadastrosCitadosTests()
    {
        _relogio.GetUtcNow().Returns(Agora);
        _tiposDeAto.ListarVigentesAsync(default!, default, default).ReturnsForAnyArgs([]);
        _leitura = new(
            _unidades,
            _tiposDeAto,
            _regras,
            _geo,
            new(
                Substitute.For<ITipoProcessoReader>(),
                Substitute.For<IFaseCanonicaReader>(),
                Substitute.For<ITipoBancaReader>(),
                Substitute.For<ICategoriaDocumentoReader>()),
            new(_relogio, new ResolvedorFusoDeTeste()));
    }

    private static UnidadeView Unidade(string codigo) =>
        new(Guid.CreateVersion7(), codigo, codigo, codigo.ToLowerInvariant(), $"Unidade {codigo}", null, "ADMINISTRATIVA", false, null);

    private static RegraCatalogo Regra(string codigo, TipoRegra tipo) =>
        RegraCatalogo.Criar(codigo, "v1", tipo, JsonSerializer.SerializeToElement(new { }), JsonSerializer.SerializeToElement(Array.Empty<string>()), "base legal de teste");

    private static DomainError Erro(string codigo) => new(codigo, "Resposta do Geo (dublê de teste).");

    private Task<Result<CadastrosCitadosNoDocumento>> Ler(CodigosCitadosPorCadastro codigos) =>
        _leitura.LerAsync(codigos, CancellationToken.None);

    private void GeoResponde(string codigoIbge, Result<string> resposta) =>
        _geo.ConferirMunicipioAsync(codigoIbge, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(resposta);

    [Fact(DisplayName = "O relógio é lido uma só vez, e o instante dos cadastros é o dessa leitura")]
    public async Task Relogio_ELidoUmaSoVez()
    {
        Result<CadastrosCitadosNoDocumento> lidos = await Ler(SemCodigos with { TiposDeAto = ["AVISO", "RESULTADO"] });

        lidos.Value!.LidoEm.Should().Be(Agora);
        _relogio.Received(1).GetUtcNow();
    }

    [Fact(DisplayName = "O tipo de ato vale no dia civil do fuso institucional, numa só consulta, e só os vigentes entram")]
    public async Task TipoDeAto_ValeNoDiaCivilDoFuso()
    {
        _tiposDeAto.ListarVigentesAsync(default!, default, default).ReturnsForAnyArgs([new TipoAtoPublicadoView("AVISO", "Aviso", false, false, false, false)]);

        Result<CadastrosCitadosNoDocumento> lidos = await Ler(SemCodigos with { TiposDeAto = ["AVISO", "INEXISTENTE"] });

        lidos.Value!.TiposDeAto.Should().BeEquivalentTo("AVISO");
        await _tiposDeAto.Received(1).ListarVigentesAsync(Arg.Is<IReadOnlyCollection<string>>(c => c.SequenceEqual(new[] { "AVISO", "INEXISTENTE" })), DiaCivil, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem tipo de ato citado o catálogo de publicações não é consultado")]
    public async Task SemTipoDeAtoCitado_NaoConsultaOCatalogo()
    {
        await Ler(SemCodigos);

        await _tiposDeAto.DidNotReceiveWithAnyArgs().ListarVigentesAsync(default!, default, default);
    }

    [Fact(DisplayName = "A falha do fuso institucional é falha da leitura, antes de qualquer cadastro")]
    public async Task FusoNaoReconhecido_EFalha()
    {
        LeituraDosCadastrosCitados leitura = new(
            _unidades, _tiposDeAto, _regras, _geo,
            new(Substitute.For<ITipoProcessoReader>(), Substitute.For<IFaseCanonicaReader>(), Substitute.For<ITipoBancaReader>(), Substitute.For<ICategoriaDocumentoReader>()),
            new(_relogio, new ResolvedorFusoIndisponivelDeTeste()));

        Result<CadastrosCitadosNoDocumento> lidos = await leitura.LerAsync(SemCodigos with { Unidades = ["CEPS"] }, CancellationToken.None);

        lidos.IsFailure.Should().BeTrue();
        await _unidades.DidNotReceiveWithAnyArgs().ObterPorCodigoAsync(default!, default);
    }

    [Fact(DisplayName = "Cada unidade citada é lida uma vez e fica pelo código, e a que o cadastro não tem vivo fica fora")]
    public async Task Unidades_SaoLidasUmaVezEFicamPeloCodigo()
    {
        UnidadeView ceps = Unidade("CEPS");
        _unidades.ObterPorCodigoAsync("CEPS", Arg.Any<CancellationToken>()).Returns(ceps);
        _unidades.ObterPorCodigoAsync("EXTINTA", Arg.Any<CancellationToken>()).Returns((UnidadeView?)null);

        Result<CadastrosCitadosNoDocumento> lidos = await Ler(SemCodigos with { Unidades = ["CEPS", "EXTINTA"] });

        lidos.Value!.Unidades.Keys.Should().Equal("CEPS");
        lidos.Value.Unidades["CEPS"].OrigemId.Should().Be(ceps.Id);
        await _unidades.Received(1).ObterPorCodigoAsync("CEPS", Arg.Any<CancellationToken>());
        await _unidades.Received(1).ObterPorCodigoAsync("EXTINTA", Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A unidade que o domínio recusa copiar é falha da leitura, que para na primeira falha e não lê as demais")]
    public async Task Unidade_QueODominioRecusa_EFalhaEParaALeitura()
    {
        _unidades.ObterPorCodigoAsync("RUIM", Arg.Any<CancellationToken>()).Returns(Unidade("RUIM") with { CidadeNome = "Marabá" });

        Result<CadastrosCitadosNoDocumento> lidos = await Ler(SemCodigos with { Unidades = ["RUIM", "CEPS"] });

        lidos.IsFailure.Should().BeTrue();
        await _unidades.DidNotReceive().ObterPorCodigoAsync("CEPS", Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A regra do catálogo é lida pelo par de código e versão, com o tipo e a referência que o processo congela")]
    public async Task Regra_ELidaPeloParDeCodigoEVersao()
    {
        RegraCatalogo regra = Regra("CONTAGEM", TipoRegra.ConvencaoContagemPrazo);
        _regras.ObterAsync("CONTAGEM", "v1", Arg.Any<CancellationToken>()).Returns(regra);

        Result<CadastrosCitadosNoDocumento> lidos = await Ler(SemCodigos with { Regras = [("CONTAGEM", "v1"), ("OUTRA", "v1")] });

        lidos.Value!.Regras.Keys.Should().Equal(("CONTAGEM", "v1"));
        lidos.Value.Regras[("CONTAGEM", "v1")].Should().Be(new RegraDoCatalogoCitada(TipoRegra.ConvencaoContagemPrazo, regra.Referencia()));
    }

    [Fact(DisplayName = "O município que o Geo conhece fica pelo código, conferido na UF do prefixo do código IBGE")]
    public async Task Municipio_ConhecidoFicaPeloCodigoNaUfDoPrefixo()
    {
        GeoResponde("1504208", Result<string>.Success("Marabá"));

        Result<CadastrosCitadosNoDocumento> lidos = await Ler(SemCodigos with { Localidade = "1504208" });

        lidos.Value!.Localidades["1504208"].Should().Be(LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value);
        lidos.Value.Localidades["1504208"].Uf.Should().Be("PA");
        await _geo.Received(1).ConferirMunicipioAsync("1504208", "PA", Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O município que o Geo diz não existir fica fora do dicionário sem falhar a leitura")]
    public async Task Municipio_InexistenteFicaFora()
    {
        GeoResponde("1599999", Result<string>.Failure(Erro(ConferenciaEnderecoGeoErrorCodes.MunicipioNaoEncontrado)));

        Result<CadastrosCitadosNoDocumento> lidos = await Ler(SemCodigos with { Localidade = "1599999" });

        lidos.IsSuccess.Should().BeTrue();
        lidos.Value!.Localidades.Should().BeEmpty();
        lidos.Value.LocalidadeNaoConferida.Should().BeNull();
    }

    [Fact(DisplayName = "O município que o Geo diz ser de outra UF que a do prefixo do código é resposta inconsistente do Geo, e não município inexistente")]
    public async Task Municipio_DeOutraUf_ContaComoGeoQueNaoRespondeu()
    {
        GeoResponde("1504208", Result<string>.Failure(Erro(ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente)));

        Result<CadastrosCitadosNoDocumento> lidos = await Ler(SemCodigos with { Localidade = "1504208" });

        lidos.IsSuccess.Should().BeTrue();
        lidos.Value!.Localidades.Should().BeEmpty();
        lidos.Value.LocalidadeNaoConferida!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
    }

    [Fact(DisplayName = "O nome que o Geo devolve e o domínio recusa conta como Geo que não respondeu, e não como município inexistente")]
    public async Task Municipio_ComNomeQueODominioRecusa_ContaComoGeoQueNaoRespondeu()
    {
        GeoResponde("1504208", Result<string>.Success(" "));

        Result<CadastrosCitadosNoDocumento> lidos = await Ler(SemCodigos with { Localidade = "1504208" });

        lidos.IsSuccess.Should().BeTrue();
        lidos.Value!.Localidades.Should().BeEmpty();
        lidos.Value.LocalidadeNaoConferida!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
    }

    [Theory(DisplayName = "O código IBGE sem a forma de município fica fora do dicionário sem consultar o Geo")]
    [InlineData("9904208")]
    [InlineData("15042")]
    [InlineData("15A4208")]
    public async Task Municipio_SemFormaDeMunicipio_NaoVaiAoGeo(string codigoIbge)
    {
        Result<CadastrosCitadosNoDocumento> lidos = await Ler(SemCodigos with { Localidade = codigoIbge });

        lidos.IsSuccess.Should().BeTrue();
        lidos.Value!.Localidades.Should().BeEmpty();
        lidos.Value.LocalidadeNaoConferida.Should().BeNull();
        await _geo.DidNotReceiveWithAnyArgs().ConferirMunicipioAsync(default!, default!, default);
    }

    [Fact(DisplayName = "O Geo fora do ar deixa a localidade fora do dicionário e não impede a leitura nem as outras partes")]
    public async Task Geo_Indisponivel_NaoImpedeALeitura()
    {
        GeoResponde("1504208", Result<string>.Failure(Erro(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel)));
        _unidades.ObterPorCodigoAsync("CEPS", Arg.Any<CancellationToken>()).Returns(Unidade("CEPS"));

        Result<CadastrosCitadosNoDocumento> lidos = await Ler(SemCodigos with { Localidade = "1504208", Unidades = ["CEPS"] });

        lidos.IsSuccess.Should().BeTrue();
        lidos.Value!.Localidades.Should().BeEmpty();
        lidos.Value.LocalidadeNaoConferida!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
        lidos.Value.Unidades.Should().ContainKey("CEPS");
    }

    [Fact(DisplayName = "Qualquer outro erro do Geo deixa a localidade fora do dicionário e guarda a causa: o que ele não confirmou nunca fica aprovado")]
    public async Task Geo_ComErroDesconhecido_DeixaALocalidadeForaComACausa()
    {
        GeoResponde("1504208", Result<string>.Failure(Erro("ConferenciaEnderecoGeo.ErroDeTeste")));

        Result<CadastrosCitadosNoDocumento> lidos = await Ler(SemCodigos with { Localidade = "1504208" });

        lidos.IsSuccess.Should().BeTrue();
        lidos.Value!.Localidades.Should().BeEmpty();
        lidos.Value.LocalidadeNaoConferida!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
    }
}
