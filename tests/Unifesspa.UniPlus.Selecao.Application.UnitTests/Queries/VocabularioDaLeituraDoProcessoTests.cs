namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;
using System.Text.Json.Serialization;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A leitura do processo devolve, para cada atributo de domínio fechado, o mesmo token que
/// a fonte daquele atributo já usa — a escrita, quando o campo é escrito
/// aqui; o catálogo de Configuração, quando é snapshot-copy (ADR-0061).
/// </summary>
/// <remarks>
/// <para>
/// As asserções são sobre o <b>JSON serializado</b>, não sobre as propriedades do DTO. É a
/// única forma de provar o wire: <c>JsonStringEnumConverter</c> desserializa
/// <c>"InscricaoPropria"</c> e <c>"INSCRICAO_PROPRIA"</c> indistintamente, então um teste
/// que lesse o DTO de volta passaria com as duas grafias — foi assim que a assimetria
/// atravessou a suíte até chegar à tela, onde nenhuma <c>option</c> casava com o valor
/// recebido e o campo aparecia vazio num processo que tinha a origem declarada.
/// </para>
/// <para>
/// As opções de serialização replicam as do host (<c>Program.cs</c>): mesma política de
/// nomes e mesmo conversor de enum. Um teste com opções próprias provaria o serializador
/// do teste, não o contrato publicado.
/// </para>
/// </remarks>
public sealed class VocabularioDaLeituraDoProcessoTests
{
    private static readonly JsonSerializerOptions OpcoesDoHost = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Fact(DisplayName = "A raiz devolve situacao e origemCandidatos no vocabulário que a criação aceita")]
    public async Task Raiz_DevolveTokensDaEscrita()
    {
        ProcessoSeletivo processo = NovoProcesso();

        JsonElement json = await ProjetarERSerializarAsync(processo);

        json.GetProperty("origemCandidatos").GetProperty("codigo").GetString().Should().Be("INSCRICAO_PROPRIA",
            "é o token que `CriarProcessoSeletivoCommand.origemCandidatos` aceita — quem grava e "
            + "relê o mesmo recurso tem de receber de volta o que enviou");
        json.GetProperty("situacao").GetProperty("codigo").GetString().Should().Be("RASCUNHO");
    }

    [Fact(DisplayName = "A etapa devolve carater no vocabulário que a definição de etapas aceita")]
    public async Task Etapa_DevolveTokenDaEscrita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Result definida = processo.DefinirEtapas(
            [EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
                new IdentificacaoDaEtapa(Nome: "Prova objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: null),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!]);
        definida.IsSuccess.Should().BeTrue(definida.Error?.Message);

        JsonElement json = await ProjetarERSerializarAsync(processo);

        json.GetProperty("etapas")[0].GetProperty("identificacao").GetProperty("carater").GetProperty("codigo").GetString().Should().Be("CLASSIFICATORIA",
            "`EtapaProcessoInput.carater` aceita este token — reenviar o GET direto no PUT não pode "
            + "exigir que o cliente traduza a grafia no caminho");
    }

    /// <summary>
    /// Os três atributos da modalidade são snapshot-copy do catálogo, e não têm superfície
    /// de escrita aqui: quem os declara é <c>GET /api/configuracao/modalidades</c>. A prova
    /// passa pela simulação porque ela compartilha a projeção com o GET e com o PUT — as
    /// três superfícies emitem o mesmo shape, por construção.
    /// </summary>
    [Fact(DisplayName = "A modalidade devolve os tokens do catálogo, e ausência de remanejamento como null")]
    public async Task Modalidade_DevolveTokensDoCatalogo()
    {
        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> simulada = await SimularModalidadeAsync();
        simulada.IsSuccess.Should().BeTrue(simulada.Error?.Message);

        JsonElement modalidade = Serializar(simulada.Value!)[0].GetProperty("modalidades")[0];

        modalidade.GetProperty("naturezaLegal").GetProperty("codigo").GetString().Should().Be("AMPLA",
            "o catálogo descreve esta mesma modalidade com este token, e o cliente cruza as duas rotas");
        modalidade.GetProperty("composicaoVagas").GetProperty("codigo").GetString().Should().Be("RESTANTE_DAS_VAGAS_OFERTADAS");
        modalidade.GetProperty("regraRemanejamento").ValueKind.Should().Be(JsonValueKind.Null,
            "a origem representa 'não remaneja' por ausência do campo, não por um token próprio");
    }

    private static async Task<Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>>> SimularModalidadeAsync()
    {
        Guid processoId = Guid.CreateVersion7();
        Guid ofertaId = Guid.CreateVersion7();
        Guid modalidadeId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ExisteAsync(processoId, Arg.Any<CancellationToken>()).Returns(true);
        ConfiguracaoDistribuicaoVagasResolver resolver = CriarResolver(ofertaId, modalidadeId);
        ConfiguracaoDistribuicaoVagasInput input = new(ofertaId, 60, 1m,
            RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", null, null, null,
            [modalidadeId], [new QuantidadeVagaInput(modalidadeId, 60)]);
        return await SimularDistribuicaoVagasQueryHandler.Handle(
            new SimularDistribuicaoVagasQuery(processoId, [input]), repository, resolver, CancellationToken.None);
    }

    private static ConfiguracaoDistribuicaoVagasResolver CriarResolver(Guid ofertaId, Guid modalidadeId)
    {
        IRegraCatalogoReader regras = Substitute.For<IRegraCatalogoReader>();
        regras.ObterAsync(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCatalogo.Criar(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1",
                TipoRegra.RegraDistribuicaoVagas, Json("{}"), Json("[]"), "base legal"));
        IOfertaCursoReader ofertas = Substitute.For<IOfertaCursoReader>();
        ofertas.ObterPorIdAsync(ofertaId, Arg.Any<CancellationToken>()).Returns(new OfertaCursoView(
            ofertaId, "MEDICINA_MARABA", Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            "CTIC", "Centro de Tecnologia", "CAMPUS", "REGULAR", "PRESENCIAL", "EXTENSIVO",
            "REGULAR", ["MATUTINO"], null, null, 100, null, null, null, "Medicina", "Bacharelado"));
        return new ConfiguracaoDistribuicaoVagasResolver(regras, ofertas, CriarModalidades(modalidadeId),
            Substitute.For<IReferenciaReservaDemograficaReader>());
    }

    private static IModalidadeReader CriarModalidades(Guid modalidadeId)
    {
        IModalidadeReader modalidades = Substitute.For<IModalidadeReader>();
        modalidades.ObterPorIdAsync(modalidadeId, Arg.Any<CancellationToken>()).Returns(new ModalidadeView(
            modalidadeId, "AC", "Ampla concorrência", "AMPLA", "RESTANTE_DAS_VAGAS_OFERTADAS",
            null, null, null, null, null, PredicadoDnf.CriarDeCondicoesAgrupadas([]).Value!, null, "Lei 12.711/2012"));
        return modalidades;
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PS vocabulário",
        TiposDeProcessoDeTeste.SiSU,
        OrigemCandidatos.InscricaoPropria,
        Guid.CreateVersion7(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static async Task<JsonElement> ProjetarERSerializarAsync(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterComConfiguracaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);

        ProcessoSeletivoDto? dto = await ObterProcessoSeletivoQueryHandler.Handle(
            new ObterProcessoSeletivoQuery(processo.Id), repository, CancellationToken.None);
        dto.Should().NotBeNull();

        return Serializar(dto!);
    }

    private static JsonElement Serializar<T>(T valor)
    {
        using JsonDocument documento = JsonDocument.Parse(JsonSerializer.Serialize(valor, OpcoesDoHost));
        return documento.RootElement.Clone();
    }

    private static JsonElement Json(string raw)
    {
        using JsonDocument documento = JsonDocument.Parse(raw);
        return documento.RootElement.Clone();
    }
}
