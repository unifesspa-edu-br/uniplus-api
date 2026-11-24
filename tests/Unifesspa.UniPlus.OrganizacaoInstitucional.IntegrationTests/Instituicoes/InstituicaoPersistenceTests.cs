namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Instituicoes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Application.Abstractions.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Configurations;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// Integração ponta-a-ponta da Story #585 contra Postgres real. Cobre os
/// cenários de persistência da <see cref="Instituicao"/> singleton: índice único
/// parcial sentinela (no máximo uma viva), soft-delete liberando o slot, audit
/// trail e FK intra-banco <c>reitoria_id → unidade(id)</c>.
/// </summary>
public sealed class InstituicaoPersistenceTests : IClassFixture<InstituicaoDbFixture>
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private static readonly DateOnly DataInicio = new(2026, 1, 1);

    private static readonly CidadeDaSede MarabaPeloGeo = new("1504208", "Marabá", "PA", "geo-api", null);

    private readonly InstituicaoDbFixture _fixture;

    public InstituicaoPersistenceTests(InstituicaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Insert persiste a Instituição e preenche created_by via AuditableInterceptor")]
    public async Task Insert_PersisteEPreencheAuditTrail()
    {
        await LimparInstituicoesAsync();
        Instituicao instituicao = NovaInstituicao(IdentificacaoValida("9001", "INS-A"));

        await _fixture.GravarAsync(AdminA, instituicao);

        Instituicao persistida = await _fixture.RecarregarAsync<Instituicao>(instituicao.Id);
        persistida.CodigoEmec.Should().Be("9001");
        persistida.IsDeleted.Should().BeFalse();
        persistida.CreatedBy.Should().Be(AdminA);
        persistida.UpdatedBy.Should().BeNull("apenas criação — sem update ainda");
    }

    [Fact(DisplayName = "Insert sem cidade faz round-trip de contexto limpo com o trio, a proveniência e o carimbo nulos")]
    public async Task Insert_SemCidade_RoundTripComCidadeAusente()
    {
        await LimparInstituicoesAsync();
        Instituicao instituicao = NovaInstituicao(IdentificacaoValida("9003", "INS-SEM-GEO"));

        await _fixture.GravarAsync(AdminA, instituicao);

        Instituicao persistida = await _fixture.RecarregarAsync<Instituicao>(instituicao.Id);
        persistida.CidadeDaSede.Should().Be(CidadeDaSede.Nenhuma);
    }

    [Fact(DisplayName = "Insert com cidade faz round-trip de contexto limpo da cidade como o agregado a expõe")]
    public async Task Insert_ComCidade_RoundTripDaCidadeDaSede()
    {
        await LimparInstituicoesAsync();
        CidadeDaSede carimbada = MarabaPeloGeo with { DisplayAtualizadoEm = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        Instituicao instituicao = NovaInstituicao(IdentificacaoValida("9005", "INS-CID"), carimbada);

        await _fixture.GravarAsync(AdminA, instituicao);

        Instituicao persistida = await _fixture.RecarregarAsync<Instituicao>(instituicao.Id);
        persistida.CidadeDaSede.Should().Be(carimbada);
    }

    [Fact(DisplayName = "A propriedade calculada CidadeDaSede não está no modelo do EF; as cinco colunas planas, sim")]
    public void Modelo_CidadeDaSede_NaoEhMapeada()
    {
        using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        IEntityType tipo = ctx.Model.FindEntityType(typeof(Instituicao))!;

        tipo.GetProperties().Select(propriedade => propriedade.Name).Should()
            .NotContain(nameof(Instituicao.CidadeDaSede))
            .And.Contain(
                [nameof(Instituicao.CidadeCodigoIbge), nameof(Instituicao.CidadeNome), nameof(Instituicao.CidadeUf),
                 nameof(Instituicao.CidadeOrigem), nameof(Instituicao.CidadeDisplayAtualizadoEm)]);
        tipo.GetNavigations().Select(navegacao => navegacao.Name).Should().NotContain(nameof(Instituicao.CidadeDaSede));
        tipo.GetComplexProperties().Select(complexa => complexa.Name).Should().NotContain(nameof(Instituicao.CidadeDaSede));
    }

    [Fact(DisplayName = "Insert persiste a referência de cidade do Geo e faz round-trip do trio + display cache (#686)")]
    public async Task Insert_ComReferenciaDeCidade_RoundTrip()
    {
        await LimparInstituicoesAsync();
        DateTimeOffset carimbo = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Instituicao instituicao = NovaInstituicao(
            IdentificacaoValida("9002", "INS-GEO"),
            MarabaPeloGeo with { DisplayAtualizadoEm = carimbo });

        await _fixture.GravarAsync(AdminA, instituicao);

        Instituicao persistida = await _fixture.RecarregarAsync<Instituicao>(instituicao.Id);
        persistida.CidadeCodigoIbge.Should().Be("1504208");
        persistida.CidadeNome.Should().Be("Marabá");
        persistida.CidadeUf.Should().Be("PA");
        persistida.CidadeOrigem.Should().Be("geo-api");
        persistida.CidadeDisplayAtualizadoEm.Should().Be(carimbo);
    }

    /// <remarks>
    /// Trava as constantes que a gravação usa para traduzir a corrida concorrente
    /// (ConstraintVioladaNaGravacao.NomeDaConstraintViolada contra o índice do singleton) em
    /// JaExisteInstituicaoViva/409: SqlState 23505 + nome do índice sentinela.
    /// </remarks>
    [Fact(DisplayName = "Índice único parcial rejeita uma segunda Instituição viva (singleton)")]
    public async Task Singleton_SegundaInstituicaoVivaRejeitada()
    {
        await LimparInstituicoesAsync();
        await _fixture.GravarAsync(AdminA, NovaInstituicao(IdentificacaoValida("9100", "INS-1")));

        await using UniPlusDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.Instituicoes.Add(NovaInstituicao(IdentificacaoValida("9101", "INS-2")));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be(RestricoesDoBanco.InstituicaoUnicaViva);
        ConstraintVioladaNaGravacao.NomeDaConstraintViolada(ex).Should().Be(RestricoesDoBanco.InstituicaoUnicaViva);
    }

    [Fact(DisplayName = "CHECK constraint impede gravar a sentinela como false (singleton constante no banco)")]
    public async Task CheckConstraint_ImpedeSentinelaFalse()
    {
        await LimparInstituicoesAsync();
        Instituicao instituicao = NovaInstituicao(IdentificacaoValida("9500", "INS-CK"));
        await _fixture.GravarAsync(AdminA, instituicao);

        await _fixture.ConferirQueOBancoRecusaPorCheckAsync(
            $"UPDATE organizacao.instituicoes SET instituicao_unica = false WHERE id = {instituicao.Id}",
            "ck_instituicoes_instituicao_unica",
            "a CHECK constraint ck_instituicoes_instituicao_unica só admite a sentinela true");
    }

    [Fact(DisplayName = "Soft-delete mantém a Instituição removida na trilha com a auditoria da exclusão")]
    public async Task SoftDelete_MantemRemovidaNaTrilhaComAuditoria()
    {
        await LimparInstituicoesAsync();
        Instituicao primeira = NovaInstituicao(IdentificacaoValida("9200", "INS-DEL"));
        await _fixture.GravarAsync(AdminA, primeira);

        await _fixture.RemoverAsync<Instituicao>(AdminB, primeira.Id);

        // O registro removido permanece na trilha (IgnoreQueryFilters) com a auditoria preenchida.
        Instituicao removida = await _fixture.RecarregarExcluidaAsync<Instituicao>(primeira.Id);
        removida.IsDeleted.Should().BeTrue();
        removida.DeletedAt.Should().NotBeNull();
        removida.DeletedBy.Should().Be(AdminB);
    }

    [Fact(DisplayName = "Soft-delete liberta o slot singleton — nova Instituição é aceita após exclusão")]
    public async Task Singleton_AposSoftDeleteNovaAceita()
    {
        await LimparInstituicoesAsync();
        Instituicao primeira = NovaInstituicao(IdentificacaoValida("9200", "INS-DEL"));
        await _fixture.GravarAsync(AdminA, primeira);
        await _fixture.RemoverAsync<Instituicao>(AdminB, primeira.Id);

        // Nova Instituição viva é aceita — o índice único parcial só vê registros vivos.
        await _fixture.ConferirQueAGravacaoEhAceitaAsync(
            AdminA,
            NovaInstituicao(IdentificacaoValida("9201", "INS-NOVA")),
            "slot liberado pelo soft-delete");
    }

    [Fact(DisplayName = "ExisteAlgumaVivaAsync reflete registros vivos e ignora os soft-deleted")]
    public async Task ExisteAlgumaVivaAsync_RefleteRegistrosVivos()
    {
        await LimparInstituicoesAsync();
        (await RepositorioAcusaInstituicaoVivaAsync()).Should().BeFalse();

        Instituicao instituicao = NovaInstituicao(IdentificacaoValida("9300", "INS-EX"));
        await _fixture.GravarAsync(AdminA, instituicao);
        (await RepositorioAcusaInstituicaoVivaAsync()).Should().BeTrue();

        await _fixture.RemoverAsync<Instituicao>(AdminA, instituicao.Id);
        (await RepositorioAcusaInstituicaoVivaAsync())
            .Should().BeFalse("registro removido não conta para o limite singleton");
    }

    [Fact(DisplayName = "FK reitoria_id aceita vínculo com Unidade do tipo reitoria")]
    public async Task Fk_UnidadeRaizReitoria_Aceita()
    {
        await LimparInstituicoesAsync();
        Unidade reitoria = NovaReitoria("reitoria-fk", "RFK", "RFK001");
        await _fixture.GravarAsync(AdminA, reitoria);

        Instituicao instituicao = NovaInstituicao(IdentificacaoValida("9400", "INS-FK"), unidadeRaizId: reitoria.Id);
        await _fixture.GravarAsync(AdminA, instituicao);

        Instituicao persistida = await _fixture.RecarregarAsync<Instituicao>(instituicao.Id);
        persistida.UnidadeRaizId.Should().Be(reitoria.Id);
    }

    [Fact(DisplayName = "ExisteComUnidadeRaizAsync reconhece só a Unidade que a Instituição tem como raiz")]
    public async Task ExisteComUnidadeRaizAsync_ReconheceSoARaizReferenciada()
    {
        await LimparInstituicoesAsync();
        Unidade reitoria = NovaReitoria("reitoria-raiz", "RRZ", "RRZ001");
        await _fixture.GravarAsync(AdminA, reitoria);
        await _fixture.GravarAsync(AdminA, NovaInstituicao(IdentificacaoValida("9401", "INS-RZ"), unidadeRaizId: reitoria.Id));

        await using UniPlusDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var repository = new InstituicaoRepository(readCtx);
        (await repository.ExisteComUnidadeRaizAsync(reitoria.Id, CancellationToken.None))
            .Should().BeTrue("a Instituição referencia esta Unidade como raiz");
        (await repository.ExisteComUnidadeRaizAsync(Guid.CreateVersion7(), CancellationToken.None))
            .Should().BeFalse("nenhuma Instituição referencia uma Unidade aleatória");
    }

    [Fact(DisplayName = "Insert com endereço estruturado faz round-trip das colunas owned")]
    public async Task Insert_ComEndereco_RoundTrip()
    {
        await LimparInstituicoesAsync();
        Instituicao instituicao = NovaInstituicao(
            IdentificacaoValida("9600", "INS-END"),
            MarabaPeloGeo,
            EnderecoCoerente());
        await _fixture.GravarAsync(AdminA, instituicao);

        Instituicao persistida = await _fixture.RecarregarAsync<Instituicao>(instituicao.Id);
        persistida.Endereco.Should().NotBeNull();
        persistida.Endereco!.Cep.Should().Be("68507590");
        persistida.Endereco.CidadeCodigoIbge.Should().Be("1504208");
    }

    [Fact(DisplayName = "Owned type opcional: Instituição sem endereço materializa Endereco nulo")]
    public async Task Insert_SemEndereco_MaterializaNulo()
    {
        await LimparInstituicoesAsync();
        Instituicao instituicao = NovaInstituicao(IdentificacaoValida("9601", "INS-SEM-END"));
        await _fixture.GravarAsync(AdminA, instituicao);

        Instituicao persistida = await _fixture.RecarregarAsync<Instituicao>(instituicao.Id);
        persistida.Endereco.Should().BeNull();
    }

    [Fact(DisplayName = "CHECK de coerência rejeita UPDATE cru com cidade do endereço divergente")]
    public async Task CheckCoerencia_RejeitaUpdateCruIncoerente()
    {
        await LimparInstituicoesAsync();
        Instituicao instituicao = NovaInstituicao(
            IdentificacaoValida("9602", "INS-CK"),
            MarabaPeloGeo,
            EnderecoCoerente());
        await _fixture.GravarAsync(AdminA, instituicao);

        await _fixture.ConferirQueOBancoRecusaPorCheckAsync(
            $"UPDATE organizacao.instituicoes SET endereco_cidade_codigo_ibge = '1501402' WHERE id = {instituicao.Id}",
            EnderecoGeoOwnedConfiguration.CoerenciaCidadeCheckName("instituicoes"),
            "o CHECK ck_instituicoes_endereco_cidade_coerente impede divergência cidade↔CEP");
    }

    [Fact(DisplayName = "CHECK rejeita UPDATE cru que zera a cidade da sede mantendo o endereço")]
    public async Task CheckCidadeObrigatoria_RejeitaEnderecoSemCidade()
    {
        await LimparInstituicoesAsync();
        Instituicao instituicao = NovaInstituicao(
            IdentificacaoValida("9603", "INS-OBR"),
            MarabaPeloGeo,
            EnderecoCoerente());
        await _fixture.GravarAsync(AdminA, instituicao);

        // Zera a cidade da sede mantendo o endereço (endereco_cep não-nulo) — o
        // domínio recusa (CidadeObrigatoriaComEndereco); o CHECK protege escrita crua.
        await _fixture.ConferirQueOBancoRecusaPorCheckAsync(
            $"UPDATE organizacao.instituicoes SET cidade_codigo_ibge = NULL, cidade_nome = NULL, cidade_uf = NULL WHERE id = {instituicao.Id}",
            "ck_instituicoes_cidade_obrigatoria_com_endereco",
            "o CHECK ck_instituicoes_cidade_obrigatoria_com_endereco exige cidade da sede quando há endereço");
    }

    [Fact(DisplayName = "Completude: CHECK rejeita UPDATE cru que deixa o trio de cidade da sede parcial")]
    public async Task CheckCidadeCompleta_RejeitaTrioParcial()
    {
        await LimparInstituicoesAsync();
        Instituicao instituicao = NovaInstituicao(IdentificacaoValida("9604", "INS-TRIO"), MarabaPeloGeo);
        await _fixture.GravarAsync(AdminA, instituicao);

        // Zera só o nome da cidade, deixando código/UF — trio parcial. O domínio
        // trata a cidade como all-or-nothing; o CHECK protege a escrita crua.
        await _fixture.ConferirQueOBancoRecusaPorCheckAsync(
            $"UPDATE organizacao.instituicoes SET cidade_nome = NULL WHERE id = {instituicao.Id}",
            "ck_instituicoes_cidade_completa",
            "o CHECK ck_instituicoes_cidade_completa exige o trio de cidade completo ou ausente");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Estado compartilhado pela IClassFixture + singleton: cada teste parte sem
    /// Instituição viva, para que a suíte não dependa da ordem de execução dos testes.
    /// </summary>
    private async Task LimparInstituicoesAsync()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(AdminA);
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM organizacao.instituicoes");
    }

    /// <summary>O que o repositório responde, num contexto limpo, sobre haver Instituição viva.</summary>
    private async Task<bool> RepositorioAcusaInstituicaoVivaAsync()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        return await new InstituicaoRepository(ctx).ExisteAlgumaVivaAsync(CancellationToken.None);
    }

    private static IdentificacaoRegulatoriaDaInstituicao IdentificacaoValida(string codigoEmec, string sigla) =>
        new(codigoEmec, $"Instituição {sigla}", sigla, "Universidade", "Pública Federal");

    private static Instituicao NovaInstituicao(
        IdentificacaoRegulatoriaDaInstituicao identificacao,
        CidadeDaSede cidade = default,
        ReferenciaEnderecoGeo? endereco = null,
        Guid? unidadeRaizId = null) =>
        Instituicao.Criar(
            identificacao,
            cidade,
            endereco,
            unidadeRaizId).Value!;

    private static ReferenciaEnderecoGeo EnderecoCoerente() =>
        ReferenciaEnderecoGeo.Criar(
            "68507590", "Folha 31", "s/n", null, "Nova Marabá", null,
            "1504208", "Marabá", "PA", -5.3m, -49.1m,
            NivelResolucaoEndereco.Logradouro, "logradouro",
            new DateTimeOffset(2026, 6, 22, 17, 10, 0, TimeSpan.Zero)).Value!;

    private static Unidade NovaReitoria(string slug, string sigla, string codigo) =>
        Unidade.Criar(
            "Reitoria",
            new IdentificadoresDaUnidade(slug, sigla, codigo, null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(DataInicio, null),
            null,
            TipoUnidade.Reitoria,
            false).Value!;
}
