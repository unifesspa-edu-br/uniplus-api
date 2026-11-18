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
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Wolverine;

internal static class ApoioDaConcorrenciaDaPublicacao
{
    internal static async Task<(Guid ProcessoId, Guid DocumentoId)> PrepararEtapasAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"Concorrência {Guid.CreateVersion7()}" });
        return (processo.Id, documento.Id);
    }

    internal static async Task<(Guid ProcessoId, Guid DocumentoId)> PrepararCascataAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await SemearProcessoFederalSemCascataAsync(db, $"Concorrência Cascata {Guid.CreateVersion7()}");
    }

    internal static DefinirEtapasCommand ComandoEtapas(Guid processoId) => new(
        processoId, [new EtapaProcessoInput(
            "Prova Discursiva (revisada)", CaraterEtapa.Classificatoria,
            TipoEtapaOrigemId: new Guid("019fee1e-7000-7000-8000-000000000001"),
            Peso: 1m, NotaMinima: null, Ordem: 1, Produtos: [], Bancas: [], Recursos: [],
            NotaMaxima: 10m, FormaDeAvaliacao: "OBJETIVA", FaseCodigo: "RESULTADO_FINAL")]);

    internal static DefinirCascataRemanejamentoCommand ComandoCascata(Guid processoId) => new(
        processoId, RegraCodigo: "REMANEJ-CASCATA-LEI-12711", RegraVersao: "v1",
        FallbackCodigo: "AC", Destinos: MatrizLegalCompletaComoInput());

    internal static PublicarProcessoSeletivoCommand ComandoPublicar(Guid processoId, Guid documentoId) => new(
        processoId, Numero: null, PeriodoInscricaoInicio: null, PeriodoInscricaoFim: null,
        DocumentoEditalId: documentoId, Ato: DadosDoAtoDeTeste.Padrao);

    internal static async Task<(Result Definir, Result Publicar)> ExecutarAsync(
        CascadingApiFactory api, object definir, PublicarProcessoSeletivoCommand publicar)
    {
        await using AsyncServiceScope definirScope = api.Services.CreateAsyncScope();
        await using AsyncServiceScope publicarScope = api.Services.CreateAsyncScope();
        IMessageBus definirBus = definirScope.ServiceProvider.GetRequiredService<IMessageBus>();
        IMessageBus publicarBus = publicarScope.ServiceProvider.GetRequiredService<IMessageBus>();
        Task<Result> definirTask = definirBus.InvokeAsync<Result>(definir);
        Task<Result> publicarTask = publicarBus.InvokeAsync<Result>(publicar);
        await Task.WhenAll(definirTask, publicarTask);
        return (await definirTask, await publicarTask);
    }

    internal static async Task ConferirEtapasAsync(CascadingApiFactory api, Guid id, Result definir, Result publicar)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.AsNoTracking().Include(p => p.Etapas).FirstAsync(p => p.Id == id);
        if (ConferirResultadoDasEtapas(processo, definir, publicar))
        {
            await ConferirEtapasCongeladasAsync(db, id);
        }
    }

    private static bool ConferirResultadoDasEtapas(ProcessoSeletivo processo, Result definir, Result publicar)
    {
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
        processo.Status.Should().Be(StatusProcesso.Publicado);
        if (definir.IsFailure)
        {
            definir.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
            processo.Etapas.Should().ContainSingle(e => e.Nome == "Prova Objetiva");
            return false;
        }
        processo.Etapas.Should().ContainSingle(e => e.Nome == "Prova Discursiva (revisada)");
        return true;
    }

    private static async Task ConferirEtapasCongeladasAsync(UniPlusDbContext db, Guid id)
    {
        VersaoConfiguracao versao = await db.VersoesConfiguracao.AsNoTracking().SingleAsync(v => v.ProcessoSeletivoId == id);
        JsonArray etapas = JsonNode.Parse(versao.ConfiguracaoCongelada)!["etapas"]!.AsArray();
        etapas.Should().ContainSingle();
        etapas[0]!["nome"]!.GetValue<string>().Should().Be("Prova Discursiva (revisada)");
    }

    internal static async Task ConferirCascataAsync(CascadingApiFactory api, Guid id, Result definir, Result publicar)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.AsNoTracking()
            .Include(p => p.Cascata!).ThenInclude(c => c.Destinos).FirstAsync(p => p.Id == id);
        ConferirCascataDoProcesso(processo, definir);
        if (publicar.IsFailure)
        {
            await ConferirPublicacaoRecusadaAsync(db, id, processo, publicar);
        }
        else
        {
            processo.Status.Should().Be(StatusProcesso.Publicado);
            await ConferirCascataCongeladaAsync(db, id);
        }
    }

    private static void ConferirCascataDoProcesso(ProcessoSeletivo processo, Result definir)
    {
        definir.IsSuccess.Should().BeTrue(definir.Error?.Message);
        processo.Cascata.Should().NotBeNull();
        processo.Cascata!.Destinos.Should().HaveCount(56);
    }

    private static async Task ConferirPublicacaoRecusadaAsync(UniPlusDbContext db, Guid id, ProcessoSeletivo processo, Result publicar)
    {
        publicar.Error!.Code.Should().Be("ProcessoSeletivo.CascataOrigemAusente");
        processo.Status.Should().Be(StatusProcesso.Rascunho);
        bool existe = await db.VersoesConfiguracao.AsNoTracking().AnyAsync(v => v.ProcessoSeletivoId == id);
        existe.Should().BeFalse();
    }

    private static async Task ConferirCascataCongeladaAsync(UniPlusDbContext db, Guid id)
    {
        VersaoConfiguracao versao = await db.VersoesConfiguracao.AsNoTracking().SingleAsync(v => v.ProcessoSeletivoId == id);
        JsonObject cascata = JsonNode.Parse(versao.ConfiguracaoCongelada)!["cascataRemanejamento"]!.AsObject();
        cascata["presente"]!.GetValue<bool>().Should().BeTrue();
        cascata["ordens"]!.AsArray().Should().HaveCount(8);
    }

    /// <summary>
    /// A oferta federal completa (Lei 12.711), SEM a cascata que ela exige —
    /// <c>ProcessoSeletivoPublicacaoSeeder.NovoProcessoComOfertaFederalECascata(nome,
    /// comCascata: false)</c> — mais um <see cref="DocumentoAPublicar"/> já confirmado, no
    /// mesmo padrão de <see cref="ProcessoSeletivoPublicavelSeeder.SemearAsync"/>.
    /// </summary>
    private static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoFederalSemCascataAsync(
        UniPlusDbContext db, string nome)
    {
        ProcessoSeletivo processo = Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos
            .ProcessoSeletivoPublicacaoSeeder.NovoProcessoComOfertaFederalECascata(nome, comCascata: false);
        await db.ProcessosSeletivos.AddAsync(processo);

        DocumentoAPublicar documento = DocumentoConfirmado(processo.Id);
        await db.DocumentosAPublicar.AddAsync(documento);

        await db.SaveChangesAsync();
        processo.DequeueDomainEvents();

        return (processo.Id, documento.Id);
    }

    private static DocumentoAPublicar DocumentoConfirmado(Guid processoId)
    {
        string hash = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            processoId, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf,
            TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, hash, TimeProvider.System).IsSuccess.Should().BeTrue();
        return documento;
    }

    /// <summary>
    /// A matriz legal completa (8×7), fallback AC, como <c>DestinoRemanejamentoInput</c> —
    /// mesma forma exigida pelo <c>declaracao_da_regra</c> congelado de <c>REMANEJ-CASCATA-LEI-12711
    /// v1</c> (cascata de remanejamento de cotas, UNI-REQ-0056, validada pelo handler): cada origem lista as outras 7 federais,
    /// na ordem de <see cref="ModalidadesFederaisLei12711.Codigos"/>.
    /// </summary>
    private static List<DestinoRemanejamentoInput> MatrizLegalCompletaComoInput() =>
        [.. ModalidadesFederaisLei12711.Codigos.SelectMany(static origem =>
            ModalidadesFederaisLei12711.Codigos.Where(destino => destino != origem)
                .Select((destino, indice) => new DestinoRemanejamentoInput(origem, indice + 1, destino)))];
}
