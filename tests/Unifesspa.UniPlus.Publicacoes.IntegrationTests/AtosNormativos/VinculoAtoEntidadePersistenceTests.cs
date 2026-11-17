namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Application.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Integração do vínculo ato ↔ entidade e da vaga de linhagem única por objeto contra
/// Postgres real: unicidade do trio, append-only imposto por trigger, e as duas travas
/// que impedem o <c>INSERT</c> cru de forjar uma vaga ou de omiti-la (ADR-0107).
/// </summary>
[Collection(PublicacoesDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class VinculoAtoEntidadePersistenceTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string RestrictViolation = "23001";
    private const string UniqueViolation = "23505";
    private const string ForeignKeyViolation = "23503";
    private const string TipoUnico = "EDITAL_ABERTURA";
    private static readonly AtoNormativoDeTeste Base = new() { Numero = null, TipoCodigo = TipoUnico, CongelaConfiguracao = true };

    private readonly PublicacoesDbFixture _fixture;

    public VinculoAtoEntidadePersistenceTests(PublicacoesDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O ato persiste com os seus vínculos, e o vínculo aponta para ele")]
    public async Task Insert_PersisteVinculos()
    {
        Guid processo = Guid.CreateVersion7();
        Guid chamada = Guid.CreateVersion7();
        AtoNormativo ato = (Base with { Vinculos = [new("PROCESSO_SELETIVO", processo), new("CHAMADA", chamada)] }).Registrar();

        await Gravar(ato);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        List<VinculoAtoEntidade> vinculos = await ctx.VinculosAtoEntidade
            .Where(v => v.AtoId == ato.Id)
            .ToListAsync();

        vinculos.Should().HaveCount(2);
        vinculos.Select(v => v.EntidadeId).Should().BeEquivalentTo([processo, chamada]);
    }

    [Fact(DisplayName = "Um ato sem vínculo persiste — há atos que não tratam de objeto algum")]
    public async Task Insert_SemVinculo_Persiste()
    {
        AtoNormativo ato = (Base with { UnicoPorObjeto = true }).Registrar();

        await Gravar(ato);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        (await ctx.AtosNormativos.AnyAsync(a => a.Id == ato.Id)).Should().BeTrue();
        (await ctx.VinculosAtoEntidade.AnyAsync(v => v.AtoId == ato.Id)).Should().BeFalse();
    }

    [Fact(DisplayName = "UPDATE cru no vínculo é bloqueado pelo trigger append-only")]
    public async Task Update_Vinculo_Bloqueado()
    {
        AtoNormativo ato = (Base with { Vinculos = [new("PROCESSO_SELETIVO", Guid.CreateVersion7())] }).Registrar();
        await Gravar(ato);

        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            $"UPDATE publicacoes.vinculos_do_ato_com_entidade SET entidade_id = {Guid.CreateVersion7()} WHERE ato_id = {ato.Id}"));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "DELETE cru no vínculo é bloqueado pelo trigger append-only")]
    public async Task Delete_Vinculo_Bloqueado()
    {
        AtoNormativo ato = (Base with { Vinculos = [new("PROCESSO_SELETIVO", Guid.CreateVersion7())] }).Registrar();
        await Gravar(ato);

        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            $"DELETE FROM publicacoes.vinculos_do_ato_com_entidade WHERE ato_id = {ato.Id}"));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "O mesmo objeto vinculado duas vezes ao mesmo ato colide no índice único do trio")]
    public async Task Insert_TrioRepetido_Colide()
    {
        Guid processo = Guid.CreateVersion7();
        AtoNormativo ato = (Base with { Vinculos = [new("PROCESSO_SELETIVO", processo)] }).Registrar();
        await Gravar(ato);

        // O agregado e o validator já recusam antes; aqui prova-se a trava do banco.
        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            $"""
            INSERT INTO publicacoes.vinculos_do_ato_com_entidade (id, ato_id, entidade_tipo, entidade_id)
            VALUES ({Guid.CreateVersion7()}, {ato.Id}, 'PROCESSO_SELETIVO', {processo})
            """));

        erro.SqlState.Should().Be(UniqueViolation);
    }

    [Fact(DisplayName = "Vínculo órfão — sem ato — é recusado pela chave estrangeira")]
    public async Task Insert_VinculoOrfao_Recusado()
    {
        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            $"""
            INSERT INTO publicacoes.vinculos_do_ato_com_entidade (id, ato_id, entidade_tipo, entidade_id)
            VALUES ({Guid.CreateVersion7()}, {Guid.CreateVersion7()}, 'PROCESSO_SELETIVO', {Guid.CreateVersion7()})
            """));

        erro.SqlState.Should().Be(ForeignKeyViolation);
    }

    /// <remarks>
    /// O ato e o vínculo do segundo entram por via crua (o handler o recusaria antes), para exercitar
    /// exatamente a trava do banco, e não a do handler.
    /// </remarks>
    [Fact(DisplayName = "Duas linhagens não ocupam a vaga do mesmo objeto: o índice único recusa a segunda")]
    public async Task Insert_SegundaVagaNoMesmoObjeto_Colide()
    {
        Guid processo = Guid.CreateVersion7();
        AtoNormativo primeiro = await GravarComVaga(processo);
        AtoNormativo segundo = (Base with { UnicoPorObjeto = true, Vinculos = [new("PROCESSO_SELETIVO", processo)] }).Registrar();

        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarNumaTransacaoAsync(
            InsertAtoCru(segundo),
            InsertVinculoCru(segundo.Id, "PROCESSO_SELETIVO", processo),
            InsertVagaCru(segundo.Id, segundo.Id, "PROCESSO_SELETIVO", processo)));

        erro.SqlState.Should().Be(UniqueViolation);
        erro.ConstraintName.Should().Be(RestricoesDoBanco.VagaDeLinhagemObjetoTipoUnica);
        primeiro.Id.Should().NotBe(segundo.Id);
    }

    /// <remarks>A raiz declarada na vaga é outro ato, que nada tem com a cadeia do ato que a reserva.</remarks>
    [Fact(DisplayName = "Vaga forjada em nome de uma raiz que não é a da cadeia do ato é bloqueada")]
    public async Task Insert_VagaComRaizFalsa_Bloqueada()
    {
        Guid processo = Guid.CreateVersion7();
        AtoNormativo ato = (Base with { UnicoPorObjeto = true, Vinculos = [new("PROCESSO_SELETIVO", processo)] }).Registrar();
        AtoNormativo outro = (Base with { UnicoPorObjeto = true }).Registrar();

        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarNumaTransacaoAsync(
            InsertAtoCru(ato),
            InsertAtoCru(outro),
            InsertVinculoCru(ato.Id, "PROCESSO_SELETIVO", processo),
            InsertVagaCru(ato.Id, outro.Id, "PROCESSO_SELETIVO", processo)));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "Vaga de objeto a que o ato não se vincula é bloqueada")]
    public async Task Insert_VagaSemVinculo_Bloqueada()
    {
        Guid processo = Guid.CreateVersion7();
        AtoNormativo ato = (Base with { UnicoPorObjeto = true }).Registrar();

        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarNumaTransacaoAsync(
            InsertAtoCru(ato),
            InsertVagaCru(ato.Id, ato.Id, "PROCESSO_SELETIVO", processo)));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    /// <remarks>
    /// É esta trava que faz o índice único valer alguma coisa: sem ela, gravar o vínculo e omitir a
    /// vaga deixaria o objeto livre para uma segunda linhagem, e o índice, que só vê as vagas que
    /// existem, nada acusaria.
    /// </remarks>
    [Fact(DisplayName = "Vínculo de ato único por objeto sem a vaga reservada é bloqueado — a correspondência é obrigatória")]
    public async Task Insert_VinculoSemVaga_Bloqueado()
    {
        Guid processo = Guid.CreateVersion7();
        AtoNormativo ato = (Base with { UnicoPorObjeto = true }).Registrar();

        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarNumaTransacaoAsync(
            InsertAtoCru(ato),
            InsertVinculoCru(ato.Id, "PROCESSO_SELETIVO", processo)));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    /// <remarks>
    /// A chave estrangeira não barra isto: ela é verificada ao fim do comando, e no fim do comando os
    /// dois atos existem. O CHECK só cobre o ciclo de tamanho um, e o índice único da linearidade não
    /// vê ciclo nenhum (A→B e B→A são alvos distintos). Um ciclo gravado seria irreparável — UPDATE e
    /// DELETE estão bloqueados — e faria toda travessia da cadeia girar para sempre.
    /// </remarks>
    [Fact(DisplayName = "Ciclo de retificação inserido num único comando é bloqueado")]
    public async Task Insert_CicloDeRetificacao_Bloqueado()
    {
        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            InsertDeCicloDeRetificacao(Guid.CreateVersion7(), Guid.CreateVersion7())));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "TRUNCATE do vínculo é bloqueado — não é DELETE, e esvaziaria o registro sem disparar o append-only")]
    public async Task Truncate_DoVinculo_Bloqueado()
    {
        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            $"TRUNCATE publicacoes.vinculos_do_ato_com_entidade"));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "TRUNCATE do ato publicado também é bloqueado")]
    public async Task Truncate_DoAto_Bloqueado()
    {
        PostgresException erro = await Assert.ThrowsAsync<PostgresException>(() => ExecutarCruAsync(
            $"TRUNCATE publicacoes.atos_normativos CASCADE"));

        erro.SqlState.Should().Be(RestrictViolation);
    }

    [Fact(DisplayName = "Vínculo de ato não único por objeto dispensa vaga")]
    public async Task Insert_VinculoDeAtoNaoUnico_NaoExigeVaga()
    {
        AtoNormativo ato = (Base with { Vinculos = [new("CHAMADA", Guid.CreateVersion7())] }).Registrar();

        await Gravar(ato);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        (await ctx.VinculosAtoEntidade.CountAsync(v => v.AtoId == ato.Id)).Should().Be(1);
        (await ctx.VagasDeLinhagem.AnyAsync(l => l.AtoId == ato.Id)).Should().BeFalse();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<AtoNormativo> GravarComVaga(Guid processo)
    {
        AtoNormativo ato = (Base with { UnicoPorObjeto = true, Vinculos = [new("PROCESSO_SELETIVO", processo)] }).Registrar();

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        ctx.AtosNormativos.Add(ato);
        ctx.VagasDeLinhagem.Add(
            VagaDeLinhagem.Criar(ato, ato.Vinculos.Single(), raizId: ato.Id));
        await ctx.SaveChangesAsync();

        return ato;
    }

    private async Task Gravar(AtoNormativo ato)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        ctx.AtosNormativos.Add(ato);
        await ctx.SaveChangesAsync();
    }

    private async Task ExecutarCruAsync(FormattableString sql)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await ctx.Database.ExecuteSqlAsync(sql);
    }

    private async Task ExecutarNumaTransacaoAsync(params FormattableString[] comandos)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await using IDbContextTransaction transacao = await ctx.Database.BeginTransactionAsync();

        foreach (FormattableString sql in comandos)
        {
            await ctx.Database.ExecuteSqlAsync(sql);
        }

        await transacao.CommitAsync();
    }

    private static FormattableString InsertDeCicloDeRetificacao(Guid a, Guid b) =>
        $"""
        INSERT INTO publicacoes.atos_normativos
            (id, orgao, serie, ano, numero, tipo_codigo, congela_configuracao, efeito_irreversivel,
             unico_por_objeto, data_do_ato, documento_hash, assinante, publicado_em,
             ato_retificado_id, motivo_retificacao)
        VALUES
            ({a}, 'CEPS', 'EDITAL', 2026, NULL, {TipoUnico}, true, false, false,
             DATE '2026-03-13', {HashValido}, {PersonasDeTeste.NomeKevinCarvalho}, now(), {b}, 'ciclo'),
            ({b}, 'CEPS', 'EDITAL', 2026, NULL, {TipoUnico}, true, false, false,
             DATE '2026-03-13', {HashValido}, {PersonasDeTeste.NomeKevinCarvalho}, now(), {a}, 'ciclo')
        """;

    private static FormattableString InsertAtoCru(AtoNormativo ato) =>
        $"""
        INSERT INTO publicacoes.atos_normativos
            (id, orgao, serie, ano, numero, tipo_codigo, congela_configuracao, efeito_irreversivel,
             unico_por_objeto, data_do_ato, documento_hash, assinante, publicado_em)
        VALUES
            ({ato.Id}, 'CEPS', 'EDITAL', 2026, NULL, {ato.TipoCodigo}, true, false,
             {ato.UnicoPorObjeto}, DATE '2026-03-13', {HashValido},
             {PersonasDeTeste.NomeKevinCarvalho}, now())
        """;

    private static FormattableString InsertVinculoCru(Guid atoId, string entidadeTipo, Guid entidadeId) =>
        $"""
        INSERT INTO publicacoes.vinculos_do_ato_com_entidade (id, ato_id, entidade_tipo, entidade_id)
        VALUES ({Guid.CreateVersion7()}, {atoId}, {entidadeTipo}, {entidadeId})
        """;

    private static FormattableString InsertVagaCru(Guid atoId, Guid raizId, string entidadeTipo, Guid entidadeId) =>
        $"""
        INSERT INTO publicacoes.vagas_de_linhagem
            (id, entidade_tipo, entidade_id, tipo_codigo, raiz_id, ato_id)
        VALUES ({Guid.CreateVersion7()}, {entidadeTipo}, {entidadeId}, {TipoUnico}, {raizId}, {atoId})
        """;
}
