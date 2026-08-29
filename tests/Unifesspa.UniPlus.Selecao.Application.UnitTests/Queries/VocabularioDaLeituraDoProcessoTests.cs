namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;
using System.Text.Json.Serialization;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A leitura do processo devolve, para cada atributo de domínio fechado, o mesmo token que
/// a fonte daquele atributo já usa (issue #1294) — a escrita, quando o campo é escrito
/// aqui; o catálogo de Configuração, quando é snapshot-copy (ADR-0061).
/// </summary>
/// <remarks>
/// <para>
/// As asserções são sobre o <b>JSON serializado</b>, não sobre as propriedades do DTO. É a
/// única forma de provar o wire: <c>JsonStringEnumConverter</c> desserializa
/// <c>"InscricaoPropria"</c> e <c>"inscricaoPropria"</c> indistintamente, então um teste
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

    [Fact(DisplayName = "A raiz devolve status e origemCandidatos no vocabulário que a criação aceita")]
    public async Task Raiz_DevolveTokensDaEscrita()
    {
        ProcessoSeletivo processo = NovoProcesso();

        JsonElement json = await ProjetarERSerializarAsync(processo);

        json.GetProperty("origemCandidatos").GetString().Should().Be("inscricaoPropria",
            "é o token que `CriarProcessoSeletivoCommand.origemCandidatos` aceita — quem grava e "
            + "relê o mesmo recurso tem de receber de volta o que enviou");
        json.GetProperty("status").GetString().Should().Be("rascunho");
    }

    [Fact(DisplayName = "A etapa devolve carater no vocabulário que a definição de etapas aceita")]
    public async Task Etapa_DevolveTokenDaEscrita()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Result definida = processo.DefinirEtapas(
            [EtapaProcesso.Criar(
                "Prova objetiva",
                CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova objetiva").Value!,
                peso: 1m,
                ordem: 1).Value!],
            PrecondicaoIfMatch.Curinga);
        definida.IsSuccess.Should().BeTrue(definida.Error?.Message);

        JsonElement json = await ProjetarERSerializarAsync(processo);

        json.GetProperty("etapas")[0].GetProperty("carater").GetString().Should().Be("classificatoria",
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
        Guid processoId = Guid.CreateVersion7();
        Guid ofertaCursoId = Guid.CreateVersion7();
        Guid modalidadeId = Guid.CreateVersion7();

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ExisteAsync(processoId, Arg.Any<CancellationToken>()).Returns(true);
        IRegraCatalogoReader regras = Substitute.For<IRegraCatalogoReader>();
        regras.ObterAsync(RegraDistribuicaoVagasCodigo.Institucional, "v1", Arg.Any<CancellationToken>())
            .Returns(RegraCatalogo.Criar(
                RegraDistribuicaoVagasCodigo.Institucional, "v1", TipoRegra.RegraDistribuicaoVagas,
                Json("{}"), Json("[]"), "base legal").Value!);
        IOfertaCursoReader ofertas = Substitute.For<IOfertaCursoReader>();
        ofertas.ObterPorIdAsync(ofertaCursoId, Arg.Any<CancellationToken>()).Returns(new OfertaCursoView(
            ofertaCursoId, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            "CTIC", "Centro de Tecnologia", "CAMPUS", "REGULAR", "PRESENCIAL", "EXTENSIVO",
            "REGULAR", ["MATUTINO"], null, null, 50, null, null));
        IModalidadeReader modalidades = Substitute.For<IModalidadeReader>();

        // Ampla concorrência: o catálogo publica `AMPLA`/`RESIDUAL_DO_VO` e NÃO publica regra
        // de remanejamento — a modalidade que não remaneja é representada por ausência.
        modalidades.ObterPorIdAsync(modalidadeId, Arg.Any<CancellationToken>()).Returns(new ModalidadeView(
            modalidadeId, "AC", "Ampla concorrência", "AMPLA", "RESIDUAL_DO_VO",
            null, null, null, null, null, [], null, "Lei 12.711/2012"));

        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagasDto>> simulada = await SimularDistribuicaoVagasQueryHandler.Handle(
            new SimularDistribuicaoVagasQuery(
                processoId,
                [new ConfiguracaoDistribuicaoVagasInput(
                    ofertaCursoId, 60, 1m, RegraDistribuicaoVagasCodigo.Institucional, "v1", null, null, null,
                    [modalidadeId], [new QuantidadeVagaInput(modalidadeId, 60)])]),
            repository, regras, ofertas, modalidades,
            Substitute.For<IReferenciaReservaDemograficaReader>(), CancellationToken.None);
        simulada.IsSuccess.Should().BeTrue(simulada.Error?.Message);

        JsonElement modalidade = Serializar(simulada.Value!)[0].GetProperty("modalidades")[0];

        modalidade.GetProperty("naturezaLegal").GetString().Should().Be("AMPLA",
            "o catálogo descreve esta mesma modalidade com este token, e o cliente cruza as duas rotas");
        modalidade.GetProperty("composicaoVagas").GetString().Should().Be("RESIDUAL_DO_VO");
        modalidade.GetProperty("regraRemanejamento").ValueKind.Should().Be(JsonValueKind.Null,
            "a origem representa 'não remaneja' por ausência do campo, não por um token próprio");
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PS vocabulário",
        TipoProcesso.SiSU,
        OrigemCandidatos.InscricaoPropria,
        Guid.CreateVersion7(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

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
