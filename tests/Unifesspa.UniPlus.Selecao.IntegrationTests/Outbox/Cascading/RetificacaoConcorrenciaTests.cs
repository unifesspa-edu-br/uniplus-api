namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Wolverine;

using Application.Commands.ProcessosSeletivos;
using Domain.Entities;
using Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Cobertura de concorrência do ciclo de publicação (ADR-0101/0102/0103/0104): a cadeia de
/// versões do certame não pode ramificar nem duplicar sob corrida.
/// </summary>
/// <remarks>
/// <para>
/// Os guard rails mudaram de casa com a #804, e é isso que estes testes travam. Não existe
/// mais <c>ux_editais_processo_abertura_unica</c> (o índice que filtrava por
/// <c>natureza = 1</c>) nem <c>ux_editais_edital_retificado_unico</c>. Quem garante o mesmo,
/// agora sem literal de tipo de ato no filtro:
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
/// duplicação, mas só no consumo da fila durável — são backstop pós-commit, não guard rail
/// transacional, e por isso não podem ser a única defesa.
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

        var publicarCommand = new PublicarProcessoSeletivoCommand(
            processoId,
            Numero: null,
            PeriodoInscricaoInicio: new DateOnly(2026, 1, 1),
            PeriodoInscricaoFim: new DateOnly(2026, 1, 31),
            DocumentoEditalId: documentoId,
            Ato: DadosDoAtoDeTeste.Padrao);

        await using AsyncServiceScope scopeA = api.Services.CreateAsyncScope();
        await using AsyncServiceScope scopeB = api.Services.CreateAsyncScope();
        IMessageBus busA = scopeA.ServiceProvider.GetRequiredService<IMessageBus>();
        IMessageBus busB = scopeB.ServiceProvider.GetRequiredService<IMessageBus>();

        Task<Result> taskA = busA.InvokeAsync<Result>(publicarCommand);
        Task<Result> taskB = busB.InvokeAsync<Result>(publicarCommand);
        await Task.WhenAll(taskA, taskB);

        Result[] resultados = [await taskA, await taskB];

        // Exatamente UMA vence. A segunda é barrada pelo lock pessimista (relê Status =
        // Publicado → TransicaoInvalida) ou, se a janela escapar, pelo índice de numeração
        // das versões. Antes da #804 quem barrava era ux_editais_processo_abertura_unica —
        // e ele carregava `natureza = 1` no filtro, o literal que esta story eliminou.
        resultados.Count(r => r.IsSuccess).Should().Be(1, "só uma publicação pode abrir a cadeia do certame");

        Result perdedor = resultados.Single(r => r.IsFailure);
        (perdedor.HasErrorCode("ProcessoSeletivo.TransicaoInvalida")
            || perdedor.HasErrorCode("VersaoConfiguracao.NumeroDuplicado"))
            .Should().BeTrue(
                $"a publicação perdedora é recusada por transição ou pelo índice de numeração — veio '{perdedor.Error?.Code}'");

        await using AsyncServiceScope readScope = api.Services.CreateAsyncScope();
        SelecaoDbContext readDb = readScope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
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

        // Publica a abertura pelo bus (mesmo caminho do handler HTTP).
        var publicarCommand = new PublicarProcessoSeletivoCommand(
            processoId,
            Numero: null,
            PeriodoInscricaoInicio: new DateOnly(2026, 1, 1),
            PeriodoInscricaoFim: new DateOnly(2026, 1, 31),
            DocumentoEditalId: documentoAbertura,
            Ato: DadosDoAtoDeTeste.Padrao);
        await using (AsyncServiceScope publicarScope = api.Services.CreateAsyncScope())
        {
            IMessageBus publicarBus = publicarScope.ServiceProvider.GetRequiredService<IMessageBus>();
            Result publicarResultado = await publicarBus.InvokeAsync<Result>(publicarCommand);
            publicarResultado.IsSuccess.Should().BeTrue(publicarResultado.Error?.Message);
        }

        Guid atoAberturaId;
        Guid documentoRetificacao;
        await using (AsyncServiceScope preparoScope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = preparoScope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            atoAberturaId = await db.VersoesConfiguracao.AsNoTracking()
                .Where(v => v.ProcessoSeletivoId == processoId)
                .Select(v => v.AtoCriadorId)
                .SingleAsync();

            documentoRetificacao = await SemearDocumentoConfirmadoAsync(db, processoId);
        }

        var retificarCommand = new RetificarProcessoSeletivoCommand(
            processoId,
            "Correção concorrente do prazo de inscrição",
            Numero: "001/2026-R1",
            PeriodoInscricaoInicio: new DateOnly(2026, 2, 1),
            PeriodoInscricaoFim: new DateOnly(2026, 2, 28),
            DocumentoEditalId: documentoRetificacao,
            Ato: DadosDoAtoDeTeste.Padrao);

        await using AsyncServiceScope scopeA = api.Services.CreateAsyncScope();
        await using AsyncServiceScope scopeB = api.Services.CreateAsyncScope();
        IMessageBus busA = scopeA.ServiceProvider.GetRequiredService<IMessageBus>();
        IMessageBus busB = scopeB.ServiceProvider.GetRequiredService<IMessageBus>();

        Task<Result> retificarTaskA = busA.InvokeAsync<Result>(retificarCommand);
        Task<Result> retificarTaskB = busB.InvokeAsync<Result>(retificarCommand);
        await Task.WhenAll(retificarTaskA, retificarTaskB);

        Result[] resultados = [await retificarTaskA, await retificarTaskB];

        // Ao menos uma conclui. Sob o lock as duas serializam e ambas concluem (empilhando na
        // cabeça); se a janela escapasse, o índice de numeração barraria a segunda. O
        // invariante testado é a NÃO-RAMIFICAÇÃO da cadeia, não o número de vencedores.
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

        await using AsyncServiceScope readScope = api.Services.CreateAsyncScope();
        SelecaoDbContext readDb = readScope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        List<VersaoConfiguracao> versoes = await readDb.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .OrderBy(v => v.NumeroVersao)
            .ToListAsync();

        // Linearidade (ADR-0103): nenhum ato é emendado por mais de um ramo.
        versoes.Where(v => v.AtoCriadorRetificaId is not null)
            .GroupBy(v => v.AtoCriadorRetificaId)
            .Where(g => g.Count() > 1)
            .Should().BeEmpty("nenhum ato pode ser emendado por dois ramos — a cadeia é linear");

        // Cada retificação concluída acrescenta exatamente uma versão; a numeração é
        // contígua a partir da abertura, e o ato da abertura é emendado no máximo uma vez.
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
        SelecaoDbContext seedDb = seedScope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, DocumentoEdital documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(seedDb, $"Concorrência {Guid.CreateVersion7()}");
        return (processo.Id, documento.Id);
    }

    private static async Task<Guid> SemearDocumentoConfirmadoAsync(SelecaoDbContext db, Guid processoId)
    {
        string hashFixo = string.Concat(Enumerable.Repeat("ef67012345", 7))[..64];
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processoId, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(2048, hashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        await db.DocumentosEdital.AddAsync(documento);
        await db.SaveChangesAsync();
        return documento.Id;
    }
}
