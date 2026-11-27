namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

public sealed class ConferenciaDosMunicipiosNoGeoTests
{
    private readonly IConferenciaEnderecoGeo _geo = Substitute.For<IConferenciaEnderecoGeo>();

    [Fact]
    public async Task ConferirAsync_SemMunicipios_NaoConsultaGeo()
    {
        ConferenciaDosMunicipiosNoGeo conferencia = Criar();
        (await conferencia.ConferirAsync((DeclaracaoDaProvaPresencial?)null, CancellationToken.None)).Value.Should().Be(MunicipiosConferidosNoGeo.Nenhum);
        (await conferencia.ConferirAsync(Declaracao(false), CancellationToken.None)).Value.Should().Be(MunicipiosConferidosNoGeo.Nenhum);
        (await conferencia.ConferirAsync(Declaracao(true), CancellationToken.None)).Value.Should().Be(MunicipiosConferidosNoGeo.Nenhum);
        await _geo.DidNotReceiveWithAnyArgs().ConferirMunicipioAsync(default!, default!, default);
    }

    [Fact]
    public async Task ConferirAsync_MunicipiosDeclarados_ConsultaEmOrdemEGuardaNomes()
    {
        _geo.ConferirMunicipioAsync(Arg.Any<string>(), "PA", Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Nome conferido"));
        Result<MunicipiosConferidosNoGeo> resultado = await Criar().ConferirAsync(Declaracao(true, "1504208", "1508159"), CancellationToken.None);
        resultado.Value!.NomeConferido("1504208").Should().Be("Nome conferido");
        resultado.Value.NomeConferido("1508159").Should().Be("Nome conferido");
        Received.InOrder(() =>
        {
            _ = _geo.ConferirMunicipioAsync("1504208", "PA", CancellationToken.None);
            _ = _geo.ConferirMunicipioAsync("1508159", "PA", CancellationToken.None);
        });
    }

    [Theory]
    [InlineData(ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente)]
    public async Task ConferirAsync_MunicipioRecusado_FicaNaoConferido(string codigo)
    {
        DomainError erro = new(codigo, "Município recusado");
        _geo.ConferirMunicipioAsync("1504208", "PA", Arg.Any<CancellationToken>()).Returns(Result<string>.Failure(erro));
        Result<MunicipiosConferidosNoGeo> resultado = await Criar().ConferirAsync(Declaracao(true, "1504208"), CancellationToken.None);
        ConferenciaDaProvaPresencial.Conferir(Declaracao(true, "1504208"), resultado.Value!, 100)
            .Should().ContainSingle().Which.Error.Code.Should().Be(ProvaPresencialErrorCodes.MunicipioNaoConferidoNoGeo);
    }

    [Fact]
    public async Task ConferirAsync_MunicipioDesconhecido_FicaNaoConferido()
    {
        DomainError erro = new(ConferenciaEnderecoGeoErrorCodes.MunicipioNaoEncontrado, "Município não encontrado");
        _geo.ConferirMunicipioAsync("1504208", "PA", Arg.Any<CancellationToken>()).Returns(Result<string>.Failure(erro));
        Result<MunicipiosConferidosNoGeo> resultado = await Criar().ConferirAsync(Declaracao(true, "1504208"), CancellationToken.None);
        resultado.Value!.Resultado("1504208").Should().Be(ResultadoDoMunicipioNoGeo.NaoConferido);
    }

    [Fact]
    public async Task ConferirAsync_GeoIndisponivel_InterrompeComErro()
    {
        DomainError erro = new(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel, "Geo indisponível");
        _geo.ConferirMunicipioAsync("1504208", "PA", Arg.Any<CancellationToken>()).Returns(Result<string>.Failure(erro));
        Result<MunicipiosConferidosNoGeo> resultado = await Criar().ConferirAsync(Declaracao(true, "1504208", "1508159"), CancellationToken.None);
        resultado.Error.Should().Be(erro);
        await _geo.DidNotReceive().ConferirMunicipioAsync("1508159", "PA", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConferirAsync_AcimaDoLimite_ConsultaGeoEAcumulaARecusaDoLimite()
    {
        DomainError erro = new(ConferenciaEnderecoGeoErrorCodes.MunicipioNaoEncontrado, "Município não encontrado");
        _geo.ConferirMunicipioAsync("1504208", "PA", Arg.Any<CancellationToken>()).Returns(Result<string>.Failure(erro));
        _geo.ConferirMunicipioAsync("1508159", "PA", Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Xinguara"));
        DeclaracaoDaProvaPresencial declaracao = Declaracao(true, "1504208", "1508159");
        Result<MunicipiosConferidosNoGeo> resultado = await Criar().ConferirAsync(declaracao, CancellationToken.None);
        ConferenciaDaProvaPresencial.Conferir(declaracao, resultado.Value!, 1).Select(static recusa => recusa.Error.Code)
            .Should().Equal(ProvaPresencialErrorCodes.MunicipioNaoConferidoNoGeo, ProvaPresencialErrorCodes.MunicipiosAcimaDoLimite);
    }

    [Fact]
    public async Task ConferirAsync_ListaDeMunicipios_ConsultaEmOrdemSemOlharADeclaracaoNemOLimite()
    {
        _geo.ConferirMunicipioAsync("1504208", "PA", Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Marabá"));
        _geo.ConferirMunicipioAsync("1508159", "PA", Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Xinguara"));
        MunicipioDeProva[] municipios = [new("1504208", "PA", null, 0), new("1508159", "PA", null, 1)];
        Result<MunicipiosConferidosNoGeo> resultado = await Criar().ConferirAsync(municipios, CancellationToken.None);
        resultado.Value!.NomeConferido("1504208").Should().Be("Marabá");
        resultado.Value.NomeConferido("1508159").Should().Be("Xinguara");
        Received.InOrder(() =>
        {
            _ = _geo.ConferirMunicipioAsync("1504208", "PA", CancellationToken.None);
            _ = _geo.ConferirMunicipioAsync("1508159", "PA", CancellationToken.None);
        });
    }

    [Fact]
    public async Task ConferirAsync_ListaDeMunicipiosComGeoIndisponivel_InterrompeComErro()
    {
        DomainError erro = new(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel, "Geo indisponível");
        _geo.ConferirMunicipioAsync("1504208", "PA", Arg.Any<CancellationToken>()).Returns(Result<string>.Failure(erro));
        MunicipioDeProva[] municipios = [new("1504208", "PA", null, 0), new("1508159", "PA", null, 1)];
        Result<MunicipiosConferidosNoGeo> resultado = await Criar().ConferirAsync(municipios, CancellationToken.None);
        resultado.Error.Should().Be(erro);
        await _geo.DidNotReceive().ConferirMunicipioAsync("1508159", "PA", Arg.Any<CancellationToken>());
    }

    private ConferenciaDosMunicipiosNoGeo Criar() => new(_geo);

    private static DeclaracaoDaProvaPresencial Declaracao(bool temProva, params string[] codigos) =>
        DeclaracaoDaProvaPresencial.Declarar(temProva, [.. codigos.Select(codigo => new MunicipioDeProvaInformado(codigo, "PA"))], 100).Value!;
}
