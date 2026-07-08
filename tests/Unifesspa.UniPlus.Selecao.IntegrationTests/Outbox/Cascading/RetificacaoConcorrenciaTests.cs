namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Wolverine;

using Application.Commands.ProcessosSeletivos;
using Domain.Entities;
using Domain.Enums;
using Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Cobertura de concorrência da retificação (Story #759 T5 #786, ADR-0101/0102):
/// duas retificações do mesmo processo disparadas simultaneamente não podem
/// ramificar a cadeia — a linearidade "nenhum Edital é sucedido por dois ramos"
/// tem de valer sob corrida. O lock pessimista (<c>SELECT ... FOR UPDATE</c> em
/// <c>ObterComConfiguracaoAsync</c>) serializa os handlers: quando a segunda
/// retificação recarrega o agregado, o vigente já é o Edital de retificação
/// recém-criado, então ela sucede ESSE — empilhando em cadeia linear
/// (abertura→R1→R2), sem ramificar. Se a serialização falhasse e ambas lessem o
/// mesmo vigente, o índice único parcial <c>ux_editais_edital_retificado_unico</c>
/// é o backstop de banco que barra a segunda. Em qualquer caminho a cadeia
/// permanece estritamente linear.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class RetificacaoConcorrenciaTests
{
    private readonly CascadingFixture _fixture;

    public RetificacaoConcorrenciaTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "Duas retificações concorrentes do mesmo processo não ramificam a cadeia (ADR-0101/0102)")]
    public async Task DuasRetificacoesConcorrentes_NaoRamificamCadeia()
    {
        CascadingApiFactory api = _fixture.Factory;

        Guid processoId;
        Guid documentoAbertura;
        await using (AsyncServiceScope seedScope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext seedDb = seedScope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (ProcessoSeletivo processo, DocumentoEdital documento) = await ProcessoSeletivoPublicavelSeeder
                .SemearAsync(seedDb, $"Concorrência retificação {Guid.CreateVersion7()}");
            processoId = processo.Id;
            documentoAbertura = documento.Id;
        }

        // Publica a abertura pelo bus (mesmo caminho do handler HTTP).
        var publicarCommand = new PublicarProcessoSeletivoCommand(
            processoId,
            Numero: null,
            PeriodoInscricaoInicio: new DateOnly(2026, 1, 1),
            PeriodoInscricaoFim: new DateOnly(2026, 1, 31),
            DocumentoEditalId: documentoAbertura);
        await using (AsyncServiceScope publicarScope = api.Services.CreateAsyncScope())
        {
            IMessageBus publicarBus = publicarScope.ServiceProvider.GetRequiredService<IMessageBus>();
            Result publicarResultado = await publicarBus.InvokeAsync<Result>(publicarCommand);
            publicarResultado.IsSuccess.Should().BeTrue(publicarResultado.Error?.Message);
        }

        Guid editalAberturaId;
        Guid documentoRetificacao;
        await using (AsyncServiceScope preparoScope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = preparoScope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            editalAberturaId = await db.Set<Edital>().AsNoTracking()
                .Where(e => e.ProcessoSeletivoId == processoId)
                .Select(e => e.Id)
                .SingleAsync();

            string hashFixo = string.Concat(Enumerable.Repeat("ef67012345", 7))[..64];
            DocumentoEdital documento = DocumentoEdital.IniciarPendente(processoId, TimeProvider.System, TimeSpan.FromMinutes(15));
            documento.Confirmar(2048, hashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
            await db.DocumentosEdital.AddAsync(documento);
            await db.SaveChangesAsync();
            documentoRetificacao = documento.Id;
        }

        var retificarCommand = new RetificarProcessoSeletivoCommand(
            processoId,
            "Correção concorrente do prazo de inscrição",
            Numero: "001/2026-R1",
            PeriodoInscricaoInicio: new DateOnly(2026, 2, 1),
            PeriodoInscricaoFim: new DateOnly(2026, 2, 28),
            DocumentoEditalId: documentoRetificacao);

        await using AsyncServiceScope scopeA = api.Services.CreateAsyncScope();
        await using AsyncServiceScope scopeB = api.Services.CreateAsyncScope();
        IMessageBus busA = scopeA.ServiceProvider.GetRequiredService<IMessageBus>();
        IMessageBus busB = scopeB.ServiceProvider.GetRequiredService<IMessageBus>();

        Task<Result> retificarTaskA = busA.InvokeAsync<Result>(retificarCommand);
        Task<Result> retificarTaskB = busB.InvokeAsync<Result>(retificarCommand);
        await Task.WhenAll(retificarTaskA, retificarTaskB);

        Result resultadoA = await retificarTaskA;
        Result resultadoB = await retificarTaskB;

        // Ao menos uma retificação conclui. Sob o lock as duas serializam e
        // ambas concluem (empilhando); num cenário sem lock, o índice único
        // barra a segunda (falha com Edital.RetificacaoJaExiste). O invariante
        // testado é a NÃO-RAMIFICAÇÃO da cadeia, não o número de vencedores.
        int sucessos = new[] { resultadoA, resultadoB }.Count(r => r.IsSuccess);
        sucessos.Should().BeGreaterThanOrEqualTo(1, "ao menos uma retificação conclui");
        Result? perdedor = new[] { resultadoA, resultadoB }.FirstOrDefault(r => r.IsFailure);
        if (perdedor is not null)
        {
            perdedor.HasErrorCode("Edital.RetificacaoJaExiste").Should().BeTrue(
                "a retificação que perde a corrida sem o lock é barrada pelo índice único de banco");
        }

        await using AsyncServiceScope readScope = api.Services.CreateAsyncScope();
        SelecaoDbContext readDb = readScope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        List<Edital> editais = await readDb.Set<Edital>().AsNoTracking()
            .Where(e => e.ProcessoSeletivoId == processoId)
            .ToListAsync();

        // Invariante de linearidade (ADR-0101): nenhum Edital é sucedido por
        // mais de um ramo de retificação.
        editais.Where(e => e.EditalRetificadoId is not null)
            .GroupBy(e => e.EditalRetificadoId)
            .Where(g => g.Count() > 1)
            .Should().BeEmpty("nenhum Edital pode ser sucedido por dois ramos — a cadeia é linear");

        // Cada retificação concluída acrescenta exatamente um Edital de
        // retificação; a abertura é sucedida por no máximo um deles.
        editais.Count(e => e.Natureza == NaturezaEdital.Retificacao).Should().Be(sucessos);
        editais.Should().ContainSingle(e => e.EditalRetificadoId == editalAberturaId);
    }
}
