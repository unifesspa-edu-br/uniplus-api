namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>A leitura da identificação e do cronograma, do JSON às declarações lidas pelo domínio: cada código distinto é lido uma vez, em qualquer fase que o cite.</summary>
public sealed class LeituraDaIdentificacaoEDoCronogramaTests
{
    private const string Documento = """
        {
          "identificacao": { "nome": "Processo de Teste", "tipoProcessoCodigo": "SISU", "unidadeAdministradoraCodigo": "CEPS", "origemCandidatos": "IMPORTACAO_EXTERNA", "localidadeCodigoIbge": "1504208" },
          "cronograma": { "fases": [
            { "chave": "inscricao", "tipoFaseCodigo": "INSCRICAO", "unidadeResponsavelCodigo": "CEPS" },
            { "chave": "recurso", "tipoFaseCodigo": "RECURSO", "unidadeResponsavelCodigo": "CEPS" },
            { "chave": "fase_recurso_externo", "tipoFaseCodigo": "RECURSO", "unidadeResponsavelCodigo": "CGA" }
          ] }
        }
        """;

    private readonly IUnidadeReader _unidades = Substitute.For<IUnidadeReader>();
    private readonly IConferenciaEnderecoGeo _geo = Substitute.For<IConferenciaEnderecoGeo>();
    private readonly ITipoProcessoReader _tiposDeProcesso = Substitute.For<ITipoProcessoReader>();
    private readonly IFaseCanonicaReader _fasesCanonicas = Substitute.For<IFaseCanonicaReader>();
    private readonly LeituraDaIdentificacaoEDoCronograma _leitura;

