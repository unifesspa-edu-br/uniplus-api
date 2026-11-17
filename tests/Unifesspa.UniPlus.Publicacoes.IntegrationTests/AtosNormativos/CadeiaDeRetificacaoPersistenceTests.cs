namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;


using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Application.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Integração da cadeia de retificação do ato (ADR-0103) contra Postgres real: o par (ato retificado,
/// motivo), a chave estrangeira para o próprio ato, a linearidade da cadeia por índice único parcial,
/// a travessia da linhagem e os CHECKs que barram a retificação malformada por insert cru.
/// </summary>
[Collection(PublicacoesDbCollection.Name)]
public sealed class CadeiaDeRetificacaoPersistenceTests
{
    private const string CheckViolation = "23514";
    private const string UniqueViolation = "23505";
    private const string ForeignKeyViolation = "23503";
    private static readonly AtoNormativoDeTeste Base = new();

    private readonly PublicacoesDbFixture _fixture;
    private readonly AtoNormativoPersistenciaApoio _apoio;

    public CadeiaDeRetificacaoPersistenceTests(PublicacoesDbFixture fixture)
    {
        _fixture = fixture;
        _apoio = new AtoNormativoPersistenciaApoio(_fixture);
    }

    [Fact(DisplayName = "Retificação persiste e relê o par (ato retificado, motivo) e o snapshot unico_por_objeto")]
    public async Task Insert_Retificacao_PersisteCampos()
    {
        AtoNormativo raiz = Base.Registrar();
        AtoNormativo retificador = Retificando(raiz, "corrige o anexo II");
        await _apoio.GravarAsync(raiz, retificador);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        AtoNormativo persistido = await ctx.AtosNormativos.SingleAsync(a => a.Id == retificador.Id);

        persistido.AtoRetificadoId.Should().Be(raiz.Id);
        persistido.MotivoRetificacao.Should().Be("corrige o anexo II");
        persistido.UnicoPorObjeto.Should().BeFalse();
    }

    [Fact(DisplayName = "FK self-ref recusa retificação a um ato inexistente")]
    public async Task Insert_RetificaInexistente_ViolaFk()
    {
        AtoNormativo retificador = (Base with { AtoRetificadoId = Guid.CreateVersion7(), MotivoRetificacao = "corrige" }).Registrar();

        Func<Task> gravar = () => _apoio.GravarAsync(retificador);

        DbUpdateException ex = (await gravar.Should().ThrowAsync<DbUpdateException>()).Which;
        (ex.InnerException as PostgresException)!.SqlState.Should().Be(ForeignKeyViolation);
    }

    /// <remarks>
    /// O nome da constraint violada é exatamente o que <c>ConflitoNaGravacaoDoAto</c> procura para traduzir
    /// a corrida check-then-act em <c>AtoJaRetificado</c> (409).
    /// </remarks>
    [Fact(DisplayName = "Índice único parcial recusa retificar o mesmo ato duas vezes (cadeia linear)")]
    public async Task Insert_RetificaDuasVezes_ViolaUnique()
    {
        AtoNormativo raiz = Base.Registrar();
        await _apoio.GravarAsync(raiz, Retificando(raiz, "primeira"));

        Func<Task> segunda = () => _apoio.GravarAsync(Retificando(raiz, "segunda"));

        DbUpdateException ex = (await segunda.Should().ThrowAsync<DbUpdateException>()).Which;
        PostgresException pg = (ex.InnerException as PostgresException)!;
        pg.SqlState.Should().Be(UniqueViolation);
        pg.ConstraintName.Should().Be(RestricoesDoBanco.AtoNormativoRetificadoUnico);
    }

    /// <remarks>R2 emenda R1, a cabeça da cadeia; R1 ainda não foi retificado, então passa.</remarks>
    [Fact(DisplayName = "Duas retificações empilham na cabeça: R2 retifica R1 (não a raiz), aceito")]
    public async Task Insert_EmpilhaNaCabeca_Aceito()
    {
        AtoNormativo raiz = Base.Registrar();
        AtoNormativo r1 = Retificando(raiz, "primeira");
        await _apoio.GravarAsync(raiz, r1);

        Func<Task> r2 = () => _apoio.GravarAsync(Retificando(r1, "segunda"));

        await r2.Should().NotThrowAsync();
    }

    /// <remarks>A partir da raiz, do meio ou da cabeça, a cadeia inteira é devolvida.</remarks>
    [Fact(DisplayName = "ListarIdsDaCadeia devolve a linhagem inteira a partir de qualquer elo (A→B→C)")]
    public async Task ListarIdsDaCadeia_DevolveLinhagemInteira()
    {
        AtoNormativo a = Base.Registrar();
        AtoNormativo b = Retificando(a, "segunda versão");
        AtoNormativo c = Retificando(b, "terceira versão");
        await _apoio.GravarAsync(a, b, c);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repo = new AtoNormativoRepository(ctx);

        Guid[] esperada = [a.Id, b.Id, c.Id];
        (await repo.ListarIdsDaCadeiaAsync(a.Id, default)).Should().BeEquivalentTo(esperada);
        (await repo.ListarIdsDaCadeiaAsync(b.Id, default)).Should().BeEquivalentTo(esperada);
        (await repo.ListarIdsDaCadeiaAsync(c.Id, default)).Should().BeEquivalentTo(esperada);
    }

    [Fact(DisplayName = "ListarIdsDaCadeia de um ato sem retificação devolve só ele")]
    public async Task ListarIdsDaCadeia_SemRetificacao_SoOProprio()
    {
        AtoNormativo solo = (Base with { Numero = "77" }).Registrar();
        await _apoio.GravarAsync(solo);

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        var repo = new AtoNormativoRepository(ctx);

        (await repo.ListarIdsDaCadeiaAsync(solo.Id, default))
            .Should().ContainSingle().Which.Should().Be(solo.Id);
    }

    [Fact(DisplayName = "CHECK recusa retificação incompleta (só ato retificado, insert cru)")]
    public async Task CheckRetificacaoCompleta_RecusaSoAto()
    {
        AtoNormativo raiz = Base.Registrar();
        await _apoio.GravarAsync(raiz);

        Func<Task> insercao = () => _apoio.InserirCruAsync(new LinhaCruaDoAto { AtoRetificadoId = raiz.Id, MotivoRetificacao = null });
        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    [Fact(DisplayName = "CHECK recusa retificação incompleta (só motivo, insert cru)")]
    public async Task CheckRetificacaoCompleta_RecusaSoMotivo()
    {
        Func<Task> insercao = () => _apoio.InserirCruAsync(new LinhaCruaDoAto { AtoRetificadoId = null, MotivoRetificacao = "corrige" });
        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    /// <remarks>
    /// Motivo presente porém sem conteúdo: uma retificação sem razão registrada, que a factory normaliza
    /// para ausente e o CHECK (btrim) barra no insert cru.
    /// </remarks>
    [Theory(DisplayName = "CHECK recusa retificação com motivo em branco (vazio ou só espaços, insert cru)")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CheckRetificacaoCompleta_RecusaMotivoEmBranco(string motivo)
    {
        AtoNormativo raiz = Base.Registrar();
        await _apoio.GravarAsync(raiz);

        Func<Task> insercao = () => _apoio.InserirCruAsync(new LinhaCruaDoAto { AtoRetificadoId = raiz.Id, MotivoRetificacao = motivo });
        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    /// <remarks>O id e o ato retificado são o mesmo, na mesma linha: a auto-retificação que o CHECK barra.</remarks>
    [Fact(DisplayName = "CHECK recusa auto-retificação (id = ato_retificado_id, insert cru)")]
    public async Task CheckNaoAutorretifica_RecusaSelfRef()
    {
        Guid id = Guid.CreateVersion7();

        Func<Task> insercao = () => _apoio.InserirCruAsync(
            new LinhaCruaDoAto { Id = id, AtoRetificadoId = id, MotivoRetificacao = "corrige" });
        (await insercao.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(CheckViolation);
    }

    private static AtoNormativo Retificando(AtoNormativo retificado, string motivo) =>
        (Base with { AtoRetificadoId = retificado.Id, MotivoRetificacao = motivo }).Registrar();
}
