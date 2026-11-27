namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Integração do ato normativo contra Postgres real: o append-only imposto por
/// trigger (UPDATE/DELETE crus bloqueados), os CHECKs de shape, a ausência de
/// unicidade do número, o round-trip do par por valor {id, hash} e a listagem paginada.
/// A cadeia de retificação tem a sua própria classe, <see cref="CadeiaDeRetificacaoPersistenceTests"/>.
/// </summary>
[Collection(PublicacoesDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class AtoNormativoPersistenceTests
{
    private const string HashValido = AtoNormativoDeTeste.HashValido;
    private const string CheckViolation = "23514";
    private static readonly DateOnly Publicacao = new(2026, 3, 13);
    private static readonly DateTimeOffset Registro = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);
    private static readonly AtoNormativoDeTeste Base = new();
    private static readonly ReferenciaVersaoDoObjeto Versao = ReferenciaVersaoDoObjeto.Criar(Guid.CreateVersion7(), HashValido).Value!;

    private readonly PublicacoesDbFixture _fixture;
    private readonly AtoNormativoPersistenciaApoio _apoio;

    public AtoNormativoPersistenceTests(PublicacoesDbFixture fixture)
    {
        _fixture = fixture;
        _apoio = new AtoNormativoPersistenciaApoio(_fixture);
    }

    [Fact(DisplayName = "Persiste e relê os campos do ato, com a versão do objeto por valor")]
    public async Task Insert_PersisteCampos_ComVersaoDoObjeto()
    {
        AtoNormativo ato = (Base with { VersaoDoObjeto = Versao }).Registrar();
        await _apoio.GravarAsync(ato);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        AtoNormativo persistido = await ctx.AtosNormativos.SingleAsync(a => a.Id == ato.Id);

        persistido.Orgao.Should().Be("CEPS");
        persistido.Numero.Should().Be("13");
        persistido.DocumentoHash.Should().Be(HashValido);
        persistido.PublicadoEm.Should().Be(Registro);
        persistido.VersaoDoObjeto.Should().NotBeNull();
        persistido.VersaoDoObjeto!.Id.Should().Be(Versao.Id);
        persistido.VersaoDoObjeto.Hash.Should().Be(Versao.Hash);
    }

    [Fact(DisplayName = "Ato sem versão do objeto persiste com o par ausente (owned opcional)")]
    public async Task Insert_SemVersaoDoObjeto_ParAusente()
    {
        AtoNormativo ato = (Base with { Numero = null }).Registrar();
        await _apoio.GravarAsync(ato);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        AtoNormativo persistido = await ctx.AtosNormativos.SingleAsync(a => a.Id == ato.Id);

        persistido.Numero.Should().BeNull();
        persistido.VersaoDoObjeto.Should().BeNull();
    }

    [Fact(DisplayName = "UPDATE cru é bloqueado pelo trigger append-only")]
    public async Task Update_Cru_Bloqueado()
    {
        AtoNormativo ato = Base.Registrar();
        await _apoio.GravarAsync(ato);

        Func<Task> update = () => _apoio.ExecutarAsync(
            $"UPDATE publicacoes.atos_normativos SET assinante = 'Outro' WHERE id = {ato.Id}");

        (await update.Should().ThrowAsync<PostgresException>())
            .Which.MessageText.Should().Contain("append-only");
    }

    /// <remarks>A prova de que o bloqueio é do banco: a linha continua lá.</remarks>
    [Fact(DisplayName = "DELETE cru é bloqueado pelo trigger append-only")]
    public async Task Delete_Cru_Bloqueado()
    {
        AtoNormativo ato = Base.Registrar();
        await _apoio.GravarAsync(ato);

        Func<Task> delete = () => _apoio.ExecutarAsync(
            $"DELETE FROM publicacoes.atos_normativos WHERE id = {ato.Id}");

        await delete.Should().ThrowAsync<PostgresException>();

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        (await ctx.AtosNormativos.AnyAsync(a => a.Id == ato.Id)).Should().BeTrue();
    }

    [Fact(DisplayName = "O trigger append-only existe no catálogo do banco")]
    public async Task Trigger_ExisteNoCatalogo()
    {
        string nome = await _apoio.LerValorAsync<string>(
            $"""
            SELECT tgname::text AS "Value"
            FROM pg_trigger
            WHERE tgname = 'trg_atos_normativos_somente_insercao'
              AND NOT tgisinternal
            """);

        nome.Should().Be("trg_atos_normativos_somente_insercao");
    }

    [Fact(DisplayName = "Dois atos com a mesma numeração são aceitos — número não tem unicidade")]
    public async Task Insert_MesmaNumeracao_Aceito()
    {
        await _apoio.GravarAsync(Base.Registrar());
        Func<Task> segunda = () => _apoio.GravarAsync(Base.Registrar());

        await segunda.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Três atos do mesmo certame no mesmo dia são aceitos (sem ordem total)")]
    public async Task Insert_TresNoMesmoDia_Aceito()
    {
        await _apoio.GravarAsync(
            Base.Registrar(),
            (Base with { Numero = "14" }).Registrar(),
            (Base with { Numero = "15" }).Registrar());

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        int doDia = await ctx.AtosNormativos.CountAsync(a => a.DataDoAto == Publicacao);
        doDia.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact(DisplayName = "O índice de numeração existe e NÃO é único")]
    public async Task IndiceNumeracao_ExisteENaoEUnico()
    {
        bool indisunique = await _apoio.LerValorAsync<bool>(
            $"""
            SELECT i.indisunique AS "Value"
            FROM pg_class c
            JOIN pg_index i ON i.indexrelid = c.oid
            WHERE c.relname = 'ix_atos_normativos_numeracao'
            """);

        indisunique.Should().Be(false);
    }

    [Fact(DisplayName = "CHECK recusa hash de documento fora do formato (insert cru)")]
    public async Task CheckDocumentoHash_RecusaFormatoInvalido()
    {
        Func<Task> insercao = () => _apoio.InserirCruAsync(new LinhaCruaDoAto { DocumentoHash = "nao-e-hash" });
        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    [Fact(DisplayName = "CHECK recusa par de versão incompleto (só id, insert cru)")]
    public async Task CheckVersaoCompleta_RecusaParIncompleto()
    {
        Func<Task> insercao = () => _apoio.InserirCruAsync(
            new LinhaCruaDoAto { VersaoId = Guid.CreateVersion7(), VersaoHash = null });
        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    [Fact(DisplayName = "CHECK recusa par de versão incompleto (só hash, insert cru)")]
    public async Task CheckVersaoCompleta_RecusaSoHash()
    {
        Func<Task> insercao = () => _apoio.InserirCruAsync(new LinhaCruaDoAto { VersaoId = null, VersaoHash = HashValido });
        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    [Fact(DisplayName = "CHECK recusa hash de versão fora do formato (insert cru)")]
    public async Task CheckVersaoHash_RecusaFormatoInvalido()
    {
        Func<Task> insercao = () => _apoio.InserirCruAsync(
            new LinhaCruaDoAto { VersaoId = Guid.CreateVersion7(), VersaoHash = new string('g', 64) });
        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    [Fact(DisplayName = "CHECK recusa versão com id zerado (insert cru)")]
    public async Task CheckVersaoIdNaoZero_RecusaGuidEmpty()
    {
        Func<Task> insercao = () => _apoio.InserirCruAsync(new LinhaCruaDoAto { VersaoId = Guid.Empty, VersaoHash = HashValido });
        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    [Fact(DisplayName = "CHECK recusa ano não-positivo (insert cru)")]
    public async Task CheckAno_RecusaNaoPositivo()
    {
        Func<Task> insercao = () => _apoio.InserirCruAsync(new LinhaCruaDoAto { Ano = 0 });
        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    [Fact(DisplayName = "ListarIdsComMesmaNumeracao devolve todos os atos da mesma numeração")]
    public async Task ListarIdsComMesmaNumeracao_DevolveOsDaMesmaNumeracao()
    {
        string numero = Guid.NewGuid().ToString("N")[..8];
        AtoNormativo a1 = (Base with { Numero = numero }).Registrar();
        AtoNormativo a2 = (Base with { Numero = numero }).Registrar();
        await _apoio.GravarAsync(a1, a2);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repo = new AtoNormativoRepository(ctx);

        IReadOnlyList<Guid> conflitantes = await repo.ListarIdsComMesmaNumeracaoAsync(
            new NumeracaoDoAto("CEPS", "EDITAL", 2026, numero), default);

        conflitantes.Should().BeEquivalentTo([a1.Id, a2.Id]);
    }

    [Theory(DisplayName = "ListarIdsComMesmaNumeracao deixa de fora o ato que difere em órgão, série, ano ou número")]
    [InlineData("PROEG", "EDITAL", 2026, "")]
    [InlineData("CEPS", "PORTARIA", 2026, "")]
    [InlineData("CEPS", "EDITAL", 2025, "")]
    [InlineData("CEPS", "EDITAL", 2026, "-1")]
    public async Task ListarIdsComMesmaNumeracao_DeixaDeForaOutraNumeracao(string orgao, string serie, int ano, string sufixoDoNumero)
    {
        string numero = Guid.NewGuid().ToString("N")[..8];
        AtoNormativo ato = (Base with { Numero = numero }).Registrar();
        AtoNormativo deOutraNumeracao = (Base with { Orgao = orgao, Serie = serie, Ano = ano, Numero = numero + sufixoDoNumero }).Registrar();
        await _apoio.GravarAsync(ato, deOutraNumeracao);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repo = new AtoNormativoRepository(ctx);

        IReadOnlyList<Guid> conflitantes = await repo.ListarIdsComMesmaNumeracaoAsync(
            new NumeracaoDoAto("CEPS", "EDITAL", 2026, numero), default);

        conflitantes.Should().Equal(ato.Id);
    }

    [Fact(DisplayName = "Listagem paginada por cursor devolve os atos em ordem de Id")]
    public async Task ListarPaginado_OrdenaPorId()
    {
        AtoNormativo a1 = (Base with { Numero = "201" }).Registrar();
        AtoNormativo a2 = (Base with { Numero = "202" }).Registrar();
        await _apoio.GravarAsync(a1, a2);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repo = new AtoNormativoRepository(ctx);

        (IReadOnlyList<AtoNormativo> itens, _, _) = await repo.ListarPaginadoAsync(
            afterSortKey: null, afterId: null, limit: 100, PaginationDirection.Next, default);

        itens.Select(a => a.Id).Should().ContainInOrder(
            new[] { a1.Id, a2.Id }.OrderBy(id => id));
    }

    /// <remarks>
    /// A entidade forense não é <c>EntityBase</c>; a paginação keyset (relaxada para <c>IIdentificavel</c>)
    /// precisa ordená-la e fatiá-la corretamente também no sentido Prev. Partindo do maior dos três, Prev
    /// traz os anteriores em ordem ascendente (o probe é cortado ainda em DESC e a lista revertida). O limite
    /// é alto e as asserções são relativas porque o container é compartilhado entre os testes.
    /// </remarks>
    [Fact(DisplayName = "Paginação Prev sobre a entidade forense devolve a página anterior em ordem ascendente")]
    public async Task ListarPaginado_Prev_FuncionaComEntidadeForense()
    {
        AtoNormativo[] atos = [.. new[] { "301", "302", "303" }.Select(n => (Base with { Numero = n }).Registrar())];
        await _apoio.GravarAsync(atos);
        Guid[] ordenados = [.. atos.Select(a => a.Id).Order()];

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repo = new AtoNormativoRepository(ctx);

        (IReadOnlyList<AtoNormativo> anteriores, _, _) = await repo.ListarPaginadoAsync(
            afterSortKey: CondicoesDoFiltro.Nenhuma.EmitirChave(), afterId: ordenados[2], limit: 100, PaginationDirection.Prev, default);

        IReadOnlyList<Guid> ids = [.. anteriores.Select(a => a.Id)];
        ids.Should().NotContain(ordenados[2], "Prev exclui o ponto de continuação");
        ids.Should().ContainInOrder(ordenados[0], ordenados[1]);
        ids.Should().BeInAscendingOrder();
    }
}
