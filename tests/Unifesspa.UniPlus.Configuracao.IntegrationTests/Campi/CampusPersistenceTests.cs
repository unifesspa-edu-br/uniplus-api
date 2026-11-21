namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Campi;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Integração ponta-a-ponta dos cadastros contra Postgres real (UNI-REQ #587):
/// CA-02 (persistência por código + nome copiado, sem FK ao Geo), UNIQUE parcial
/// de sigla, soft-delete e bloqueio de remoção de Campus com LocalOferta vivo.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class CampusPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private static readonly DateTimeOffset Agora = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    private readonly ConfiguracaoDbFixture _fixture;

    public CampusPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-02: criar Campus persiste a referência de cidade por código + nome copiado (sem FK ao Geo)")]
    public async Task Insert_PersisteReferenciaCidadeEDisplayCache()
    {
        Campus campus = NovoCampus("CAMar", "Campus Marabá", "1504208", "Marabá", "PA");
        await PersistirAsync(campus, AdminA);

        Campus persistido = await LerAsync(campus.Id);

        persistido.Sigla.Should().Be("CAMAR");
        persistido.CidadeCodigoIbge.Should().Be("1504208");
        persistido.CidadeNome.Should().Be("Marabá");
        persistido.CidadeUf.Should().Be("PA");
        persistido.CidadeOrigem.Should().Be(ReferenciaCidadeGeo.OrigemGeoApi);
        persistido.CidadeDisplayAtualizadoEm.Should().Be(Agora);
        persistido.CreatedBy.Should().Be(AdminA);
        persistido.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "UNIQUE parcial (sigla) rejeita segundo Campus vivo com a mesma sigla")]
    public async Task UniquePartial_Sigla_RejeitaDuplicataAtiva()
    {
        await PersistirAsync(NovoCampus("DUPSIG", "Campus Um", "1504208", "Marabá", "PA"), AdminA);
        Campus segundo = NovoCampus("DUPSIG", "Campus Dois", "1501402", "Belém", "PA");
        await using UniPlusDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.Campi.Add(segundo);

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>()
            .WithInnerException(typeof(Npgsql.PostgresException));
    }

    [Fact(DisplayName = "Soft-delete liberta o slot da UNIQUE parcial de sigla")]
    public async Task SoftDelete_LibertaUniquePartialSigla()
    {
        Campus campus = NovoCampus("RECSIG", "Campus Reciclado", "1504208", "Marabá", "PA");
        await PersistirAsync(campus, AdminA);
        await RemoverAsync(campus.Id, AdminB);

        await ConferirExcluidoPorAsync(campus.Id, AdminB);

        Campus novo = NovoCampus("RECSIG", "Campus Novo", "1501402", "Belém", "PA");
        await using UniPlusDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.Campi.Add(novo);

        Func<Task> act = async () => await ctx3.SaveChangesAsync();

        await act.Should().NotThrowAsync("o slot da sigla foi liberado pelo soft-delete");
    }

    [Fact(DisplayName = "CA-05: remover Campus responsável por LocalOferta vivo é bloqueado (409)")]
    public async Task RemoverCampus_ComLocalOfertaVivo_Bloqueia()
    {
        Campus campus = await PersistirCampusComLocalOfertaAsync("BLKCAM", "Campus Bloqueado");

        await using UniPlusDbContext handlerCtx = _fixture.CreateDbContext(AdminB);
        Result resultado = await RemoverCampusCommandHandler.Handle(
            new RemoverCampusCommand(campus.Id),
            new CampusRepository(handlerCtx),
            new LocalOfertaRepository(handlerCtx),
            handlerCtx,
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CampusErrorCodes.RemocaoBloqueadaPorLocalOferta);
    }

    [Fact(DisplayName = "FK campus_responsavel_id com RESTRICT bloqueia DELETE físico do Campus com LocalOferta")]
    public async Task FkRestrict_BloqueiaDeleteFisicoDoCampus()
    {
        Campus campus = await PersistirCampusComLocalOfertaAsync("FKCAM", "Campus FK");

        Func<Task> act = () => ExecutarSqlCruAsync($"DELETE FROM configuracao.campus WHERE id = {campus.Id}");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "a FK campus_responsavel_id com RESTRICT impede o DELETE físico do campus referenciado");
    }

    [Fact(DisplayName = "CA-01/CA-07: criar Campus com endereço estruturado faz round-trip das colunas owned")]
    public async Task Insert_ComEndereco_RoundTrip()
    {
        Campus campus = NovoCampus("ENDCAM", "Campus Endereço", "1504208", "Marabá", "PA", EnderecoCoerente());
        await PersistirAsync(campus, AdminA);

        Campus persistido = await LerAsync(campus.Id);

        persistido.Endereco.Should().NotBeNull();
        persistido.Endereco!.Cep.Should().Be("68507590");
        persistido.Endereco.Logradouro.Should().Be("Folha 31");
        persistido.Endereco.CidadeCodigoIbge.Should().Be("1504208");
        persistido.Endereco.NivelResolucao.Should().Be(NivelResolucaoEndereco.Logradouro);
        persistido.Endereco.DisplayAtualizadoEm.Should().Be(Agora);
    }

    [Fact(DisplayName = "Owned type opcional: Campus sem endereço materializa Endereco nulo (sem endereço fantasma)")]
    public async Task Insert_SemEndereco_MaterializaNulo()
    {
        Campus campus = NovoCampus("NOENDR", "Campus Sem Endereço", "1504208", "Marabá", "PA");
        await PersistirAsync(campus, AdminA);

        Campus persistido = await LerAsync(campus.Id);

        persistido.Endereco.Should().BeNull("todas as colunas endereco_* nulas ⇒ owned type ausente");
    }

    // Diverge o snapshot de cidade do endereço (Belém) da cidade do campus (Marabá).
    [Fact(DisplayName = "CA-04: CHECK de coerência cidade↔CEP rejeita UPDATE cru com cidade do endereço divergente")]
    public async Task CheckCoerencia_RejeitaUpdateCruIncoerente()
    {
        Campus campus = NovoCampus("CKCOER", "Campus Coerência", "1504208", "Marabá", "PA", EnderecoCoerente());
        await PersistirAsync(campus, AdminA);

        Func<Task> act = () => ExecutarSqlCruAsync(
            $"UPDATE configuracao.campus SET endereco_cidade_codigo_ibge = '1501402' WHERE id = {campus.Id}");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_campus_endereco_cidade_coerente impede divergência cidade↔CEP");
    }

    // Código IBGE do endereço ainda bate, mas a UF vira nula — sem o ramo
    // IS NOT NULL no CHECK, a comparação viraria UNKNOWN e passaria.
    [Fact(DisplayName = "CA-04: CHECK NULL-safe rejeita UF do endereço nula com código IBGE batendo")]
    public async Task CheckCoerencia_RejeitaUfNulaComCodigoBatendo()
    {
        Campus campus = NovoCampus("CKUFNL", "Campus UF Nula", "1504208", "Marabá", "PA", EnderecoCoerente());
        await PersistirAsync(campus, AdminA);

        Func<Task> act = () => ExecutarSqlCruAsync(
            $"UPDATE configuracao.campus SET endereco_cidade_uf = NULL WHERE id = {campus.Id}");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK NULL-safe rejeita UF nula no endereço quando há código IBGE coincidente");
    }

    // Define só o CEP — sem cidade/nível/origem do owned type. A coluna que indica
    // presença do endereço (endereco_cep) ficaria não-nulo e o EF materializaria um endereço
    // parcial. O CHECK de completude (all-or-nothing) rejeita.
    [Fact(DisplayName = "Completude: CHECK rejeita UPDATE cru que deixa só endereco_cep preenchido")]
    public async Task CheckCompletude_RejeitaCepSemRestante()
    {
        Campus campus = NovoCampus("CKCOMP", "Campus Completude", "1504208", "Marabá", "PA");
        await PersistirAsync(campus, AdminA);

        Func<Task> act = () => ExecutarSqlCruAsync(
            $"UPDATE configuracao.campus SET endereco_cep = '68507590' WHERE id = {campus.Id}");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_campus_endereco_completo exige todos os campos obrigatórios do endereço");
    }

    // Preenche só um campo opcional (logradouro), sem CEP nem os obrigatórios —
    // fragmento que ficaria oculto (endereco_cep nulo ⇒ owned ausente). O CHECK
    // all-or-nothing (que inclui as colunas opcionais no ramo "ausente") rejeita.
    [Fact(DisplayName = "Completude: CHECK rejeita UPDATE cru que preenche só um campo opcional do endereço")]
    public async Task CheckCompletude_RejeitaOpcionalSemObrigatorios()
    {
        Campus campus = NovoCampus("CKOPC", "Campus Opcional", "1504208", "Marabá", "PA");
        await PersistirAsync(campus, AdminA);

        Func<Task> act = () => ExecutarSqlCruAsync(
            $"UPDATE configuracao.campus SET endereco_logradouro = 'Rua Fantasma' WHERE id = {campus.Id}");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_campus_endereco_completo rejeita fragmentos opcionais sem o endereço completo");
    }

    private async Task PersistirAsync(Campus campus, string usuario)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(usuario);
        ctx.Campi.Add(campus);
        await ctx.SaveChangesAsync();
    }

    private async Task<Campus> PersistirCampusComLocalOfertaAsync(string sigla, string nome)
    {
        Campus campus = NovoCampus(sigla, nome, "1504208", "Marabá", "PA");
        await PersistirAsync(campus, AdminA);

        LocalOferta local = LocalOferta.Criar(
            new(TipoLocalOferta.CursoForaDeSede, campus.Id, "1504208", "Marabá", "PA", null, null),
            Agora).ValorEsperado();
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.LocaisOferta.Add(local);
        await ctx.SaveChangesAsync();
        return campus;
    }

    private async Task<Campus> LerAsync(Guid id)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        return await ctx.Campi.SingleAsync(c => c.Id == id);
    }

    private async Task RemoverAsync(Guid id, string usuario)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(usuario);
        Campus rastreado = await ctx.Campi.SingleAsync(c => c.Id == id);
        ctx.Campi.Remove(rastreado);
        await ctx.SaveChangesAsync();
    }

    private async Task ConferirExcluidoPorAsync(Guid id, string usuario)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        Campus excluido = await ctx.Campi.IgnoreQueryFilters().SingleAsync(c => c.Id == id);
        excluido.IsDeleted.Should().BeTrue();
        excluido.DeletedBy.Should().Be(usuario);
    }

    private async Task ExecutarSqlCruAsync(FormattableString comando)
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        await ctx.Database.ExecuteSqlAsync(comando);
    }

    private static ReferenciaEnderecoGeo EnderecoCoerente() =>
        ReferenciaEnderecoGeo.Criar(
            "68507590", "Folha 31", "s/n", null, "Nova Marabá", null,
            "1504208", "Marabá", "PA", -5.3m, -49.1m,
            NivelResolucaoEndereco.Logradouro, "logradouro", Agora).Value!;

    private static Campus NovoCampus(
        string sigla, string nome, string codigoIbge, string cidadeNome, string uf,
        ReferenciaEnderecoGeo? endereco = null) =>
        Campus.Criar(
            sigla, nome, codigoIbge, cidadeNome, uf,
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, endereco, null).Value!;
}