    public LeituraDaIdentificacaoEDoCronogramaTests()
    {
        ITipoAtoPublicadoReader tiposDeAto = Substitute.For<ITipoAtoPublicadoReader>();
        tiposDeAto.ListarVigentesAsync(default!, default, default).ReturnsForAnyArgs([]);
        _unidades.ListarPorCodigosAsync(default!, default).ReturnsForAnyArgs([Unidade("CEPS")]);
        _tiposDeProcesso.ObterAtivoPorCodigoAsync("SISU", Arg.Any<CancellationToken>()).Returns(new TipoProcessoView(Guid.CreateVersion7(), "SISU", "Sistema de Seleção", null));
        _fasesCanonicas.ListarVivasPorCodigosAsync(default!, default).ReturnsForAnyArgs([Fase("INSCRICAO"), Fase("RECURSO")]);
        _geo.ConferirMunicipioAsync("1504208", "PA", Arg.Any<CancellationToken>()).Returns(Result<string>.Success("Marabá"));
        _leitura = new(new(
            _unidades,
            _geo,
            new(Substitute.For<IRegraCatalogoReader>(), tiposDeAto, Substitute.For<IFatoCandidatoReader>()),
            new(_tiposDeProcesso, _fasesCanonicas, Substitute.For<ITipoBancaReader>(), Substitute.For<ICategoriaDocumentoReader>(), Substitute.For<ITipoEtapaReader>(), Substitute.For<IPesoAreaEnemReader>(), Substitute.For<ITipoDocumentoReader>()),
            new(
                Substitute.For<IOfertaCursoReader>(),
                Substitute.For<IModalidadeReader>(),
                Substitute.For<IReferenciaReservaDemograficaReader>(),
                Substitute.For<ICondicaoAtendimentoReader>(),
                Substitute.For<IRecursoAcessibilidadeReader>(),
                Substitute.For<ITipoDeficienciaReader>(),
                Substitute.For<IBaseLegalDoFadaReader>()),
            new(new RelogioFixo(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero)), new ResolvedorFusoDeTeste())));
    }

    private static UnidadeView Unidade(string codigo) =>
        new(Guid.CreateVersion7(), codigo, codigo, codigo.ToLowerInvariant(), $"Unidade {codigo}", null, "ADMINISTRATIVA", false, null);

    private static FaseCanonicaView Fase(string codigo) => new(Guid.CreateVersion7(), codigo, $"Fase {codigo}", null, false, false, null, false, false, false, "NO_EDITAL");

    private Task<Result<IdentificacaoECronogramaLidos>> Ler(string documento = Documento) =>
        _leitura.LerAsync(JsonNode.Parse(documento)!.AsObject(), CancellationToken.None);

    [Fact(DisplayName = "O documento lido entrega a identificação, a localidade e as fases adotadas, uma por fase do documento")]
    public async Task Documento_EntregaIdentificacaoLocalidadeEFases()
    {
        Result<IdentificacaoECronogramaLidos> lidos = await Ler();

        lidos.IsSuccess.Should().BeTrue();
        lidos.Value!.Identidade.Nome.Should().Be("Processo de Teste");
        lidos.Value.Identidade.TipoProcesso!.Codigo.Should().Be("SISU");
        lidos.Value.Identidade.UnidadeAdministradora!.Codigo.Should().Be("CEPS");
        lidos.Value.Cronograma.Localidade!.CodigoIbge.Should().Be("1504208");
        lidos.Value.Cronograma.CronogramaFases.Should().HaveCount(3);
    }

    [Fact(DisplayName = "A unidade que é administradora e responsável de fases, e a fase canônica citada por duas fases, são lidas uma vez")]
    public async Task CodigoCitadoEmVariasPartes_ELidoUmaVez()
    {
        await Ler();

        await _unidades.Received(1).ListarPorCodigosAsync(Arg.Is<IReadOnlyCollection<string>>(c => c.SequenceEqual(new[] { "CEPS", "CGA" })), Arg.Any<CancellationToken>());
        await _fasesCanonicas.Received(1).ListarVivasPorCodigosAsync(Arg.Is<IReadOnlyCollection<string>>(c => c.SequenceEqual(new[] { "INSCRICAO", "RECURSO" })), Arg.Any<CancellationToken>());
        await _tiposDeProcesso.Received(1).ObterAtivoPorCodigoAsync("SISU", Arg.Any<CancellationToken>());
        await _geo.Received(1).ConferirMunicipioAsync("1504208", "PA", Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A parte que o domínio recusa viaja na origem do resultado, no ponteiro do campo, sem falhar a leitura")]
    public async Task ParteRecusada_ViajaNaOrigem()
    {
        Result<IdentificacaoECronogramaLidos> lidos = await Ler();

        lidos.Value!.Origem.PartesRecusadas.Select(static parte => (parte.Causa.Field, parte.Causa.Error.Code)).Should().Equal(
            ("/cronograma/fases/2/unidadeResponsavelCodigo", FaseCronogramaErrorCodes.UnidadeResponsavelNaoEncontrada));
    }

    [Fact(DisplayName = "O documento sem identificação nem cronograma é lido como declaração vazia, sem consultar cadastro")]
    public async Task DocumentoVazio_NaoConsultaCadastro()
    {
        Result<IdentificacaoECronogramaLidos> lidos = await Ler("{}");

        lidos.IsSuccess.Should().BeTrue();
        lidos.Value!.Cronograma.CronogramaFases.Should().BeEmpty();
        await _unidades.DidNotReceiveWithAnyArgs().ListarPorCodigosAsync(default!, default);
        await _geo.DidNotReceiveWithAnyArgs().ConferirMunicipioAsync(default!, default!, default);
    }

    [Fact(DisplayName = "O Geo fora do ar não impede a leitura: a localidade fica sem conferir, e o restante do documento é lido")]
    public async Task GeoIndisponivel_NaoImpedeALeitura()
    {
        GeoResponde(Result<string>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel, "Geo indisponível (dublê de teste).")));

        Result<IdentificacaoECronogramaLidos> lidos = await Ler();

        lidos.IsSuccess.Should().BeTrue();
        lidos.Value!.Cronograma.Localidade.Should().BeNull();
        lidos.Value.Cronograma.CronogramaFases.Should().NotBeEmpty();
    }

    [Fact(DisplayName = "O Geo fora do ar aponta uma só causa no campo da localidade: a falta de conferência, nunca município inexistente")]
    public async Task GeoIndisponivel_ApontaAFaltaDeConferenciaEUmaVezSo()
    {
        GeoResponde(Result<string>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel, "Geo indisponível (dublê de teste).")));

        Result<IdentificacaoECronogramaLidos> lidos = await Ler();

        CausasDaLocalidade(lidos.Value!).Should().Equal(("/identificacao/localidadeCodigoIbge", ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel));
    }

    [Fact(DisplayName = "O município que o Geo diz não existir aponta a causa de município inexistente no campo da localidade")]
    public async Task MunicipioInexistente_ApontaMunicipioNaoEncontrado()
    {
        GeoResponde(Result<string>.Failure(new DomainError(ConferenciaEnderecoGeoErrorCodes.MunicipioNaoEncontrado, "Município inexistente (dublê de teste).")));

        Result<IdentificacaoECronogramaLidos> lidos = await Ler();

        CausasDaLocalidade(lidos.Value!).Should().Equal(("/identificacao/localidadeCodigoIbge", MunicipioErrorCodes.NaoEncontrado));
    }

    [Fact(DisplayName = "O município que o Geo confirma não deixa causa no campo da localidade")]
    public async Task MunicipioConfirmado_NaoApontaCausa()
    {
        Result<IdentificacaoECronogramaLidos> lidos = await Ler();

        CausasDaLocalidade(lidos.Value!).Should().BeEmpty();
    }

    private void GeoResponde(Result<string> resposta) =>
        _geo.ConferirMunicipioAsync("1504208", "PA", Arg.Any<CancellationToken>()).Returns(resposta);

    private static IEnumerable<(string? Campo, string Codigo)> CausasDaLocalidade(IdentificacaoECronogramaLidos lidos) =>
        lidos.Origem.PartesRecusadas
            .Where(static parte => parte.Causa.Field == "/identificacao/localidadeCodigoIbge")
            .Select(static parte => (parte.Causa.Field, parte.Causa.Error.Code));
}
