namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.CalendariosDiasUteis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Integração ponta-a-ponta do calendário de dias úteis contra Postgres real
/// (UNI-REQ-0116): persistência do calendário e dos dias não úteis (código de contrato
/// da <c>Abrangencia</c> na coluna), índice único
/// parcial de vigência, CHECK de coerência de município e soft-delete
/// preservando os dias não úteis filhos (<c>ClientNoAction</c>).
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class CalendarioDiasUteisPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private readonly ConfiguracaoDbFixture _fixture;

    public CalendarioDiasUteisPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Persistir com DiasNaoUteis e reler com Include devolve os dias corretos")]
    public async Task Insert_PersisteDiasNaoUteisComTokenCorreto()
    {
        CalendarioDiasUteis calendario = Nova(
            VersaoUnica(),
            new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2027, 1, 1), "Confraternização Universal"),
            new DiaNaoUtilCriacao("ESTADUAL", null, null, null, new DateOnly(2027, 8, 15), "Adesão do Pará à Independência", "PA"),
            new DiaNaoUtilCriacao("MUNICIPAL", "1504208", "Marabá", "PA", new DateOnly(2027, 5, 8), "Aniversário de Marabá"));

        await PersistirAsync(calendario);

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        CalendarioDiasUteis persistido = await readCtx.CalendariosDiasUteis
            .Include(c => c.DiasNaoUteis)
            .SingleAsync(c => c.Id == calendario.Id);

        ConferirDiasPersistidos(persistido);
    }

    [Fact(DisplayName = "A coluna da abrangência grava o código de contrato de cada dia")]
    public async Task Insert_ColunaDaAbrangencia_GravaOCodigoDeContrato()
    {
        CalendarioDiasUteis calendario = Nova(
            VersaoUnica(),
            new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2027, 1, 1), "Confraternização Universal"),
            new DiaNaoUtilCriacao("INSTITUCIONAL", null, null, null, new DateOnly(2027, 12, 24), "Recesso institucional"));
        await PersistirAsync(calendario);
        await using UniPlusDbContext leitura = _fixture.CreateDbContext(userId: null);
        List<string> gravados = await leitura.Database
            .SqlQuery<string>($"SELECT abrangencia AS \"Value\" FROM configuracao.dias_nao_uteis WHERE calendario_dias_uteis_id = {calendario.Id} ORDER BY data")
            .ToListAsync();

        gravados.Should().Equal("NACIONAL", "INSTITUCIONAL");
    }

    [Fact(DisplayName = "Exclusion constraint (vigente) rejeita um segundo calendário vigente")]
    public async Task ExclusionConstraint_Vigente_RejeitaSegundoVigenteAtivo()
    {
        CalendarioDiasUteis primeiro = Nova(VersaoUnica());
        primeiro.MarcarVigente();
        await PersistirAsync(primeiro);
        CalendarioDiasUteis segundo = Nova(VersaoUnica());
        segundo.MarcarVigente();
        await using UniPlusDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.CalendariosDiasUteis.Add(segundo);
        Func<Task> act = async () => await ctx2.SaveChangesAsync();
        Npgsql.PostgresException pg = (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        pg.SqlState.Should().Be("23P01");
        pg.ConstraintName.Should().Be(RestricoesDoBanco.CalendarioDeDiasUteisVigenteUnico);
        await DesmarcarVigenteAsync(primeiro.Id);
    }

    [Fact(DisplayName = "Sob transação ambiente, só ForcarChecagemImediataDeConstraints detecta a colisão")]
    public async Task ExclusionConstraint_SobTransacaoAmbiente_SoDetectaNaChecagemForcada()
    {
        CalendarioDiasUteis primeiro = Nova(VersaoUnica());
        primeiro.MarcarVigente();
        await PersistirAsync(primeiro);
        CalendarioDiasUteis segundo = Nova(VersaoUnica());
        segundo.MarcarVigente();
        await using UniPlusDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx = await ctx2.Database.BeginTransactionAsync();
        ctx2.CalendariosDiasUteis.Add(segundo);
        await ctx2.SaveChangesAsync();
        Func<Task> act = async () => await ctx2.ForcarChecagemImediataDeConstraintsAsync();
        Npgsql.PostgresException pg = (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        pg.SqlState.Should().Be("23P01");
        pg.ConstraintName.Should().Be(RestricoesDoBanco.CalendarioDeDiasUteisVigenteUnico);
        await DesmarcarVigenteAsync(primeiro.Id);
    }

    [Fact(DisplayName = "CHECK de banco rejeita dia municipal com snapshot parcial via SQL cru")]
    public async Task Check_RejeitaMunicipalComSnapshotParcialViaSqlCru()
    {
        CalendarioDiasUteis pai = Nova(VersaoUnica());
        await PersistirAsync(pai);
        await using UniPlusDbContext ctx2 = _fixture.CreateDbContext(userId: null);
        Func<Task> act = async () => await ctx2.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO configuracao.dias_nao_uteis
                 (id, calendario_dias_uteis_id, abrangencia, municipio_ibge, data, descricao, created_at)
             VALUES
                 ({Guid.CreateVersion7()}, {pai.Id}, {"MUNICIPAL"}, {"1504208"}, {new DateOnly(2027, 1, 1)}, {"Snapshot parcial"}, {DateTimeOffset.UtcNow})
             """);
        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_dias_nao_uteis_municipio_coerente exige a tripla municipal completa");
    }

    [Fact(DisplayName = "CHECK de banco rejeita campo municipal fora de MUNICIPAL via SQL cru")]
    public async Task Check_RejeitaCampoMunicipalForaDeMunicipalViaSqlCru()
    {
        CalendarioDiasUteis pai = Nova(VersaoUnica());
        await PersistirAsync(pai);
        await using UniPlusDbContext ctx2 = _fixture.CreateDbContext(userId: null);
        Func<Task> act = async () => await ctx2.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO configuracao.dias_nao_uteis
                 (id, calendario_dias_uteis_id, abrangencia, municipio_nome, data, descricao, created_at)
             VALUES
                 ({Guid.CreateVersion7()}, {pai.Id}, {"NACIONAL"}, {"Marabá"}, {new DateOnly(2027, 1, 1)}, {"Campo indevido"}, {DateTimeOffset.UtcNow})
             """);
        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_dias_nao_uteis_municipio_coerente proíbe cada campo municipal nas demais abrangências");
    }

    [Fact(DisplayName = "Soft-delete de calendário não vigente preserva os dias não úteis filhos (ClientNoAction)")]
    public async Task SoftDelete_PreservaDiasNaoUteisFilhos()
    {
        CalendarioDiasUteis calendario = Nova(
            VersaoUnica(),
            new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2027, 1, 1), "Confraternização Universal"),
            new DiaNaoUtilCriacao("INSTITUCIONAL", null, null, null, new DateOnly(2027, 12, 24), "Recesso da Unifesspa"));
        await PersistirAsync(calendario);
        await ExcluirAsync(calendario.Id);
        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        CalendarioDiasUteis excluido = await readCtx.CalendariosDiasUteis
            .IgnoreQueryFilters()
            .Include(c => c.DiasNaoUteis)
            .SingleAsync(c => c.Id == calendario.Id);
        excluido.IsDeleted.Should().BeTrue();
        excluido.DeletedBy.Should().Be(AdminB);
        excluido.DiasNaoUteis.Should().HaveCount(2, "os dias não úteis não têm soft-delete próprio e permanecem sob a linha soft-deleted");
    }

    [Fact(DisplayName = "ICalendarioVigenteReader preserva abrangência e município por dia")]
    public async Task VigenteReader_PreservaAbrangenciaEMunicipioPorDia()
    {
        CalendarioDiasUteis calendario = Nova(
            VersaoUnica(),
            new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2027, 1, 1), "Confraternização Universal"),
            new DiaNaoUtilCriacao("MUNICIPAL", "1504208", "Marabá", "PA", new DateOnly(2027, 5, 8), "Aniversário de Marabá"));
        calendario.MarcarVigente();
        await PersistirAsync(calendario);
        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new CalendarioVigenteReader(readCtx);
        CalendarioVigenteView? vigente = await reader.ObterVigenteAsync();
        ConferirDiasDoVigente(vigente, calendario.Id);
    }

    [Fact(DisplayName = "Calendário carregado do banco recusa dia repetido contra os dias já gravados")]
    public async Task IncluirDiaNaoUtil_AgregadoReidratado_RecusaRepetidoEAceitaDiferente()
    {
        CalendarioDiasUteis calendario = Nova(VersaoUnica());
        await PersistirAsync(calendario);

        await using UniPlusDbContext leitura = _fixture.CreateDbContext(AdminA);
        CalendarioDiasUteis reidratado = await leitura.CalendariosDiasUteis
            .Include(c => c.DiasNaoUteis)
            .SingleAsync(c => c.Id == calendario.Id);

        reidratado.IncluirDiaNaoUtil(
            new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2027, 1, 1), "Repetido")).Error?.Code
            .Should().Be(CalendarioDiasUteisErrorCodes.DataDuplicadaNoCalendario);
        reidratado.IncluirDiaNaoUtil(
            new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2027, 1, 2), "Outro dia")).IsSuccess
            .Should().BeTrue();
    }

    private static void ConferirDiasPersistidos(CalendarioDiasUteis persistido)
    {
        persistido.CreatedBy.Should().Be(AdminA);
        persistido.Vigente.Should().BeFalse();
        persistido.DiasNaoUteis.Should().HaveCount(3);
        persistido.DiasNaoUteis.Should().Contain(d =>
            d.Abrangencia == Abrangencia.Nacional && d.MunicipioIbge == null && d.Data == new DateOnly(2027, 1, 1));
        persistido.DiasNaoUteis.Should().Contain(d =>
            d.Abrangencia == Abrangencia.Estadual
            && d.MunicipioIbge == null
            && d.MunicipioNome == null
            && d.MunicipioUf == null
            && d.Uf == "PA"
            && d.Data == new DateOnly(2027, 8, 15));
        ConferirDiaMunicipal(persistido);
    }

    private static void ConferirDiaMunicipal(CalendarioDiasUteis persistido)
    {
        persistido.DiasNaoUteis.Should().Contain(d =>
            d.Abrangencia == Abrangencia.Municipal
            && d.MunicipioIbge == "1504208"
            && d.MunicipioNome == "Marabá"
            && d.MunicipioUf == "PA"
            && d.Uf == null
            && d.Data == new DateOnly(2027, 5, 8));
    }

    private static void ConferirDiasDoVigente(CalendarioVigenteView? vigente, Guid id)
    {
        vigente.Should().NotBeNull();
        vigente!.Id.Should().Be(id);
        vigente.DiasNaoUteis.Should().HaveCount(2);
        vigente.DiasNaoUteis.Should().Contain(d =>
            d.Data == new DateOnly(2027, 1, 1) && d.Abrangencia == "NACIONAL" && d.MunicipioIbge == null);
        vigente.DiasNaoUteis.Should().Contain(d =>
            d.Data == new DateOnly(2027, 5, 8)
            && d.Abrangencia == "MUNICIPAL"
            && d.MunicipioIbge == "1504208"
            && d.MunicipioNome == "Marabá"
            && d.MunicipioUf == "PA"
            && d.Uf == null);
    }

    private async Task ExcluirAsync(Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminB);
        CalendarioDiasUteis calendario = await ctx.CalendariosDiasUteis.SingleAsync(c => c.Id == id);
        ctx.CalendariosDiasUteis.Remove(calendario);
        await ctx.SaveChangesAsync();
    }

    private async Task DesmarcarVigenteAsync(Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        CalendarioDiasUteis calendario = await ctx.CalendariosDiasUteis.SingleAsync(c => c.Id == id);
        calendario.MarcarNaoVigente();
        await ctx.SaveChangesAsync();
    }

    private async Task PersistirAsync(CalendarioDiasUteis calendario)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.CalendariosDiasUteis.Add(calendario);
        await ctx.SaveChangesAsync();
    }

    private static string VersaoUnica() => $"cal-{Guid.NewGuid():N}"[..20];

    private static CalendarioDiasUteis Nova(string versaoDoCalendario, params DiaNaoUtilCriacao[] dias)
    {
        DiaNaoUtilCriacao[] diasNaoUteis = dias.Length == 0
            ? [new DiaNaoUtilCriacao("NACIONAL", null, null, null, new DateOnly(2027, 1, 1), "Confraternização Universal")]
            : dias;
        return CalendarioDiasUteis.Criar(versaoDoCalendario, diasNaoUteis).ValorEsperado();
    }
}
