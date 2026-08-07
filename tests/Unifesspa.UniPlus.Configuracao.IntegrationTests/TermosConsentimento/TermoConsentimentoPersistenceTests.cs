namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TermosConsentimento;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Integração ponta-a-ponta do catálogo de termos de consentimento (UNI-REQ-0086,
/// issue #1018) contra Postgres real: persistência do agregado + versões
/// promovidas (<c>IForensicEntity</c>, append-only) e soft-delete preservando
/// as versões filhas (<c>ClientNoAction</c>, mesmo padrão de
/// <c>CalendarioDiasUteis.DiasNaoUteis</c>).
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TermoConsentimentoPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";
    private static readonly DateTimeOffset Agora = new(2027, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ConfiguracaoDbFixture _fixture;

    public TermoConsentimentoPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Persistir com rascunho completo e reler devolve os campos corretos")]
    public async Task Insert_PersisteRascunhoComCamposCorretos()
    {
        TermoConsentimento termo = TermoConsentimento.Criar(
            "Termo LGPD", "Texto do termo", "Lei 13.709/2018", "REGISTRO_DIGITAL_SEM_LOG_IP").Value!;

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TermosConsentimento.Add(termo);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        TermoConsentimento persistido = await readCtx.TermosConsentimento
            .SingleAsync(t => t.Id == termo.Id);

        persistido.CreatedBy.Should().Be(AdminA);
        persistido.Nome.Should().Be("Termo LGPD");
        persistido.TextoRascunho.Should().Be("Texto do termo");
        persistido.BaseLegalRascunho.Should().Be("Lei 13.709/2018");
        persistido.FormaAceiteRascunho.Should().Be(FormaAceite.RegistroDigitalSemLogIp);
        persistido.Revisado.Should().BeFalse();
    }

    [Fact(DisplayName = "Promover após reload persiste a versão imutável via AdicionarVersaoAsync")]
    public async Task Promover_AposReload_PersisteVersaoImutavelViaRepositorio()
    {
        // Reproduz o fluxo real: criar+revisar em uma requisição/contexto, promover
        // numa SEGUNDA requisição/contexto — o termo é recarregado (já rastreado),
        // não construído do zero. Sob esse fluxo, adicionar a versão só à coleção em
        // memória do agregado NÃO basta (o EF Core não a detecta como Added — o Id
        // gerado client-side parece "já existente" à heurística de DetectChanges);
        // a versão precisa ser adicionada explicitamente ao DbSet
        // (ITermoConsentimentoRepository.AdicionarVersaoAsync), como o handler faz.
        TermoConsentimento termo = TermoConsentimento.Criar(
            "Termo LGPD", "Texto do termo", "Lei 13.709/2018", "REGISTRO_DIGITAL_SEM_LOG_IP").Value!;
        termo.MarcarRevisado("usuario.revisor", Agora);

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TermosConsentimento.Add(termo);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            TermoConsentimentoRepository repository = new(ctx);
            TermoConsentimento reloaded = (await repository.ObterPorIdAsync(termo.Id, CancellationToken.None))!;

            TermoConsentimentoVersao versao = reloaded.Promover("usuario.revisor", Agora).Value!;
            await repository.AdicionarVersaoAsync(reloaded, versao, CancellationToken.None);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        TermoConsentimento persistido = await readCtx.TermosConsentimento
            .Include(t => t.Versoes)
            .SingleAsync(t => t.Id == termo.Id);

        persistido.Versoes.Should().HaveCount(1);
        TermoConsentimentoVersao versaoPersistida = persistido.Versoes[0];
        versaoPersistida.Texto.Should().Be("Texto do termo");
        versaoPersistida.BaseLegal.Should().Be("Lei 13.709/2018");
        versaoPersistida.FormaAceite.Should().Be(FormaAceite.RegistroDigitalSemLogIp);
        versaoPersistida.PromovidaPor.Should().Be("usuario.revisor");
        versaoPersistida.Hash.Should().HaveLength(64, "hash é SHA-256 hex (64 caracteres)");

        // O texto/base legal do rascunho permanecem intactos após a promoção; só a
        // revisão é consumida (status volta a EM_ELABORACAO).
        persistido.TextoRascunho.Should().Be("Texto do termo");
        persistido.Revisado.Should().BeFalse();
    }

    [Fact(DisplayName = "Promover concorrente com edição que reverte a revisão falha por concorrência (xmin)")]
    public async Task Promover_ConcorrenteComEdicaoQueReverteRevisao_FalhaPorConcorrencia()
    {
        // Reproduz uma corrida de leitura-escrita: request A lê o termo revisado
        // (fica com uma cópia rastreada nesse estado); antes de A promover, request B
        // carrega o MESMO termo, edita o rascunho (o que reverte Revisado para false)
        // e comita. Sem o xmin amarrado a um write real no commit de A, a promoção
        // de A sairia gravada a partir de um rascunho já invalidado.
        TermoConsentimento termo = TermoConsentimento.Criar(
            "Termo LGPD", "Texto do termo", "Lei 13.709/2018", null).Value!;
        termo.MarcarRevisado("usuario.revisor", Agora);

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TermosConsentimento.Add(termo);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext ctxA = _fixture.CreateDbContext(AdminA);
        TermoConsentimentoRepository repositoryA = new(ctxA);
        TermoConsentimento termoA = (await repositoryA.ObterPorIdAsync(termo.Id, CancellationToken.None))!;
        TermoConsentimentoVersao versaoA = termoA.Promover("usuario.revisor", Agora).Value!;

        await using (ConfiguracaoDbContext ctxB = _fixture.CreateDbContext(AdminA))
        {
            TermoConsentimento termoB = await ctxB.TermosConsentimento.SingleAsync(t => t.Id == termo.Id);
            termoB.EditarRascunho("Texto ajustado por outra requisição", "Lei 13.709/2018", null);
            await ctxB.SaveChangesAsync();
        }

        await repositoryA.AdicionarVersaoAsync(termoA, versaoA, CancellationToken.None);

        Func<Task> act = async () => await ctxA.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact(DisplayName = "Após o conflito capturado, descartar o rastreamento evita a segunda exceção do SaveChanges do outbox")]
    public async Task ConflitoDeConcorrencia_AposDescartarRastreamento_SegundoSaveChangesNaoLancaDeNovo()
    {
        // Reproduz a interação com o outbox do Wolverine
        // (AutoApplyTransactions, ADR-0004), que chama SaveChangesAsync no MESMO
        // DbContext DEPOIS que o handler retorna. Se o handler capturar a
        // DbUpdateConcurrencyException e devolver a falha sem descartar o
        // rastreamento das entidades Added/Modified da tentativa fracassada, essa
        // segunda chamada tenta gravar as MESMAS entidades de novo e a mesma
        // exceção estoura fora do catch do handler — 500 em vez de 409.
        TermoConsentimento termo = TermoConsentimento.Criar(
            "Termo LGPD", "Texto do termo", "Lei 13.709/2018", null).Value!;
        termo.MarcarRevisado("usuario.revisor", Agora);

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TermosConsentimento.Add(termo);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext ctxA = _fixture.CreateDbContext(AdminA);
        TermoConsentimentoRepository repositoryA = new(ctxA);
        TermoConsentimento termoA = (await repositoryA.ObterPorIdAsync(termo.Id, CancellationToken.None))!;
        TermoConsentimentoVersao versaoA = termoA.Promover("usuario.revisor", Agora).Value!;

        await using (ConfiguracaoDbContext ctxB = _fixture.CreateDbContext(AdminA))
        {
            TermoConsentimento termoB = await ctxB.TermosConsentimento.SingleAsync(t => t.Id == termo.Id);
            termoB.EditarRascunho("Texto ajustado por outra requisição", "Lei 13.709/2018", null);
            await ctxB.SaveChangesAsync();
        }

        await repositoryA.AdicionarVersaoAsync(termoA, versaoA, CancellationToken.None);

        // Simula exatamente o que o handler faz: tenta salvar, captura a
        // concorrência, descarta o rastreamento (equivalente a
        // IConfiguracaoUnitOfWork.DescartarAlteracoesNaoSalvas).
        Func<Task> primeiroSave = async () => await ctxA.SaveChangesAsync();
        await primeiroSave.Should().ThrowAsync<DbUpdateConcurrencyException>();
        ctxA.ChangeTracker.Clear();

        // O SaveChangesAsync automático do outbox do Wolverine, chamado DEPOIS que
        // o handler já retornou a falha — sem entidades sujas rastreadas, é um
        // no-op e não relança a exceção.
        Func<Task> segundoSave = async () => await ctxA.SaveChangesAsync();
        await segundoSave.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Soft-delete de termo sem versões marca IsDeleted/DeletedBy")]
    public async Task SoftDelete_SemVersoes_MarcaExcluido()
    {
        TermoConsentimento termo = TermoConsentimento.Criar("Termo LGPD", null, null, null).Value!;

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TermosConsentimento.Add(termo);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            TermoConsentimento tracked = await ctx.TermosConsentimento.SingleAsync(t => t.Id == termo.Id);
            ctx.TermosConsentimento.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        TermoConsentimento excluido = await readCtx.TermosConsentimento
            .IgnoreQueryFilters()
            .SingleAsync(t => t.Id == termo.Id);

        excluido.IsDeleted.Should().BeTrue();
        excluido.DeletedBy.Should().Be(AdminB);
    }

    [Fact(DisplayName = "Soft-delete de termo com versão promovida preserva a versão filha (ClientNoAction)")]
    public async Task SoftDelete_ComVersaoPromovida_PreservaVersaoFilha()
    {
        TermoConsentimento termo = TermoConsentimento.Criar(
            "Termo LGPD", "Texto do termo", "Lei 13.709/2018", null).Value!;
        termo.MarcarRevisado("usuario.revisor", Agora);

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TermosConsentimento.Add(termo);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            TermoConsentimentoRepository repository = new(ctx);
            TermoConsentimento reloaded = (await repository.ObterPorIdAsync(termo.Id, CancellationToken.None))!;
            TermoConsentimentoVersao versao = reloaded.Promover("usuario.revisor", Agora).Value!;
            await repository.AdicionarVersaoAsync(reloaded, versao, CancellationToken.None);
            await ctx.SaveChangesAsync();
        }

        // Cenário direto de infraestrutura — o gate de negócio (RemocaoBloqueadaComVersaoPromovida)
        // vive no handler da Application; aqui se prova que, SE a linha chegar a ser
        // marcada como Removed, o mapeamento EF Core não quebra a versão filha.
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            TermoConsentimento tracked = await ctx.TermosConsentimento.SingleAsync(t => t.Id == termo.Id);
            ctx.TermosConsentimento.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        TermoConsentimento excluido = await readCtx.TermosConsentimento
            .IgnoreQueryFilters()
            .Include(t => t.Versoes)
            .SingleAsync(t => t.Id == termo.Id);

        excluido.IsDeleted.Should().BeTrue();
        excluido.Versoes.Should().HaveCount(1, "a versão promovida (IForensicEntity) não tem soft-delete próprio e permanece sob a linha soft-deleted");
    }
}
