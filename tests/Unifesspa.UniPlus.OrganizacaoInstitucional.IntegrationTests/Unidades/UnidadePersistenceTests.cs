namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// Integração ponta-a-ponta contra Postgres real. Cobre os
/// cenários de persistência da <see cref="Unidade"/>:
/// UNIQUE parcial slug/sigla/codigo, soft-delete liberando slot único,
/// histórico de identificadores, audit trail e FK hierárquica.
/// </summary>
public sealed class UnidadePersistenceTests : IClassFixture<UnidadeDbFixture>
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private static readonly DateOnly DataInicio = new(2026, 1, 1);

    private static readonly CidadeDaUnidade Maraba = new("1504208", "Marabá", "PA");

    private readonly UnidadeDbFixture _fixture;

    public UnidadePersistenceTests(UnidadeDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Insert persiste os identificadores da Unidade")]
    public async Task Insert_PersisteIdentificadoresDaUnidade()
    {
        Unidade unidade = NovaUnidade(new IdentificadoresDaUnidade("insert-ident", "CEPS-IDT", "IDT001", null));
        await _fixture.GravarAsync(AdminA, unidade);

        Unidade persistida = await _fixture.RecarregarAsync<Unidade>(unidade.Id);
        persistida.Slug.Should().Be(Slug.From("insert-ident").Value!);
        persistida.Sigla.Should().Be("CEPS-IDT");
        persistida.Codigo.Should().Be("IDT001");
        persistida.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "Insert persiste Unidade e cria 3 entradas de histórico (Slug, Sigla, Codigo)")]
    public async Task Insert_PersistUnidadeECriaHistoricoDeIdentificadores()
    {
        Unidade unidade = NovaUnidade(new IdentificadoresDaUnidade("insert-basico", "CEPS-INS", "INS001", null));
        await _fixture.GravarAsync(AdminA, unidade);

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        Unidade persistida = await readCtx.Unidades
            .Include(u => u.Historico)
            .SingleAsync(u => u.Id == unidade.Id);
        persistida.Historico.Should().HaveCount(3);
        persistida.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Slug && h.Valor == "insert-basico" && h.VigenciaFim == null);
        persistida.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Sigla && h.Valor == "CEPS-INS" && h.VigenciaFim == null);
        persistida.Historico.Should().Contain(h =>
            h.TipoIdentificador == TipoIdentificador.Codigo && h.Valor == "INS001" && h.VigenciaFim == null);
    }

    [Fact(DisplayName = "Update de identificador grava o valor novo e o autor da alteração")]
    public async Task Update_Identificador_GravaValorNovoEAutor()
    {
        Unidade unidade = NovaUnidade(new IdentificadoresDaUnidade("alias-update", "AUPD", "AUP001", "Alias original"));
        await _fixture.GravarAsync(AdminA, unidade);

        await TrocarAliasAsync(unidade.Id, DataInicio.AddDays(10));

        Unidade persistida = await _fixture.RecarregarAsync<Unidade>(unidade.Id);
        persistida.Alias.Should().Be("Alias atualizado");
        persistida.UpdatedBy.Should().Be(AdminB);
    }

    [Fact(DisplayName = "Update de identificador fecha histórico atual e insere nova entrada")]
    public async Task Update_Identificador_InsereNovaEntradaHistorico()
    {
        Unidade unidade = NovaUnidade(new IdentificadoresDaUnidade("hist-update", "HUPD", "HUP001", "Alias original"));
        await _fixture.GravarAsync(AdminA, unidade);
        DateOnly dataMudanca = DataInicio.AddDays(10);

        await TrocarAliasAsync(unidade.Id, dataMudanca);

        List<UnidadeIdentificadorHistorico> historicoAlias = await HistoricoDoAliasAsync(unidade.Id);

        historicoAlias.Should().HaveCount(2);
        historicoAlias[0].Valor.Should().Be("Alias original");
        historicoAlias[0].VigenciaFim.Should().Be(dataMudanca);
        historicoAlias[1].Valor.Should().Be("Alias atualizado");
        historicoAlias[1].VigenciaFim.Should().BeNull();
        historicoAlias[1].MotivoMudanca.Should().Be("ajuste de alias");
    }

    [Fact(DisplayName = "AuditableInterceptor preenche created_by com o UserId do contexto")]
    public async Task Insert_PreencheCreatedByViaAuditableInterceptor()
    {
        Unidade unidade = NovaUnidade(new IdentificadoresDaUnidade("audit-trail", "AUDIT", "AUD001", null));
        await _fixture.GravarAsync(AdminA, unidade);

        Unidade persistida = await _fixture.RecarregarAsync<Unidade>(unidade.Id);
        persistida.CreatedBy.Should().Be(AdminA);
        persistida.UpdatedBy.Should().BeNull("apenas criação — sem update ainda");
    }

    [Fact(DisplayName = "UNIQUE parcial (slug) rejeita segunda Unidade ativa com mesmo slug")]
    public async Task UniquePartial_Slug_RejeitaDuplicataAtiva()
    {
        Unidade primeira = NovaUnidade(new IdentificadoresDaUnidade("slug-unico", "SU01", "SUC001", null));
        await _fixture.GravarAsync(AdminA, primeira);

        Unidade segunda = NovaUnidade(new IdentificadoresDaUnidade("slug-unico", "SU02", "SUC002", null));
        await ConferirQueADuplicataEhRecusadaAsync(segunda, "ix_unidades_slug_vivo");
    }

    [Fact(DisplayName = "UNIQUE parcial (sigla) rejeita segunda Unidade ativa com mesma sigla")]
    public async Task UniquePartial_Sigla_RejeitaDuplicataAtiva()
    {
        Unidade primeira = NovaUnidade(new IdentificadoresDaUnidade("sigla-unica-a", "SIGDUP", "SGDUP001", null));
        await _fixture.GravarAsync(AdminA, primeira);

        Unidade segunda = NovaUnidade(new IdentificadoresDaUnidade("sigla-unica-b", "SIGDUP", "SGDUP002", null));
        await ConferirQueADuplicataEhRecusadaAsync(segunda, "ix_unidades_sigla_vivo");
    }

    [Fact(DisplayName = "Soft-delete liberta o slot da UNIQUE parcial — novo slug é aceito após exclusão")]
    public async Task SoftDelete_LibertaUniquePartialSlug()
    {
        Unidade unidade = NovaUnidade(new IdentificadoresDaUnidade("slug-reciclado", "RCSL", "REC001", null));
        await _fixture.GravarAsync(AdminA, unidade);

        await _fixture.RemoverAsync<Unidade>(AdminA, unidade.Id);

        Unidade excluida = await _fixture.RecarregarExcluidaAsync<Unidade>(unidade.Id);
        excluida.IsDeleted.Should().BeTrue();

        // Nova Unidade com mesmo slug é aceita — UNIQUE parcial WHERE is_deleted = false.
        await _fixture.ConferirQueAGravacaoEhAceitaAsync(
            AdminA,
            NovaUnidade(new IdentificadoresDaUnidade("slug-reciclado", "RCSL2", "REC002", null)),
            "slot liberado pelo soft-delete");
    }

    [Fact(DisplayName = "SoftDeleteInterceptor preenche deleted_by e deleted_at")]
    public async Task SoftDelete_PreencheDeletedByEDeletedAt()
    {
        Unidade unidade = NovaUnidade(new IdentificadoresDaUnidade("soft-del-audit", "SDA", "SDA001", null));
        await _fixture.GravarAsync(AdminA, unidade);

        await _fixture.RemoverAsync<Unidade>(AdminB, unidade.Id);

        Unidade excluida = await _fixture.RecarregarExcluidaAsync<Unidade>(unidade.Id);
        excluida.IsDeleted.Should().BeTrue();
        excluida.DeletedAt.Should().NotBeNull();
        excluida.DeletedBy.Should().Be(AdminB);
    }

    [Fact(DisplayName = "FK unidade_superior_id aceita hierarquia válida pai → filho")]
    public async Task Hierarquia_FkAceitaPaiFilhoValido()
    {
        Unidade pai = NovaUnidade(new IdentificadoresDaUnidade("reitoria", "REIT", "REIT001", null));
        await _fixture.GravarAsync(AdminA, pai);

        Unidade filho = NovaUnidade(new IdentificadoresDaUnidade("proeg", "PROEG", "PROEG001", null), superiorId: pai.Id);
        await _fixture.GravarAsync(AdminA, filho);

        Unidade persistido = await _fixture.RecarregarAsync<Unidade>(filho.Id);
        persistido.UnidadeSuperiorId.Should().Be(pai.Id);
    }

    [Fact(DisplayName = "FK unidade_superior_id com RESTRICT bloqueia DELETE físico do pai quando há filhos")]
    public async Task Hierarquia_FkRestrictBloqueiaDeleteFisicoDoSuperior()
    {
        // O SoftDeleteInterceptor converte EntityState.Deleted → UPDATE is_deleted=true,
        // portanto não emite DELETE SQL e não aciona o RESTRICT. Para testar a constraint
        // de banco em si, emitimos DELETE físico via SQL bruto.
        Unidade pai = await GravarPaiComFilhoAsync();

        // Tenta DELETE físico do pai com filho ativo — deve violar FK RESTRICT.
        await _fixture.ConferirQueOBancoRestringeAExclusaoAsync(
            $"DELETE FROM organizacao.unidades WHERE id = {pai.Id}",
            "fk_unidades_unidades_unidade_superior_id",
            "FK unidade_superior_id com RESTRICT deve impedir DELETE físico de unidade pai que possui filhos referenciando-a");
    }

    [Fact(DisplayName = "EhDescendenteAsync percorre a cadeia de superiores e ignora não-relacionados")]
    public async Task EhDescendenteAsync_PercorreAncestraisContraPostgresReal()
    {
        (Unidade a, Unidade b, Unidade c, Unidade d) = await HierarquiaABCComDSoltaAsync();
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repository = new UnidadeRepository(ctx);

        // C é descendente de A (A→B→C): tornar C superior de A formaria ciclo.
        (await repository.EhDescendenteAsync(c.Id, a.Id, CancellationToken.None)).Should().BeTrue();
        // B é descendente direto de A.
        (await repository.EhDescendenteAsync(b.Id, a.Id, CancellationToken.None)).Should().BeTrue();
        // A não é descendente de C: reapontar A para sob C é válido (sem ciclo).
        (await repository.EhDescendenteAsync(a.Id, c.Id, CancellationToken.None)).Should().BeFalse();
        // Igualdade conta como descendente — barra a auto-referência.
        (await repository.EhDescendenteAsync(a.Id, a.Id, CancellationToken.None)).Should().BeTrue();
        // D não tem vínculo com A.
        (await repository.EhDescendenteAsync(d.Id, a.Id, CancellationToken.None)).Should().BeFalse();
    }

    [Fact(DisplayName = "Checagens de unicidade normalizam (Trim) Sigla e Codigo do argumento")]
    public async Task ChecagensDeUnicidade_NormalizamArgumentoComEspacos()
    {
        Unidade existente = NovaUnidade(new IdentificadoresDaUnidade("trim-check", "TRIMSIG", "TRIMCOD", null));
        await _fixture.GravarAsync(AdminA, existente);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repository = new UnidadeRepository(ctx);

        // " trimsig " deve casar com "TRIMSIG" persistido (Trim + ToUpperInvariant).
        (await repository.SiglaExisteEntreVivosAsync(" trimsig ", null, CancellationToken.None)).Should().BeTrue();
        // " TRIMCOD " deve casar com "TRIMCOD" persistido (Trim).
        (await repository.CodigoExisteEntreVivosAsync(" TRIMCOD ", null, CancellationToken.None)).Should().BeTrue();
    }

    /// <remarks>
    /// Regressão: ObterPorIdAsync faz Include(Historico);
    /// com a FK em Cascade, Remover(unidade) marcava os históricos carregados como
    /// Deleted e — como o SoftDeleteInterceptor só converte ISoftDeletable — eles
    /// sofriam hard-delete físico, destruindo a trilha append-only. A FK Restrict
    /// impede o cascade em memória: a Unidade vira soft-delete e o histórico sobrevive.
    /// </remarks>
    [Fact(DisplayName = "Soft-delete de Unidade preserva o histórico de identificadores (FK não cascateia)")]
    public async Task SoftDelete_PreservaHistoricoDeIdentificadores()
    {
        Unidade unidade = NovaUnidade(new IdentificadoresDaUnidade("hist-preserva", "HPRES", "HPR001", null));
        await _fixture.GravarAsync(AdminA, unidade);

        await RemoverPeloCaminhoDoHandlerAsync(unidade.Id);

        // Unidade soft-deletada (UPDATE is_deleted=true), não removida fisicamente.
        Unidade excluida = await _fixture.RecarregarExcluidaAsync<Unidade>(unidade.Id);
        excluida.IsDeleted.Should().BeTrue();

        // Histórico append-only PRESERVADO — as 3 linhas seguem no banco.
        int historicoCount = await LinhasDeHistoricoNoBancoAsync(unidade.Id);
        historicoCount.Should().Be(3,
            "o histórico de identificadores não pode ser hard-deletado no soft-delete da Unidade");
    }

    // ── Cidade ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "Insert persiste e recarrega a cidade completa da Unidade")]
    public async Task Insert_ComCidadeCompleta_PersisteERecarrega()
    {
        Unidade unidade = NovaUnidade(
            new IdentificadoresDaUnidade("cidade-completa", "CIDCOMP", "CID001", null),
            cidade: Maraba);
        await _fixture.GravarAsync(AdminA, unidade);

        Unidade persistida = await _fixture.RecarregarAsync<Unidade>(unidade.Id);
        persistida.CidadeCodigoIbge.Should().Be("1504208");
        persistida.CidadeNome.Should().Be("Marabá");
        persistida.CidadeUf.Should().Be("PA");
    }

    [Fact(DisplayName = "CHECK ck_unidades_cidade_completa rejeita UPDATE cru que deixa o trio de cidade parcial")]
    public async Task CheckCidadeCompleta_RejeitaTrioParcial()
    {
        Unidade unidade = NovaUnidade(
            new IdentificadoresDaUnidade("cidade-trio", "CIDTRIO", "CID002", null),
            cidade: Maraba);
        await _fixture.GravarAsync(AdminA, unidade);

        // Zera só o nome da cidade, deixando código/UF — trio parcial. O domínio trata a
        // cidade como all-or-nothing; o CHECK protege a escrita crua (mesmo padrão de
        // InstituicaoPersistenceTests.CheckCidadeCompleta_RejeitaTrioParcial).
        await _fixture.ConferirQueOBancoRecusaPorCheckAsync(
            $"UPDATE organizacao.unidades SET cidade_nome = NULL WHERE id = {unidade.Id}",
            "ck_unidades_cidade_completa",
            "o CHECK ck_unidades_cidade_completa exige o trio de cidade completo ou ausente");
    }

    // ── Apoio ────────────────────────────────────────────────────────────

    /// <summary>
    /// Troca só o alias, como AdminB, carregando pelo repositório num contexto próprio;
    /// confere que o agregado aceita e que o EF insere a nova entrada de histórico.
    /// </summary>
    private async Task TrocarAliasAsync(Guid unidadeId, DateOnly dataMudanca)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminB);
        var repository = new UnidadeRepository(ctx);
        Unidade tracked = (await repository.ObterPorIdAsync(unidadeId, CancellationToken.None))!;

        Result atualizarResult = AtualizarSoOAlias(tracked, dataMudanca);
        atualizarResult.IsSuccess.Should().BeTrue();

        Func<Task> act = async () => await ctx.SaveChangesAsync();

        await act.Should().NotThrowAsync(
            "a nova entrada de histórico deve ser inserida, não tratada como update de linha existente");
    }

    private static Result AtualizarSoOAlias(Unidade unidade, DateOnly dataMudanca) =>
        unidade.Atualizar(
            unidade.Nome,
            new MudancaDeIdentificadores(
                new IdentificadoresDaUnidade(unidade.Slug.Valor, unidade.Sigla, unidade.Codigo, "Alias atualizado"),
                dataMudanca,
                "ajuste de alias"),
            CidadeDaUnidade.Nenhuma,
            unidade.VigenciaFim,
            unidade.UnidadeSuperiorId,
            unidade.Tipo,
            unidade.UnidadeAcademica);

    private async Task<List<UnidadeIdentificadorHistorico>> HistoricoDoAliasAsync(Guid unidadeId)
    {
        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        return await readCtx.UnidadesIdentificadoresHistorico
            .Where(h => h.UnidadeId == unidadeId && h.TipoIdentificador == TipoIdentificador.Alias)
            .OrderBy(h => h.VigenciaInicio)
            .ToListAsync();
    }

    private async Task<int> LinhasDeHistoricoNoBancoAsync(Guid unidadeId)
    {
        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        return await readCtx.UnidadesIdentificadoresHistorico
            .CountAsync(h => h.UnidadeId == unidadeId);
    }

    /// <summary>
    /// Caminho EXATO do RemoverUnidadeCommandHandler: ObterPorIdAsync (Include
    /// Historico, rastreado) seguido de Remover, no mesmo contexto.
    /// </summary>
    private async Task RemoverPeloCaminhoDoHandlerAsync(Guid unidadeId)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        var repository = new UnidadeRepository(ctx);
        Unidade tracked = (await repository.ObterPorIdAsync(unidadeId, CancellationToken.None))!;
        tracked.Historico.Should().HaveCount(3,
            "a criação abriu 3 entradas (Slug, Sigla, Codigo), carregadas e rastreadas");

        repository.Remover(tracked);
        await ctx.SaveChangesAsync();
    }

    private async Task<Unidade> GravarPaiComFilhoAsync()
    {
        Unidade pai = NovaUnidade(new IdentificadoresDaUnidade("pai-restrict", "PRST", "PRS001", null));
        Unidade filho = NovaUnidade(new IdentificadoresDaUnidade("filho-restrict", "FRST", "FRS001", null), pai.Id);
        await _fixture.GravarAsync(AdminA, pai, filho);
        return pai;
    }

    private async Task ConferirQueADuplicataEhRecusadaAsync(Unidade duplicata, string indiceEsperado)
    {
        await using UniPlusDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.Unidades.Add(duplicata);

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        Npgsql.PostgresException violacao = (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<Npgsql.PostgresException>().Which;
        violacao.SqlState.Should().Be(Npgsql.PostgresErrorCodes.UniqueViolation);
        violacao.ConstraintName.Should().Be(indiceEsperado);
    }

    /// <summary>
    /// Hierarquia A → B → C (A superior de B, B superior de C) + D solta, gravada
    /// em saves sequenciais que respeitam a FK auto-referencial (pai antes do filho).
    /// </summary>
    private async Task<(Unidade A, Unidade B, Unidade C, Unidade D)> HierarquiaABCComDSoltaAsync()
    {
        Unidade a = NovaUnidade(new IdentificadoresDaUnidade("hier-a", "HIERA", "HRA001", null));
        Unidade b = NovaUnidade(new IdentificadoresDaUnidade("hier-b", "HIERB", "HRB001", null), a.Id);
        Unidade c = NovaUnidade(new IdentificadoresDaUnidade("hier-c", "HIERC", "HRC001", null), b.Id);
        Unidade d = NovaUnidade(new IdentificadoresDaUnidade("hier-d", "HIERD", "HRD001", null));

        await _fixture.GravarAsync(AdminA, a, b, c, d);
        return (a, b, c, d);
    }

    private static Unidade NovaUnidade(
        IdentificadoresDaUnidade identificadores,
        Guid? superiorId = null,
        CidadeDaUnidade cidade = default) =>
        Unidade.Criar(
            $"Unidade {identificadores.Sigla}",
            identificadores,
            cidade,
            new VigenciaDaUnidade(DataInicio, null),
            superiorId,
            TipoUnidade.Centro,
            false).Value!;
}
