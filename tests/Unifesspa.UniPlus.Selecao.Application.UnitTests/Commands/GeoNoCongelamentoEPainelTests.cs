namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class GeoNoCongelamentoEPainelTests
{
    [Fact]
    public async Task Publicar_GeoIndisponivel_RecusaSemGravar()
    {
        ProcessoSeletivo processo = ProcessoComMunicipio();
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo);
        GeoIndisponivel(servicos);
        (Result resposta, IEnumerable<object> eventos) = await servicos.PublicarAsync(processo.Id);
        resposta.Error!.Code.Should().Be(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel);
        processo.ProvaPresencial!.Municipios.Single().Nome.Should().BeNull();
        eventos.Should().BeEmpty();
        await servicos.NadaFoiGravadoAsync();
    }

    [Fact]
    public async Task Publicar_MunicipioDesconhecido_RecusaNoCampoDoItem()
    {
        ProcessoSeletivo processo = ProcessoComMunicipio();
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo);
        servicos.Geo.ConferirMunicipioAsync("1504208", "PA", Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.MunicipioNaoEncontrado, "Município não encontrado")));
        (Result resposta, IEnumerable<object> eventos) = await servicos.PublicarAsync(processo.Id);
        resposta.Errors.Should().Contain(e => e.Error.Code == ProvaPresencialErrorCodes.MunicipioNaoConferidoNoGeo && e.Field == "/provaPresencial/municipios/0/codigoIbge");
        eventos.Should().BeEmpty();
        await servicos.NadaFoiGravadoAsync();
    }

    [Fact]
    public async Task Publicar_GeoDisponivel_RecopiaNomeConferido()
    {
        ProcessoSeletivo processo = ProcessoComMunicipio();
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo);
        servicos.Geo.ConferirMunicipioAsync("1504208", "PA", Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Marabá"));
        await servicos.PublicarAsync(processo.Id);
        processo.ProvaPresencial!.Municipios.Single().Nome.Should().Be("Marabá");
        await servicos.Geo.Received(1).ConferirMunicipioAsync("1504208", "PA", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Painel_GeoIndisponivel_MarcaProvaPendente()
    {
        ProcessoSeletivo processo = ProcessoComMunicipio();
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo);
        GeoIndisponivel(servicos);
        servicos.Repositorio.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ConformidadeProcessoSeletivoDto? painel = await servicos.ConformidadeAsync(new ObterConformidadeProcessoSeletivoQuery(processo.Id));
        painel!.Itens.Should().ContainSingle(item => item.Codigo == "prova_presencial").Which.Ok.Should().BeFalse();
    }

    [Fact]
    public async Task Painel_MunicipioDesconhecido_MarcaProvaPendente()
    {
        ProcessoSeletivo processo = ProcessoComMunicipio();
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo);
        servicos.Geo.ConferirMunicipioAsync("1504208", "PA", Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.MunicipioNaoEncontrado, "Município não encontrado")));
        servicos.Repositorio.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ConformidadeProcessoSeletivoDto? painel = await servicos.ConformidadeAsync(new ObterConformidadeProcessoSeletivoQuery(processo.Id));
        painel!.Itens.Should().ContainSingle(item => item.Codigo == "prova_presencial").Which.Ok.Should().BeFalse();
    }

    [Fact]
    public async Task PainelEPublicacao_LimiteReduzido_RecusamSemConsultarGeo()
    {
        ProcessoSeletivo processo = ProcessoComMunicipio();
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo);
        processo.DefinirProvaPresencial(true, [new("1504208", "PA"), new("1508159", "PA")], 100).IsSuccess.Should().BeTrue();
        servicos.LimiteDeMunicipios = 1;
        servicos.Repositorio.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        ConformidadeProcessoSeletivoDto? painel = await servicos.ConformidadeAsync(new ObterConformidadeProcessoSeletivoQuery(processo.Id));
        (Result resposta, IEnumerable<object> eventos) = await servicos.PublicarAsync(processo.Id);
        painel!.Itens.Should().ContainSingle(item => item.Codigo == "prova_presencial").Which.Ok.Should().BeFalse();
        resposta.Errors.Should().Contain(erro => erro.Error.Code == ProvaPresencialErrorCodes.MunicipiosAcimaDoLimite);
        eventos.Should().BeEmpty();
        await servicos.Geo.DidNotReceiveWithAnyArgs().ConferirMunicipioAsync(default!, default!, default);
    }

    private static ProcessoSeletivo ProcessoComMunicipio()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("Processo de seleção");
        processo.DefinirProvaPresencial(true, [new("1504208", "PA")], 100).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static void GeoIndisponivel(ServicosDoCongelamentoDeTeste servicos) =>
        servicos.Geo.ConferirMunicipioAsync("1504208", "PA", Arg.Any<CancellationToken>())
            .Returns(Result<string>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel, "Geo indisponível")));
}
