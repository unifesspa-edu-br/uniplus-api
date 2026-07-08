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
/// duas retificações do MESMO Edital vigente disparadas simultaneamente não
/// podem ramificar a cadeia — a linearidade "cada Edital é retificado no máximo
/// uma vez" tem de valer sob corrida. O lock pessimista
/// (<c>SELECT ... FOR UPDATE</c> em <c>ObterComConfiguracaoAsync</c>) serializa
/// os handlers: quando a segunda retificação recarrega o agregado, o vigente já
/// é o Edital de retificação recém-criado, e o guard de vigência a recusa. Se a
/// serialização falhasse e ambas passassem pelo guard em memória, o índice único
/// parcial <c>ux_editais_edital_retificado_unico</c> é o backstop de banco — em
/// qualquer caminho, exatamente uma retificação vence e a cadeia não ramifica.
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
        "Duas retificações concorrentes do mesmo Edital vigente não ramificam a cadeia (ADR-0101/0102)")]
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
            editalAberturaId,
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

        // Exatamente uma retificação vence; a outra é recusada — nunca as duas.
        new[] { resultadoA, resultadoB }.Count(r => r.IsSuccess).Should().Be(1,
            "o lock pessimista + guard de vigência serializam as retificações do mesmo processo");

        Result perdedor = resultadoA.IsFailure ? resultadoA : resultadoB;
        bool codigoDeLinearidade =
            perdedor.HasErrorCode("ProcessoSeletivo.EditalRetificadoInvalido")
            || perdedor.HasErrorCode("Edital.RetificacaoJaExiste");
        codigoDeLinearidade.Should().BeTrue(
            "a segunda retificação recarrega a cadeia com o novo vigente (guard em memória) " +
            "ou é barrada pelo índice único de banco — ambos preservam a linearidade");

        // A cadeia não ramificou: exatamente 1 abertura + 1 retificação.
        await using AsyncServiceScope readScope = api.Services.CreateAsyncScope();
        SelecaoDbContext readDb = readScope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        List<Edital> editais = await readDb.Set<Edital>().AsNoTracking()
            .Where(e => e.ProcessoSeletivoId == processoId)
            .ToListAsync();

        editais.Should().HaveCount(2);
        editais.Count(e => e.Natureza == NaturezaEdital.Retificacao).Should().Be(1,
            "a linearidade da cadeia (ADR-0101) impede um segundo ramo de retificação sobre o mesmo Edital");
        editais.Should().ContainSingle(e => e.EditalRetificadoId == editalAberturaId);
    }
}
