namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Text.Json.Nodes;

using Application.Commands.ProcessosSeletivos;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;

using Kernel.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Wolverine;

/// <summary>
/// Cobertura de concorrência: dois handlers
/// que carregam o MESMO processo via <c>ObterComConfiguracaoAsync</c> — um
/// <c>DefinirEtapas</c> e um <c>Publicar</c> — não podem produzir um estado
/// em que o processo está Publicado mas o snapshot congelado diverge da
/// configuração viva. O lock pessimista (<c>SELECT ... FOR UPDATE</c> em
/// <c>ProcessoSeletivoRepository.ObterComConfiguracaoAsync</c>) serializa os
/// dois handlers — a ordem de conclusão não é determinística, mas o
/// resultado final sempre é consistente numa das duas formas válidas.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class PublicacaoConcorrenciaTests
{
    private readonly CascadingFixture _fixture;

    public PublicacaoConcorrenciaTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Definir etapas junto da publicação mantém o processo e o documento congelado coerentes")]
    public async Task DefinirEtapasConcorrenteComPublicar_NuncaDeixaEstadoInconsistente()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient cliente = api.CreateClient();
        (Guid processoId, Guid documentoId) = await SemearProcessoPublicavelAsync();

        (Result definicao, Result publicacao) = await DefinirEPublicarAsync(EtapasRevisadas(processoId), Publicacao(processoId, documentoId));

        await ConferirEtapasAsync(processoId, definicao, publicacao);
    }

    [Fact(DisplayName = "Definir a cascata junto da publicação nunca publica sem a cascata exigida pela oferta")]
    public async Task DefinirCascataRemanejamentoConcorrenteComPublicar_NuncaPublicaSnapshotSemCascata()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient cliente = api.CreateClient();
        (Guid processoId, Guid documentoId) = await SemearProcessoFederalAsync();
        DefinirCascataRemanejamentoCommand cascata = new(processoId, "REMANEJAMENTO_ORDEM_LEI_12711", "v1", "AC", MatrizLegalCompletaComoInput());

        (Result definicao, Result publicacao) = await DefinirEPublicarAsync(cascata, Publicacao(processoId, documentoId));

        await ConferirCascataAsync(processoId, definicao, publicacao);
    }

    private static DefinirEtapasCommand EtapasRevisadas(Guid processoId) => new(
        processoId,
        [new EtapaProcessoInput(
            "Prova Discursiva (revisada)", CaraterEtapa.Classificatoria.Codigo(),
            TipoEtapaOrigemId: new Guid("019fee1e-7000-7000-8000-000000000001"),
            Peso: 1m, NotaMinima: null, Ordem: 1, Produtos: [], Bancas: [], Recursos: [],
            NotaMaxima: 10m, FormaDeAvaliacao: "OBJETIVA", FaseCodigo: "RESULTADO_FINAL")]);

    private static PublicarProcessoSeletivoCommand Publicacao(Guid processoId, Guid documentoId) =>
        new(processoId, Numero: null, PeriodoInscricaoInicio: null, PeriodoInscricaoFim: null,
            DocumentoEditalId: documentoId, Ato: DadosDoAtoDeTeste.Padrao);

    private async Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoPublicavelAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
            db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"Concorrência {Guid.CreateVersion7()}" });
        return (processo.Id, documento.Id);
    }

    private async Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoFederalAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await SemearProcessoFederalSemCascataAsync(db, $"Concorrência Cascata {Guid.CreateVersion7()}");
    }

    private async Task<(Result Definicao, Result Publicacao)> DefinirEPublicarAsync<TDefinicao>(
        TDefinicao definicao, PublicarProcessoSeletivoCommand publicacao)
    {
        await using AsyncServiceScope definirEscopo = _fixture.Factory.Services.CreateAsyncScope();
        await using AsyncServiceScope publicarEscopo = _fixture.Factory.Services.CreateAsyncScope();
        IMessageBus definirBus = definirEscopo.ServiceProvider.GetRequiredService<IMessageBus>();
        IMessageBus publicarBus = publicarEscopo.ServiceProvider.GetRequiredService<IMessageBus>();
        Task<Result> definir = definirBus.InvokeAsync<Result>(definicao!);
        Task<Result> publicar = publicarBus.InvokeAsync<Result>(publicacao);
        await Task.WhenAll(definir, publicar);
        return (await definir, await publicar);
    }

    private async Task ConferirEtapasAsync(Guid processoId, Result definicao, Result publicacao)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.AsNoTracking().Include(p => p.Etapas).FirstAsync(p => p.Id == processoId);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        processo.Status.Should().Be(StatusProcesso.Publicado);
        if (definicao.IsFailure)
        {
            definicao.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
            processo.Etapas.Should().ContainSingle(etapa => etapa.Nome == "Prova Objetiva");
            return;
        }
        await ConferirEtapasRevisadasAsync(db, processo);
    }

    private static async Task ConferirEtapasRevisadasAsync(UniPlusDbContext db, ProcessoSeletivo processo)
    {
        processo.Etapas.Should().ContainSingle(etapa => etapa.Nome == "Prova Discursiva (revisada)");
        JsonNode configuracao = await ConfiguracaoPublicadaAsync(db, processo.Id);
        JsonArray etapas = configuracao["etapas"]!.AsArray();
        etapas.Should().ContainSingle();
        etapas[0]!["nome"]!.GetValue<string>().Should().Be("Prova Discursiva (revisada)");
    }

    private async Task ConferirCascataAsync(Guid processoId, Result definicao, Result publicacao)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.AsNoTracking().Include(p => p.Cascata!).ThenInclude(c => c.Destinos).FirstAsync(p => p.Id == processoId);
        definicao.IsSuccess.Should().BeTrue(definicao.Error?.Message);
        processo.Cascata.Should().NotBeNull();
        processo.Cascata!.Destinos.Should().HaveCount(56);
        if (publicacao.IsFailure)
        {
            await ConferirPublicacaoSemCascataRecusadaAsync(db, processo, publicacao);
            return;
        }
        await ConferirCascataPublicadaAsync(db, processo);
    }

    private static async Task ConferirPublicacaoSemCascataRecusadaAsync(UniPlusDbContext db, ProcessoSeletivo processo, Result publicacao)
    {
        publicacao.Error!.Code.Should().Be("ProcessoSeletivo.CascataOrigemAusente");
        processo.Status.Should().Be(StatusProcesso.Rascunho);
        bool existeVersao = await db.VersoesConfiguracao.AsNoTracking().AnyAsync(versao => versao.ProcessoSeletivoId == processo.Id);
        existeVersao.Should().BeFalse("a publicação recusada não deixa uma versão congelada");
    }

    private static async Task ConferirCascataPublicadaAsync(UniPlusDbContext db, ProcessoSeletivo processo)
    {
        processo.Status.Should().Be(StatusProcesso.Publicado);
        JsonNode configuracao = await ConfiguracaoPublicadaAsync(db, processo.Id);
        JsonObject cascata = configuracao["cascataRemanejamento"]!.AsObject();
        cascata["presente"]!.GetValue<bool>().Should().BeTrue();
        cascata["ordens"]!.AsArray().Should().HaveCount(8);
    }

    private static async Task<JsonNode> ConfiguracaoPublicadaAsync(UniPlusDbContext db, Guid processoId)
    {
        VersaoConfiguracao versao = await db.VersoesConfiguracao.AsNoTracking().SingleAsync(versao => versao.ProcessoSeletivoId == processoId);
        return JsonNode.Parse(versao.ConfiguracaoCongelada)!;
    }

    private static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoFederalSemCascataAsync(
        UniPlusDbContext db, string nome)
    {
        ProcessoSeletivo processo = Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos
            .ProcessoSeletivoPublicacaoSeeder.NovoProcessoComOfertaFederalECascata(nome, comCascata: false);
        await db.ProcessosSeletivos.AddAsync(processo);
        await MotivosDaHomologacaoSemeados.EscolherAsync(db, processo);

        string hashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, hashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        await db.DocumentosAPublicar.AddAsync(documento);

        await db.SaveChangesAsync();
        processo.DequeueDomainEvents();

        return (processo.Id, documento.Id);
    }

    /// <summary>
    /// A matriz legal completa (8×7), fallback AC, como <c>DestinoRemanejamentoInput</c> —
    /// mesma forma exigida pela declaração da regra de <c>REMANEJAMENTO_ORDEM_LEI_12711
    /// v1</c> (cascata de remanejamento de cotas, UNI-REQ-0056, validada pelo handler): cada origem lista as outras 7 federais,
    /// na ordem de <see cref="ModalidadesFederaisLei12711.Codigos"/>.
    /// </summary>
    private static List<DestinoRemanejamentoInput> MatrizLegalCompletaComoInput()
    {
        IReadOnlyList<string> origens = ModalidadesFederaisLei12711.Codigos;
        List<DestinoRemanejamentoInput> destinos = [];
        foreach (string origem in origens)
        {
            string[] destinosDaOrigem = [.. origens.Where(o => o != origem)];
            for (int i = 0; i < destinosDaOrigem.Length; i++)
            {
                destinos.Add(new DestinoRemanejamentoInput(origem, i + 1, destinosDaOrigem[i]));
            }
        }

        return destinos;
    }
}
