namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.PesosAreaEnem;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Integração ponta-a-ponta de Pesos por Área contra Postgres real: as cinco áreas na
/// tabela filha, UNIQUE parcial do par (resolução, grupo), liberação do slot por
/// soft-delete sem perder as áreas, atualização no lugar, CHECKs de domínio e de faixa, e
/// leitura cross-módulo na ordem canônica.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class PesoAreaEnemPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";
    private const string BaseLegal = "Res. 805/2024 Anexo I";

    private readonly ConfiguracaoDbFixture _fixture;

    public PesoAreaEnemPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Criar persiste os pesos e fica visível pelo leitor cross-módulo")]
    public async Task Insert_PersisteEFicaVisivelPeloReader()
    {
        string resolucao = ResolucaoUnica();
        PesoAreaEnem peso = Nova(resolucao);

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PesosAreaEnem.Add(peso);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        PesoAreaEnem persistida = await readCtx.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);

        persistida.Resolucao.Should().Be(resolucao);
        persistida.GrupoCurso.Codigo.Should().Be(GrupoCurso.Tecnologica);
        persistida.CreatedBy.Should().Be(AdminA);
        persistida.IsDeleted.Should().BeFalse();
        persistida.AreasDaLinha.Select(a => (a.Codigo, a.Rotulo, a.Peso, a.Corte)).Should().Equal(
            ("REDACAO", "Redação", 2.00m, (decimal?)400.000m),
            ("CIENCIAS_DA_NATUREZA", "Ciências da Natureza e suas Tecnologias", 1.50m, (decimal?)null),
            ("CIENCIAS_HUMANAS", "Ciências Humanas e suas Tecnologias", 2.50m, (decimal?)null),
            ("LINGUAGENS", "Linguagens e suas Tecnologias", 2.50m, (decimal?)null),
            ("MATEMATICA", "Matemática e suas Tecnologias", 1.50m, (decimal?)null));
        (await ContarAreasAsync(readCtx, peso.Id)).Should().Be(5);
        (await GrupoPersistidoAsync(readCtx, peso.Id)).Should().Be(("TECNOLOGICA", "Tecnológica"),
            "a linha grava o código do grupo e, ao lado, o rótulo posto pelo sistema");

        var reader = new PesoAreaEnemReader(readCtx);
        PesoAreaEnemView? view = await reader.ObterPorIdAsync(peso.Id);
        view.Should().NotBeNull();
        view!.Resolucao.Should().Be(resolucao);
        view.GrupoCurso.Should().Be(new GrupoAreaEnemView(GrupoCurso.Tecnologica, "Tecnológica"));
        view.Areas.Select(a => (a.Codigo, a.Rotulo, a.Peso, a.Corte)).Should().Equal(
            persistida.AreasDaLinha.Select(a => (a.Codigo, a.Rotulo, a.Peso, a.Corte)));
    }

    [Fact(DisplayName = "Atualizar muda os valores no lugar: continuam cinco linhas filhas, com os valores novos")]
    public async Task Atualizar_MudaNoLugar()
    {
        PesoAreaEnem peso = Nova(ResolucaoUnica());
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PesosAreaEnem.Add(peso);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            PesoAreaEnem tracked = await ctx.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
            List<AreaInformada> novas = Areas();
            novas[0] = new(PesoAreaEnem.CodigoRedacao, 3.00m, null);
            novas[4] = new(PesoAreaEnem.CodigoMatematica, 4.25m, null);
            tracked.Atualizar(novas, "Nova base").IsSuccess.Should().BeTrue();
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        PesoAreaEnem persistida = await readCtx.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
        persistida.AreasDaLinha[0].Peso.Should().Be(3.00m);
        persistida.AreasDaLinha[0].Corte.Should().BeNull();
        persistida.AreasDaLinha[4].Peso.Should().Be(4.25m);
        persistida.BaseLegal.Should().Be("Nova base");
        persistida.UpdatedBy.Should().Be(AdminB);
        (await ContarAreasAsync(readCtx, peso.Id)).Should().Be(5);
    }

    [Fact(DisplayName = "Atualizar só o peso de uma área, com a mesma base legal, carimba UpdatedAt/UpdatedBy da linha de pesos")]
    public async Task Atualizar_SoAsAreas_CarimbaAuditoriaDaLinha()
    {
        PesoAreaEnem peso = Nova(ResolucaoUnica());
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PesosAreaEnem.Add(peso);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            var repository = new PesoAreaEnemRepository(ctx);
            PesoAreaEnem tracked = (await repository.ObterPorIdAsync(peso.Id, CancellationToken.None))!;
            List<AreaInformada> novas = Areas();
            novas[4] = new(PesoAreaEnem.CodigoMatematica, 4.25m, null);
            tracked.Atualizar(novas, BaseLegal).IsSuccess.Should().BeTrue();
            repository.RegistrarAtualizacao(tracked);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        PesoAreaEnem persistida = await readCtx.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
        persistida.AreasDaLinha[4].Peso.Should().Be(4.25m);
        persistida.BaseLegal.Should().Be(BaseLegal);
        persistida.UpdatedBy.Should().Be(AdminB, "mudar só o peso de uma área também é edição da linha de pesos");
        persistida.UpdatedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "Atualizar regrava o rótulo da área a partir do domínio")]
    public async Task Atualizar_RegravaORotulo()
    {
        PesoAreaEnem peso = Nova(ResolucaoUnica());
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PesosAreaEnem.Add(peso);
            await ctx.SaveChangesAsync();
            await ctx.Database.ExecuteSqlAsync(
                $"UPDATE configuracao.peso_area_enem_area SET rotulo = {"Rótulo antigo"} WHERE peso_area_enem_id = {peso.Id} AND codigo = {PesoAreaEnem.CodigoLinguagens}");
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            PesoAreaEnem tracked = await ctx.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
            tracked.AreasDaLinha[3].Rotulo.Should().Be("Rótulo antigo");
            tracked.Atualizar(Areas(), BaseLegal).IsSuccess.Should().BeTrue();
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        PesoAreaEnem persistida = await readCtx.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
        persistida.AreasDaLinha[3].Rotulo.Should().Be("Linguagens e suas Tecnologias");
    }

    [Fact(DisplayName = "Atualizar regrava o rótulo do grupo a partir do domínio")]
    public async Task Atualizar_RegravaORotuloDoGrupo()
    {
        PesoAreaEnem peso = Nova(ResolucaoUnica());
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PesosAreaEnem.Add(peso);
            await ctx.SaveChangesAsync();
            await ctx.Database.ExecuteSqlAsync(
                $"UPDATE configuracao.peso_area_enem SET grupo_curso_rotulo = {"Rótulo antigo"} WHERE id = {peso.Id}");
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            PesoAreaEnem tracked = await ctx.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
            tracked.Atualizar(Areas(), BaseLegal).IsSuccess.Should().BeTrue();
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        (await GrupoPersistidoAsync(readCtx, peso.Id)).Should().Be(("TECNOLOGICA", "Tecnológica"),
            "o rótulo do grupo é regravado a partir do código na edição, como o das áreas");
    }

    [Fact(DisplayName = "Edição só dos pesos concorrente com remoção lógica não desfaz a remoção")]
    public async Task AtualizarConcorrenteComRemocao_NaoRessuscitaALinha()
    {
        PesoAreaEnem peso = Nova(ResolucaoUnica());
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PesosAreaEnem.Add(peso);
            await ctx.SaveChangesAsync();
        }

        // A edição carrega a linha viva; a remoção lógica é confirmada antes de a edição salvar.
        await using ConfiguracaoDbContext ctxEdicao = _fixture.CreateDbContext(AdminA);
        PesoAreaEnem emEdicao = await ctxEdicao.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);

        await using (ConfiguracaoDbContext ctxRemocao = _fixture.CreateDbContext(AdminB))
        {
            PesoAreaEnem aRemover = await ctxRemocao.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
            ctxRemocao.PesosAreaEnem.Remove(aRemover);
            await ctxRemocao.SaveChangesAsync();
        }

        List<AreaInformada> novas = Areas();
        novas[4] = new(PesoAreaEnem.CodigoMatematica, 4.25m, null);
        emEdicao.Atualizar(novas, BaseLegal).IsSuccess.Should().BeTrue();
        new PesoAreaEnemRepository(ctxEdicao).RegistrarAtualizacao(emEdicao);
        await ctxEdicao.SaveChangesAsync();

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        PesoAreaEnem final = await readCtx.PesosAreaEnem.IgnoreQueryFilters().SingleAsync(p => p.Id == peso.Id);
        final.IsDeleted.Should().BeTrue("a edição regrava só as colunas de auditoria, não as de remoção lógica");
        final.DeletedBy.Should().Be(AdminB);
        final.UpdatedBy.Should().Be(AdminA);
    }

    [Fact(DisplayName = "Edição idêntica ao estado gravado não carimba a auditoria")]
    public async Task AtualizarSemMudanca_NaoCarimbaAuditoria()
    {
        PesoAreaEnem peso = Nova(ResolucaoUnica());
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PesosAreaEnem.Add(peso);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            PesoAreaEnem tracked = await ctx.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
            tracked.Atualizar(Areas(), BaseLegal).IsSuccess.Should().BeTrue();
            new PesoAreaEnemRepository(ctx).RegistrarAtualizacao(tracked);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        PesoAreaEnem persistida = await readCtx.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
        persistida.UpdatedBy.Should().BeNull("nada mudou, então não houve edição a registrar");
        persistida.UpdatedAt.Should().BeNull();
    }

    [Fact(DisplayName = "UNIQUE parcial (resolução, grupo) rejeita segundo par vivo idêntico")]
    public async Task UniquePartial_Par_RejeitaDuplicataAtiva()
    {
        string resolucao = ResolucaoUnica();
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PesosAreaEnem.Add(Nova(resolucao));
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.PesosAreaEnem.Add(Nova(resolucao));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        // Trava as constantes que o handler usa para traduzir a corrida concorrente
        // (UniqueConstraintViolation.GetViolatedConstraint/IsParConflict) em
        // ParJaExiste/409: SqlState 23505 + nome do índice único parcial.
        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be("ix_peso_area_enem_resolucao_grupo_vivo");
    }

    [Fact(DisplayName = "Mesma resolução em grupo distinto é aceita (par distinto)")]
    public async Task ParDistinto_MesmaResolucaoOutroGrupo_Aceita()
    {
        string resolucao = ResolucaoUnica();
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.PesosAreaEnem.Add(Nova(resolucao, GrupoCurso.Tecnologica));
        ctx.PesosAreaEnem.Add(Nova(resolucao, GrupoCurso.HumanisticaI));

        Func<Task> act = async () => await ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync("o par (resolução, grupo) é distinto");
    }

    [Fact(DisplayName = "Soft-delete preserva a trilha e liberta o slot da UNIQUE parcial do par")]
    public async Task SoftDelete_PreservaTrilhaELibertaSlot()
    {
        string resolucao = ResolucaoUnica();
        PesoAreaEnem peso = Nova(resolucao);
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PesosAreaEnem.Add(peso);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            PesoAreaEnem tracked = await ctx.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
            ctx.PesosAreaEnem.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            PesoAreaEnem excluida = await ctx.PesosAreaEnem
                .IgnoreQueryFilters().SingleAsync(p => p.Id == peso.Id);
            excluida.IsDeleted.Should().BeTrue();
            excluida.DeletedBy.Should().Be(AdminB);
            excluida.AreasDaLinha.Should().HaveCount(5, "o soft-delete preserva as áreas junto com a linha");
            (await ContarAreasAsync(ctx, peso.Id)).Should().Be(5);
        }

        await using ConfiguracaoDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.PesosAreaEnem.Add(Nova(resolucao));

        Func<Task> act = async () => await ctx3.SaveChangesAsync();
        await act.Should().NotThrowAsync("o slot do par foi liberado pelo soft-delete");
    }

    [Theory(DisplayName = "CHECK de banco rejeita grupo fora dos quatro códigos via SQL cru, inclusive o rótulo")]
    [InlineData("Engenharias")]
    [InlineData("Tecnológica")]
    public async Task Check_RejeitaGrupoForaDoDominioViaSqlCru(string grupo)
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.peso_area_enem (id, resolucao, grupo_curso, grupo_curso_rotulo, base_legal, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {ResolucaoUnica()}, {grupo}, {"Rótulo"}, {BaseLegal}, {DateTimeOffset.UtcNow}, {false})");

        Npgsql.PostgresException pg = (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        pg.ConstraintName.Should().Be("ck_peso_area_enem_grupo_curso");
    }

    [Theory(DisplayName = "CHECKs da tabela de áreas rejeitam código fora das cinco, peso negativo e corte fora da faixa via SQL cru")]
    [InlineData("FISICA", 1.0, null, "ck_peso_area_enem_area_codigo")]
    [InlineData("REDACAO", -1.0, null, "ck_peso_area_enem_area_peso")]
    [InlineData("REDACAO", 1.0, 1000.001, "ck_peso_area_enem_area_corte")]
    [InlineData("REDACAO", 1.0, -0.5, "ck_peso_area_enem_area_corte")]
    public async Task Check_TabelaDeAreas_RejeitaForaDaFaixa(string codigo, double peso, double? corte, string constraint)
    {
        Guid paiId = await CriarPaiSemAreasAsync();
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        decimal? corteDecimal = corte is null ? null : (decimal)corte.Value;

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.peso_area_enem_area (peso_area_enem_id, codigo, rotulo, peso, corte) VALUES ({paiId}, {codigo}, {"Rótulo"}, {(decimal)peso}, {corteDecimal})");

        Npgsql.PostgresException pg = (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        pg.ConstraintName.Should().Be(constraint);
    }

    [Fact(DisplayName = "A chave (linha, código) rejeita a mesma área duas vezes na mesma linha via SQL cru")]
    public async Task Pk_RejeitaAreaRepetidaNaLinha()
    {
        Guid paiId = await CriarPaiSemAreasAsync();
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.peso_area_enem_area (peso_area_enem_id, codigo, rotulo, peso) VALUES ({paiId}, {"REDACAO"}, {"Redação"}, {1.0m})");

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.peso_area_enem_area (peso_area_enem_id, codigo, rotulo, peso) VALUES ({paiId}, {"REDACAO"}, {"Redação"}, {2.0m})");

        Npgsql.PostgresException pg = (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        pg.SqlState.Should().Be("23505");
    }

    [Theory(DisplayName = "Corte da Redação nos limites e sem corte persistem como gravados")]
    [InlineData(null)]
    [InlineData(0.0)]
    [InlineData(1000.0)]
    public async Task CorteDaRedacao_PersisteComoGravado(double? corte)
    {
        decimal? esperado = corte is null ? null : (decimal)corte.Value;
        List<AreaInformada> areas = Areas();
        areas[0] = new(PesoAreaEnem.CodigoRedacao, 2.00m, esperado);
        PesoAreaEnem peso = PesoAreaEnem.Criar(ResolucaoUnica(), GrupoCurso.Tecnologica, areas, BaseLegal).Value!;

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PesosAreaEnem.Add(peso);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        PesoAreaEnem persistida = await readCtx.PesosAreaEnem.SingleAsync(p => p.Id == peso.Id);
        persistida.AreasDaLinha[0].Corte.Should().Be(esperado);
    }

    [Fact(DisplayName = "Reader.ListarVivasAsync ordena por resolução e exclui soft-deleted")]
    public async Task ListarVivas_OrdenaPorResolucaoEExcluiSoftDeleted()
    {
        // Prefixo único por execução: o banco é compartilhado na collection, então
        // filtramos o resultado às linhas deste teste para asserções determinísticas.
        string prefixo = $"Res. {Guid.NewGuid().ToString("N")[..12]}";
        string resA = $"{prefixo}-a";
        string resB = $"{prefixo}-b";
        string resExcluida = $"{prefixo}-d";

        // Insere fora de ordem (B antes de A) para provar a ordenação do reader.
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PesosAreaEnem.Add(Nova(resB));
            ctx.PesosAreaEnem.Add(Nova(resA));
            ctx.PesosAreaEnem.Add(Nova(resExcluida));
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            PesoAreaEnem aExcluir = await ctx.PesosAreaEnem.SingleAsync(p => p.Resolucao == resExcluida);
            ctx.PesosAreaEnem.Remove(aExcluir);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new PesoAreaEnemReader(readCtx);
        IReadOnlyList<PesoAreaEnemView> todas = await reader.ListarVivasAsync();

        string[] meus = [.. todas
            .Select(v => v.Resolucao)
            .Where(r => r.StartsWith(prefixo, StringComparison.Ordinal))];

        // O reader ordena por Resolucao ascendente e exclui o soft-deleted:
        // exatamente [resA, resB], nessa ordem (inserimos B antes de A).
        meus.Should().Equal([resA, resB]);
    }

    [Fact(DisplayName = "Leitura por resolução completa devolve as quatro linhas ordenadas pelo código do grupo, sem grupo ausente")]
    public async Task ObterPorResolucao_Completa_DevolveLinhasOrdenadasPorCodigo()
    {
        string resolucao = ResolucaoUnica();
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            // Inseridos fora da ordem do código para a ordenação não sair de graça.
            ctx.PesosAreaEnem.AddRange(
                Nova(resolucao, GrupoCurso.Tecnologica),
                Nova(resolucao, GrupoCurso.SaudeEBiologicas),
                Nova(resolucao, GrupoCurso.HumanisticaII),
                Nova(resolucao, GrupoCurso.HumanisticaI));
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        ResolucaoPesoAreaEnemView? view = await new PesoAreaEnemReader(readCtx).ObterPorResolucaoAsync($"  {resolucao} ");

        view.Should().NotBeNull();
        view!.Resolucao.Should().Be(resolucao);
        view.Completa.Should().BeTrue();
        view.GruposAusentes.Should().BeEmpty();
        view.Linhas.Select(l => l.GrupoCurso.Codigo).Should().Equal(
            GrupoCurso.HumanisticaI, GrupoCurso.HumanisticaII, GrupoCurso.SaudeEBiologicas, GrupoCurso.Tecnologica);
        view.Linhas.Should().OnlyContain(l => l.Resolucao == resolucao && l.Areas.Count == 5 && l.BaseLegal == BaseLegal);
    }

    [Fact(DisplayName = "Leitura por resolução aponta como ausente o grupo sem linha viva, inclusive o removido logicamente")]
    public async Task ObterPorResolucao_Incompleta_ApontaGruposAusentes()
    {
        string resolucao = ResolucaoUnica();
        PesoAreaEnem removida = Nova(resolucao, GrupoCurso.HumanisticaII);
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PesosAreaEnem.AddRange(
                Nova(resolucao, GrupoCurso.Tecnologica),
                Nova(resolucao, GrupoCurso.HumanisticaI),
                removida);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            PesoAreaEnem aExcluir = await ctx.PesosAreaEnem.SingleAsync(p => p.Id == removida.Id);
            ctx.PesosAreaEnem.Remove(aExcluir);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        ResolucaoPesoAreaEnemView? view = await new PesoAreaEnemReader(readCtx).ObterPorResolucaoAsync(resolucao);

        view.Should().NotBeNull();
        view!.Completa.Should().BeFalse();
        view.Linhas.Select(l => l.GrupoCurso.Codigo).Should().Equal(GrupoCurso.HumanisticaI, GrupoCurso.Tecnologica);
        view.GruposAusentes.Should().Equal(
            new GrupoAreaEnemView(GrupoCurso.HumanisticaII, "Humanística II"),
            new GrupoAreaEnemView(GrupoCurso.SaudeEBiologicas, "Saúde e Biológicas"));
    }

    [Fact(DisplayName = "Leitura por resolução casa a entrada decomposta com a resolução gravada em NFC")]
    public async Task ObterPorResolucao_EntradaDecomposta_CasaComAGravadaEmNfc()
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8];
        string composta = $"Resolução {sufixo}".Normalize(System.Text.NormalizationForm.FormC);
        string decomposta = composta.Normalize(System.Text.NormalizationForm.FormD);
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.PesosAreaEnem.Add(Nova(decomposta));
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        ResolucaoPesoAreaEnemView? view = await new PesoAreaEnemReader(readCtx).ObterPorResolucaoAsync(decomposta);

        view.Should().NotBeNull("o cadastro grava em NFC e a busca normaliza a entrada do mesmo jeito");
        view!.Resolucao.Should().Be(composta);
        view.Linhas.Should().ContainSingle().Which.Resolucao.Should().Be(composta);
    }

    [Theory(DisplayName = "Leitura por resolução com texto que não cabe na coluna devolve null, sem ir ao banco")]
    [InlineData("805\u0000")]
    [InlineData("Res.\n805")]
    [InlineData("Resolução com quarenta e um caracteres ..")]
    public async Task ObterPorResolucao_TextoInvalidoParaAColuna_DevolveNull(string resolucao)
    {
        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);

        ResolucaoPesoAreaEnemView? view = await new PesoAreaEnemReader(readCtx).ObterPorResolucaoAsync(resolucao);

        view.Should().BeNull("o Postgres recusaria o caractere nulo com erro de banco, e nenhuma linha gravada tem esse texto");
    }

    [Fact(DisplayName = "Leitura por resolução com não-caractere devolve null, sem exceção")]
    public async Task ObterPorResolucao_NaoCaractere_DevolveNull()
    {
        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);

        ResolucaoPesoAreaEnemView? view = await new PesoAreaEnemReader(readCtx).ObterPorResolucaoAsync("Res. 805" + (char)0xFFFE);

        view.Should().BeNull();
    }

    [Fact(DisplayName = "Leitura por resolução sem nenhuma linha viva devolve null")]
    public async Task ObterPorResolucao_Inexistente_DevolveNull()
    {
        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);

        ResolucaoPesoAreaEnemView? view = await new PesoAreaEnemReader(readCtx).ObterPorResolucaoAsync(ResolucaoUnica());

        view.Should().BeNull();
    }

    private static List<AreaInformada> Areas() =>
    [
        new(PesoAreaEnem.CodigoRedacao, 2.00m, 400m),
        new(PesoAreaEnem.CodigoCienciasDaNatureza, 1.50m, null),
        new(PesoAreaEnem.CodigoCienciasHumanas, 2.50m, null),
        new(PesoAreaEnem.CodigoLinguagens, 2.50m, null),
        new(PesoAreaEnem.CodigoMatematica, 1.50m, null),
    ];

    private static PesoAreaEnem Nova(string resolucao, string grupo = GrupoCurso.Tecnologica) =>
        PesoAreaEnem.Criar(resolucao, grupo, Areas(), BaseLegal).Value!;

    private static Task<int> ContarAreasAsync(ConfiguracaoDbContext ctx, Guid pesoId) =>
        ctx.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM configuracao.peso_area_enem_area WHERE peso_area_enem_id = {pesoId}")
            .SingleAsync();

    // Linha de pesos sem áreas, direto no banco: base para exercitar os CHECKs da tabela
    // filha sem a validação do agregado no caminho. Nasce removida logicamente — o banco é
    // compartilhado pela coleção, e uma linha viva incompleta apareceria para os testes
    // que listam ou editam os pesos vivos.
    private async Task<Guid> CriarPaiSemAreasAsync()
    {
        Guid id = Guid.CreateVersion7();
        DateTimeOffset agora = DateTimeOffset.UtcNow;
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.peso_area_enem (id, resolucao, grupo_curso, grupo_curso_rotulo, base_legal, created_at, is_deleted, deleted_at) VALUES ({id}, {ResolucaoUnica()}, {GrupoCurso.Tecnologica}, {"Tecnológica"}, {BaseLegal}, {agora}, {true}, {agora})");
        return id;
    }

    private static async Task<(string Codigo, string Rotulo)> GrupoPersistidoAsync(ConfiguracaoDbContext ctx, Guid pesoId)
    {
        GrupoGravado grupo = await ctx.Database
            .SqlQuery<GrupoGravado>($"SELECT grupo_curso AS codigo, grupo_curso_rotulo AS rotulo FROM configuracao.peso_area_enem WHERE id = {pesoId}")
            .SingleAsync();
        return (grupo.Codigo, grupo.Rotulo);
    }

    private sealed record GrupoGravado(string Codigo, string Rotulo);

    private static string ResolucaoUnica() => $"Res. {Guid.NewGuid().ToString("N")[..12]}";
}
