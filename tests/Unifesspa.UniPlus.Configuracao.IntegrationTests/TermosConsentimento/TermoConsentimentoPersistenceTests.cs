namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TermosConsentimento;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Integração ponta-a-ponta do catálogo de termos de consentimento (UNI-REQ-0086,
/// issue #1018) contra Postgres real: persistência do agregado + versões
/// promovidas (<c>IForensicEntity</c>, append-only) e soft-delete preservando
/// as versões filhas (<c>ClientNoAction</c>, mesmo padrão de
/// <c>CalendarioDiasUteis.DiasNaoUteis</c>).
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class TermoConsentimentoPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";
    private static readonly DateTimeOffset Agora = new(2027, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ConfiguracaoDbFixture _fixture;

    private static string NomeUnico() => $"Termo LGPD {Guid.NewGuid():N}";

    public TermoConsentimentoPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Persistir com rascunho completo e reler devolve os campos corretos")]
    public async Task Insert_PersisteRascunhoComCamposCorretos()
    {
        TermoConsentimento termo = NovoTermo("REGISTRO_DIGITAL_SEM_LOG_IP");
        await GravarAsync(termo);

        TermoConsentimento persistido = await LerAsync(termo.Id);

        persistido.CreatedBy.Should().Be(AdminA);
        persistido.Nome.Should().Be(termo.Nome);
        persistido.TextoRascunho.Should().Be("Texto do termo");
        persistido.BaseLegalRascunho.Should().Be("Lei 13.709/2018");
        persistido.FormaAceiteRascunho.Should().Be(FormaAceite.RegistroDigitalSemLogIp);
        persistido.Revisado.Should().BeFalse();
    }

    // O termo é recarregado (já rastreado) e a versão precisa ser adicionada explicitamente ao DbSet: o EF Core não a
    // detecta como nova só pela coleção em memória, porque o Id gerado no cliente parece "já existente".
    [Fact(DisplayName = "Promover após reload persiste a versão imutável via AdicionarVersaoAsync")]
    public async Task Promover_AposReload_PersisteVersaoImutavelViaRepositorio()
    {
        TermoConsentimento termo = TermoRevisado("REGISTRO_DIGITAL_SEM_LOG_IP");
        await GravarAsync(termo);
        await PromoverAsync(termo.Id);

        TermoConsentimento persistido = await LerAsync(termo.Id, comVersoes: true);

        persistido.Versoes.Should().ContainSingle();
        TermoConsentimentoVersao versao = persistido.Versoes[0];
        (versao.Texto, versao.BaseLegal, versao.FormaAceite, versao.PromovidaPor).Should().Be(
            ("Texto do termo", "Lei 13.709/2018", FormaAceite.RegistroDigitalSemLogIp, "usuario.revisor"));
        versao.Hash.Should().HaveLength(64, "hash é SHA-256 hex (64 caracteres)");
        persistido.Revisado.Should().BeFalse("a revisão é consumida pela promoção");
    }

    [Fact(DisplayName = "Promover concorrente com edição que reverte a revisão falha por concorrência (xmin)")]
    public async Task Promover_ConcorrenteComEdicaoQueReverteRevisao_FalhaPorConcorrencia()
    {
        await using UniPlusDbContext ctxA = _fixture.CreateDbContext(AdminA);
        await PrepararPromocaoEmConflitoAsync(ctxA);

        Func<Task> act = async () => await ctxA.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    // O Wolverine chama SaveChangesAsync no mesmo DbContext depois que o handler retorna: sem descartar o rastreamento da
    // tentativa fracassada, a mesma exceção estoura fora do catch do handler (500 em vez de 409).
    [Fact(DisplayName = "Após o conflito capturado, descartar o rastreamento evita a segunda exceção do SaveChanges do outbox")]
    public async Task ConflitoDeConcorrencia_AposDescartarRastreamento_SegundoSaveChangesNaoLancaDeNovo()
    {
        await using UniPlusDbContext ctxA = _fixture.CreateDbContext(AdminA);
        await PrepararPromocaoEmConflitoAsync(ctxA);

        Func<Task> primeiroSave = async () => await ctxA.SaveChangesAsync();
        await primeiroSave.Should().ThrowAsync<DbUpdateConcurrencyException>();
        ctxA.ChangeTracker.Clear();

        Func<Task> segundoSave = async () => await ctxA.SaveChangesAsync();
        await segundoSave.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Soft-delete de termo sem versões marca IsDeleted/DeletedBy")]
    public async Task SoftDelete_SemVersoes_MarcaExcluido()
    {
        TermoConsentimento termo = TermoConsentimento.Criar(NomeUnico(), null, null, null).Value!;
        await GravarAsync(termo);
        await RemoverAsync(termo.Id);

        TermoConsentimento excluido = await LerExcluidoAsync(termo.Id);

        excluido.IsDeleted.Should().BeTrue();
        excluido.DeletedBy.Should().Be(AdminB);
    }

    // O bloqueio de negócio (RemocaoBloqueadaComVersaoPromovida) vive no handler; aqui se prova que, se a linha chegar a
    // ser removida, o mapeamento do EF Core não quebra a versão filha.
    [Fact(DisplayName = "Soft-delete de termo com versão promovida preserva a versão filha (ClientNoAction)")]
    public async Task SoftDelete_ComVersaoPromovida_PreservaVersaoFilha()
    {
        TermoConsentimento termo = TermoRevisado(null);
        await GravarAsync(termo);
        await PromoverAsync(termo.Id);
        await RemoverAsync(termo.Id);

        TermoConsentimento excluido = await LerExcluidoAsync(termo.Id, comVersoes: true);

        excluido.IsDeleted.Should().BeTrue();
        excluido.Versoes.Should().HaveCount(1, "a versão promovida (IForensicEntity) não tem soft-delete próprio e permanece sob a linha soft-deleted");
    }

    [Fact(DisplayName = "CHECK de banco rejeita forma de aceite fora do contrato via SQL cru")]
    public async Task Check_RejeitaFormaDeAceiteForaDoContratoViaSqlCru()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.termos_de_consentimento (id, codigo, nome, forma_aceite_rascunho, revisado, is_deleted, created_at) VALUES ({Guid.CreateVersion7()}, {"TERMO_FORA_DO_CONTRATO"}, {"Termo"}, {"INEXISTENTE"}, {false}, {false}, {DateTimeOffset.UtcNow})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de domínio de forma_aceite_rascunho impede o INSERT direto");
    }

    private static TermoConsentimento NovoTermo(string? formaAceite) =>
        TermoConsentimento.Criar(NomeUnico(), "Texto do termo", "Lei 13.709/2018", formaAceite).Value!;

    private static TermoConsentimento TermoRevisado(string? formaAceite)
    {
        TermoConsentimento termo = NovoTermo(formaAceite);
        termo.MarcarRevisado("usuario.revisor", Agora);

        return termo;
    }

    private async Task GravarAsync(TermoConsentimento termo)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.TermosConsentimento.Add(termo);
        await ctx.SaveChangesAsync();
    }

    private async Task PromoverAsync(Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        TermoConsentimentoRepository repository = new(ctx);
        TermoConsentimento recarregado = (await repository.ObterPorIdAsync(id, CancellationToken.None))!;
        TermoConsentimentoVersao versao = recarregado.Promover("usuario.revisor", Agora).Value!;
        await repository.AdicionarVersaoAsync(recarregado, versao, CancellationToken.None);
        await ctx.SaveChangesAsync();
    }

    private async Task RemoverAsync(Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminB);
        TermoConsentimento rastreado = await ctx.TermosConsentimento.SingleAsync(t => t.Id == id);
        ctx.TermosConsentimento.Remove(rastreado);
        await ctx.SaveChangesAsync();
    }

    private async Task<TermoConsentimento> LerAsync(Guid id, bool comVersoes = false)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        IQueryable<TermoConsentimento> termos = comVersoes ? ctx.TermosConsentimento.Include(t => t.Versoes) : ctx.TermosConsentimento;

        return await termos.SingleAsync(t => t.Id == id);
    }

    private async Task<TermoConsentimento> LerExcluidoAsync(Guid id, bool comVersoes = false)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        IQueryable<TermoConsentimento> termos = ctx.TermosConsentimento.IgnoreQueryFilters();

        return await (comVersoes ? termos.Include(t => t.Versoes) : termos).SingleAsync(t => t.Id == id);
    }

    // Uma requisição lê o termo revisado e promove; antes de ela gravar, outra edita o rascunho (o que reverte a revisão)
    // e confirma. Sem o token de concorrência amarrado a uma gravação real, a promoção sairia de um rascunho já invalidado.
    private async Task PrepararPromocaoEmConflitoAsync(UniPlusDbContext ctxA)
    {
        TermoConsentimento termo = TermoRevisado(null);
        await GravarAsync(termo);

        TermoConsentimentoRepository repositoryA = new(ctxA);
        TermoConsentimento termoA = (await repositoryA.ObterPorIdAsync(termo.Id, CancellationToken.None))!;
        TermoConsentimentoVersao versaoA = termoA.Promover("usuario.revisor", Agora).Value!;

        await EditarEmOutraRequisicaoAsync(termo.Id);
        await repositoryA.AdicionarVersaoAsync(termoA, versaoA, CancellationToken.None);
    }

    private async Task EditarEmOutraRequisicaoAsync(Guid id)
    {
        await using UniPlusDbContext ctxB = _fixture.CreateDbContext(AdminA);
        TermoConsentimento termoB = await ctxB.TermosConsentimento.SingleAsync(t => t.Id == id);
        termoB.EditarRascunho("Texto ajustado por outra requisição", "Lei 13.709/2018", null);
        await ctxB.SaveChangesAsync();
    }
}
