namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using Application.Commands.ProcessosSeletivos;

using AwesomeAssertions;

using Domain.Entities;

using Kernel.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Wolverine;

/// <summary>
/// Cobertura de concorrência do ciclo de publicação (ADR-0101/0102/0103/0104): a cadeia de
/// versões do certame não pode ramificar nem duplicar sob corrida.
/// </summary>
/// <remarks>
/// <para>
/// A integridade da cadeia é garantida sem depender do tipo de ato no filtro:
/// </para>
/// <list type="bullet">
///   <item><c>ux_versoes_configuracao_processo_numero</c> — duas publicações concorrentes
///     derivam a MESMA versão 1, e o índice deixa passar uma só;</item>
///   <item><c>ux_versoes_configuracao_ato_criador</c> mais o trigger de sucessão
///     (<c>ck_versoes_configuracao_cadeia</c>) — um ato cria no máximo uma versão, e a
///     sucessora tem de emendar o ato criador da anterior.</item>
/// </list>
/// <para>
/// Ambos atuam na MESMA transação da Seleção. Os índices de Publicações também barram a
/// duplicação, mas só no consumo da fila durável, após a confirmação da transação;
/// por isso não podem ser a única defesa.
/// </para>
/// </remarks>
[Collection(CascadingCollection.Name)]
public sealed class RetificacaoConcorrenciaTests
{
    private readonly CascadingFixture _fixture;

    public RetificacaoConcorrenciaTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "Duas PUBLICAÇÕES concorrentes do mesmo processo não criam duas versões 1 — o índice de numeração substitui o de abertura única")]
    public async Task DuasPublicacoesConcorrentes_NaoDuplicamAVersaoUm()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);

        (Guid processoId, Guid documentoId) = await SemearProcessoPublicavelAsync(api);

        PublicarProcessoSeletivoCommand publicarCommand = PrepararPublicacao(processoId, documentoId);

        Result[] resultados = await PublicarConcorrentementeAsync(api, publicarCommand);
        ConferirResultadoDasPublicacoes(resultados);
        await ConferirVersaoDeAberturaAsync(api, processoId);
    }

    private static async Task<Result[]> PublicarConcorrentementeAsync(CascadingApiFactory api, PublicarProcessoSeletivoCommand command)
    {
        await using AsyncServiceScope scopeA = api.Services.CreateAsyncScope();
        await using AsyncServiceScope scopeB = api.Services.CreateAsyncScope();
        IMessageBus busA = scopeA.ServiceProvider.GetRequiredService<IMessageBus>();
        IMessageBus busB = scopeB.ServiceProvider.GetRequiredService<IMessageBus>();
        Task<Result> taskA = busA.InvokeAsync<Result>(command);
        Task<Result> taskB = busB.InvokeAsync<Result>(command);
        await Task.WhenAll(taskA, taskB);
        return [await taskA, await taskB];
    }

    private static PublicarProcessoSeletivoCommand PrepararPublicacao(Guid processoId, Guid documentoId) =>
        new(processoId, Numero: null, PeriodoInscricaoInicio: null, PeriodoInscricaoFim: null,
            DocumentoEditalId: documentoId, Ato: DadosDoAtoDeTeste.Padrao);

    private static void ConferirResultadoDasPublicacoes(Result[] resultados)
    {
        resultados.Count(r => r.IsSuccess).Should().Be(1, "só uma publicação pode abrir a cadeia do certame");

        Result perdedor = resultados.Single(r => r.IsFailure);
        (perdedor.HasErrorCode("ProcessoSeletivo.TransicaoInvalida")
            || perdedor.HasErrorCode("VersaoConfiguracao.NumeroDuplicado"))
            .Should().BeTrue(
                $"a publicação perdedora é recusada por transição ou pelo índice de numeração — veio '{perdedor.Error?.Code}'");
    }

    private static async Task ConferirVersaoDeAberturaAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope readScope = api.Services.CreateAsyncScope();
        UniPlusDbContext readDb = readScope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        List<VersaoConfiguracao> versoes = await readDb.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .ToListAsync();

        versoes.Should().ContainSingle("o certame tem exatamente uma versão 1 — nunca duas");
        versoes[0].NumeroVersao.Should().Be(1);
        versoes[0].AtoCriadorRetificaId.Should().BeNull("a raiz da cadeia não emenda ninguém");
    }

    [Fact(DisplayName =
        "Duas retificações concorrentes do mesmo processo não ramificam a cadeia de versões (ADR-0102/0104)")]
    public async Task DuasRetificacoesConcorrentes_NaoRamificamCadeia()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoAbertura) = await SemearProcessoPublicavelAsync(api);
        await PublicarAberturaAsync(api, processoId, documentoAbertura);
        (Guid atoAberturaId, Guid documentoRetificacao) = await PrepararRetificacaoAsync(api, processoId);
        var command = new RetificarProcessoSeletivoCommand(
            processoId, "Correção concorrente do prazo de inscrição", Numero: "001/2026-R1",
            PeriodoInscricaoInicio: null, PeriodoInscricaoFim: null,
            DocumentoEditalId: documentoRetificacao, Ato: DadosDoAtoDeTeste.Padrao);
        Result[] resultados = await RetificarConcorrentementeAsync(api, command);
        int sucessos = ConferirResultadoDasRetificacoes(resultados);
        await ConferirCadeiaDeVersoesAsync(api, processoId, atoAberturaId, sucessos);
    }

    private static async Task PublicarAberturaAsync(CascadingApiFactory api, Guid processoId, Guid documentoId)
    {
        PublicarProcessoSeletivoCommand command = PrepararPublicacao(processoId, documentoId);
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        Result resultado = await bus.InvokeAsync<Result>(command);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    private static async Task<(Guid AtoAberturaId, Guid DocumentoRetificacao)> PrepararRetificacaoAsync(
        CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Guid atoAberturaId = await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .Select(v => v.AtoCriadorId)
            .SingleAsync();
        Guid documentoRetificacao = await SemearDocumentoConfirmadoAsync(db, processoId);
        return (atoAberturaId, documentoRetificacao);
    }

    private static async Task<Result[]> RetificarConcorrentementeAsync(CascadingApiFactory api, RetificarProcessoSeletivoCommand command)
    {
        await using AsyncServiceScope scopeA = api.Services.CreateAsyncScope();
        await using AsyncServiceScope scopeB = api.Services.CreateAsyncScope();
        IMessageBus busA = scopeA.ServiceProvider.GetRequiredService<IMessageBus>();
        IMessageBus busB = scopeB.ServiceProvider.GetRequiredService<IMessageBus>();
        Task<Result> taskA = busA.InvokeAsync<Result>(command);
        Task<Result> taskB = busB.InvokeAsync<Result>(command);
        await Task.WhenAll(taskA, taskB);
        return [await taskA, await taskB];
    }

    private static int ConferirResultadoDasRetificacoes(Result[] resultados)
    {
        int sucessos = resultados.Count(r => r.IsSuccess);
        sucessos.Should().BeGreaterThanOrEqualTo(1, "ao menos uma retificação conclui");
        if (resultados.FirstOrDefault(r => r.IsFailure) is { } perdedor)
        {
            (perdedor.HasErrorCode("VersaoConfiguracao.NumeroDuplicado")
                || perdedor.HasErrorCode("VersaoConfiguracao.CadeiaQuebrada")
                || perdedor.HasErrorCode("ProcessoSeletivo.AtoJaRetificado"))
                .Should().BeTrue(
                    $"a retificação que perde a corrida é barrada por um guard rail da CADEIA DE VERSÕES — veio '{perdedor.Error?.Code}'");
        }
        return sucessos;
    }

    private static async Task ConferirCadeiaDeVersoesAsync(CascadingApiFactory api, Guid processoId, Guid atoAberturaId, int sucessos)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        List<VersaoConfiguracao> versoes = await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .OrderBy(v => v.NumeroVersao)
            .ToListAsync();
        ConferirLinearidadeENumeracao(versoes, atoAberturaId, sucessos);
    }

    private static void ConferirLinearidadeENumeracao(List<VersaoConfiguracao> versoes, Guid atoAberturaId, int sucessos)
    {
        versoes.Where(v => v.AtoCriadorRetificaId is not null)
            .GroupBy(v => v.AtoCriadorRetificaId)
            .Where(g => g.Count() > 1)
            .Should().BeEmpty("nenhum ato pode ser emendado por dois ramos — a cadeia é linear");
        versoes.Should().HaveCount(1 + sucessos);
        versoes.Select(v => v.NumeroVersao).Should().BeEquivalentTo(
            Enumerable.Range(1, versoes.Count),
            options => options.WithStrictOrdering(),
            "a numeração das versões é contígua — não há buraco nem repetição");
        versoes.Should().ContainSingle(v => v.AtoCriadorRetificaId == atoAberturaId);
    }

    private static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoPublicavelAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope seedScope = api.Services.CreateAsyncScope();
        UniPlusDbContext seedDb = seedScope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(seedDb, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"Concorrência {Guid.CreateVersion7()}" });
        return (processo.Id, documento.Id);
    }

    private static async Task<Guid> SemearDocumentoConfirmadoAsync(UniPlusDbContext db, Guid processoId)
    {
        string hashFixo = string.Concat(Enumerable.Repeat("ef67012345", 7))[..64];
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processoId, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(2048, hashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        await db.DocumentosAPublicar.AddAsync(documento);
        await db.SaveChangesAsync();
        return documento.Id;
    }
}
