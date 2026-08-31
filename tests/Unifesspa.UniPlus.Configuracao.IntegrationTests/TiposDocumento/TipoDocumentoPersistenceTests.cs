namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposDocumento;

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
/// Integração ponta-a-ponta do TipoDocumento contra Postgres real (UNI-REQ-0013):
/// persistência, UNIQUE parcial do código vivo, liberação do slot por soft-delete,
/// CHECK de auto-equivalência, ausência de domínio fechado na categoria — que agora
/// é código de cadastro —, não-bloqueio de remoção de um tipo apontado como
/// equivalente, e leitura cross-módulo (CA-01, CA-02, CA-04).
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TipoDocumentoPersistenceTests
{
    private const string AdminA = "admin-a";
    private const string AdminB = "admin-b";

    private readonly ConfiguracaoDbFixture _fixture;

    public TipoDocumentoPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-01: criar persiste os campos e fica visível pelo leitor cross-módulo")]
    public async Task Insert_PersisteEFicaVisivelPeloReader()
    {
        string codigo = CodigoUnico();
        TipoDocumento tipo = Novo(codigo, categoria: "SAUDE");

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TiposDocumento.Add(tipo);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        TipoDocumento persistido = await readCtx.TiposDocumento.SingleAsync(t => t.Id == tipo.Id);

        persistido.Codigo.Valor.Should().Be(codigo);
        persistido.Nome.Should().Be("Laudo médico");
        persistido.Categoria.Should().Be("SAUDE");
        persistido.CreatedBy.Should().Be(AdminA);
        persistido.IsDeleted.Should().BeFalse();

        var reader = new TipoDocumentoReader(readCtx);
        TipoDocumentoView? view = await reader.ObterPorIdAsync(tipo.Id);
        view.Should().NotBeNull();
        view!.Codigo.Should().Be(codigo);
        view.Categoria.Should().Be("SAUDE");
    }

    [Fact(DisplayName = "CA-02: UNIQUE parcial do código rejeita segundo tipo vivo com mesmo código")]
    public async Task UniquePartial_Codigo_RejeitaDuplicataAtiva()
    {
        string codigo = CodigoUnico();
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TiposDocumento.Add(Novo(codigo));
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext ctx2 = _fixture.CreateDbContext(AdminA);
        ctx2.TiposDocumento.Add(Novo(codigo));

        Func<Task> act = async () => await ctx2.SaveChangesAsync();

        // Trava as constantes que o handler usa para traduzir a corrida concorrente
        // (UniqueConstraintViolation.GetViolatedConstraint/IsCodigoConflict) em
        // CodigoJaExiste/409: SqlState 23505 + nome do índice único parcial.
        DbUpdateException ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        Npgsql.PostgresException pg = ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which;
        pg.SqlState.Should().Be("23505");
        pg.ConstraintName.Should().Be("ix_tipo_documento_codigo_vivo");
    }

    [Fact(DisplayName = "Código distinto é aceito")]
    public async Task CodigoDistinto_Aceita()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA);
        ctx.TiposDocumento.Add(Novo(CodigoUnico()));
        ctx.TiposDocumento.Add(Novo(CodigoUnico()));

        Func<Task> act = async () => await ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync("os códigos são distintos");
    }

    [Fact(DisplayName = "CA-04: soft-delete preserva a trilha e libera o slot da UNIQUE parcial do código")]
    public async Task SoftDelete_PreservaTrilhaELibertaSlot()
    {
        string codigo = CodigoUnico();
        TipoDocumento tipo = Novo(codigo);
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TiposDocumento.Add(tipo);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            TipoDocumento tracked = await ctx.TiposDocumento.SingleAsync(t => t.Id == tipo.Id);
            ctx.TiposDocumento.Remove(tracked);
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null))
        {
            TipoDocumento excluido = await ctx.TiposDocumento
                .IgnoreQueryFilters().SingleAsync(t => t.Id == tipo.Id);
            excluido.IsDeleted.Should().BeTrue();
            excluido.DeletedBy.Should().Be(AdminB);
        }

        await using ConfiguracaoDbContext ctx3 = _fixture.CreateDbContext(AdminA);
        ctx3.TiposDocumento.Add(Novo(codigo));

        Func<Task> act = async () => await ctx3.SaveChangesAsync();
        await act.Should().NotThrowAsync("o slot do código foi liberado pelo soft-delete");
    }

    [Fact(DisplayName = "CA-04: remover um tipo apontado como equivalente por outro vivo NÃO é bloqueado")]
    public async Task SoftDelete_TipoApontadoComoEquivalente_NaoBloqueia()
    {
        string codigoCin = CodigoUnico();
        string codigoRg = CodigoUnico();

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            // CIN existe; RG aponta CIN como equivalente (rótulo classificatório, sem FK).
            ctx.TiposDocumento.Add(Novo(codigoCin, categoria: "IDENTIFICACAO"));
            ctx.TiposDocumento.Add(Novo(codigoRg, categoria: "IDENTIFICACAO", tipoEquivalente: codigoCin));
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            CodigoTipoDocumento cinVo = Vo(codigoCin);
            TipoDocumento cin = await ctx.TiposDocumento.SingleAsync(t => t.Codigo == cinVo);
            ctx.TiposDocumento.Remove(cin);
            Func<Task> act = async () => await ctx.SaveChangesAsync();
            await act.Should().NotThrowAsync("tipo_equivalente é rótulo, não FK — a remoção não é bloqueada");
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        CodigoTipoDocumento rgVo = Vo(codigoRg);
        TipoDocumento rg = await readCtx.TiposDocumento.SingleAsync(t => t.Codigo == rgVo);
        rg.TipoEquivalente.Should().Be(codigoCin, "o rótulo permanece, agora apontando para um código sem alvo vivo");
    }

    [Fact(DisplayName = "Banco aceita categoria fora dos sete tokens antigos — o domínio fechado saiu do schema")]
    public async Task Banco_AceitaCategoriaForaDoRosterAntigo()
    {
        Guid id = Guid.CreateVersion7();
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.tipo_documento (id, codigo, nome, categoria, created_at, is_deleted) VALUES ({id}, {CodigoUnico()}, {"X"}, {"DOCUMENTO_MILITAR"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().NotThrowAsync(
            "a categoria virou código de cadastro: um CHECK preso aos sete tokens do enum recusaria "
            + "qualquer categoria que o CEPS criasse");

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        TipoDocumento persistido = await readCtx.TiposDocumento.SingleAsync(t => t.Id == id);
        persistido.Categoria.Should().Be("DOCUMENTO_MILITAR",
            "a reidratação não passa mais por conversor de enum, que falharia rápido neste valor");
    }

    [Fact(DisplayName = "CHECK de banco rejeita categoria fora do formato de código via SQL cru")]
    public async Task Check_RejeitaCategoriaForaDoFormatoViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.tipo_documento (id, codigo, nome, categoria, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"renda familiar"}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o domínio fechado saiu, mas a forma continua protegida: sem conversor de enum, "
            + "uma categoria malformada gravada por fora chegaria ao snapshot de Seleção");
    }

    [Fact(DisplayName = "Coluna comporta código de categoria no tamanho máximo do cadastro")]
    public async Task Coluna_ComportaCodigoDeCategoriaNoTamanhoMaximo()
    {
        // O cadastro de categorias aceita até 50 caracteres; a coluna nasceu com 30,
        // dimensionada para os sete tokens do enum. Sem a ampliação, uma categoria
        // legítima de 31+ caracteres viraria erro de banco em vez de cadastro aceito.
        string categoriaLonga = "A" + new string('X', 49);
        Guid id = Guid.CreateVersion7();

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.tipo_documento (id, codigo, nome, categoria, created_at, is_deleted) VALUES ({id}, {CodigoUnico()}, {"X"}, {categoriaLonga}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "CHECK de banco rejeita tipo_equivalente igual ao código via SQL cru")]
    public async Task Check_RejeitaEquivalenteIgualCodigoViaSqlCru()
    {
        string codigo = CodigoUnico();
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.tipo_documento (id, codigo, nome, categoria, tipo_equivalente, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {codigo}, {"X"}, {"OUTROS"}, {codigo}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK tipo_equivalente <> codigo impede o INSERT direto");
    }

    [Fact(DisplayName = "CHECK de banco rejeita tamanho_maximo_mb não-positivo via SQL cru")]
    public async Task Check_RejeitaTamanhoMaximoNaoPositivoViaSqlCru()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"INSERT INTO configuracao.tipo_documento (id, codigo, nome, categoria, tamanho_maximo_mb, created_at, is_deleted) VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"OUTROS"}, {0}, {DateTimeOffset.UtcNow}, {false})");

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK tamanho_maximo_mb > 0 impede o INSERT direto");
    }

    [Fact(DisplayName = "Reader.ListarVivosAsync ordena por código e exclui soft-deleted")]
    public async Task ListarVivos_OrdenaPorCodigoEExcluiSoftDeleted()
    {
        string prefixo = $"DOC_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
        string codA = $"{prefixo}_A";
        string codB = $"{prefixo}_B";
        string codExcluido = $"{prefixo}_D";

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TiposDocumento.Add(Novo(codB));
            ctx.TiposDocumento.Add(Novo(codA));
            ctx.TiposDocumento.Add(Novo(codExcluido));
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            CodigoTipoDocumento excluidoVo = Vo(codExcluido);
            TipoDocumento aExcluir = await ctx.TiposDocumento.SingleAsync(t => t.Codigo == excluidoVo);
            ctx.TiposDocumento.Remove(aExcluir);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new TipoDocumentoReader(readCtx);
        IReadOnlyList<TipoDocumentoView> todos = await reader.ListarVivosAsync();

        string[] meus = [.. todos
            .Select(v => v.Codigo)
            .Where(c => c.StartsWith(prefixo, StringComparison.Ordinal))];

        meus.Should().Equal([codA, codB]);
    }

    [Fact(DisplayName = "Reader.ObterVivoPorCodigoAsync não devolve tipo removido, e normaliza o código buscado")]
    public async Task ObterVivoPorCodigo_ExcluiSoftDeletedENormalizaBusca()
    {
        // A regra legal referencia o tipo de documento por código, e quem valida a referência
        // é este leitor. Se um tipo removido continuasse respondendo, a regra que o exige
        // seria aceita e o edital publicaria sob exigência de um documento que saiu do
        // catálogo. O espaço supérfluo entra pelo mesmo caminho: quem digita " CODIGO " no
        // cadastro da regra precisa encontrar o mesmo registro, ou a validação recusa o que
        // existe.
        string codigoVivo = CodigoUnico();
        string codigoRemovido = CodigoUnico();

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            ctx.TiposDocumento.Add(Novo(codigoVivo));
            ctx.TiposDocumento.Add(Novo(codigoRemovido));
            await ctx.SaveChangesAsync();
        }

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminB))
        {
            CodigoTipoDocumento removidoVo = Vo(codigoRemovido);
            TipoDocumento aExcluir = await ctx.TiposDocumento.SingleAsync(t => t.Codigo == removidoVo);
            ctx.TiposDocumento.Remove(aExcluir);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        var reader = new TipoDocumentoReader(readCtx);

        TipoDocumentoView? vivo = await reader.ObterVivoPorCodigoAsync(codigoVivo);
        TipoDocumentoView? comEspaco = await reader.ObterVivoPorCodigoAsync($"  {codigoVivo} ");
        TipoDocumentoView? removido = await reader.ObterVivoPorCodigoAsync(codigoRemovido);

        vivo.Should().NotBeNull();
        vivo!.Codigo.Should().Be(codigoVivo);
        comEspaco.Should().NotBeNull("o leitor apara o código antes de comparar");
        comEspaco!.Id.Should().Be(vivo.Id);
        removido.Should().BeNull("o filtro global de soft-delete tira o tipo removido do cadastro vivo");
    }

    [Fact(DisplayName = "CHECK de formato do código rejeita insert cru fora do padrão canônico")]
    public async Task CheckConstraint_Codigo_RejeitaFormatoInvalido()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA);

        // Bypassa o value object de propósito: o CHECK é defesa em profundidade
        // contra escrita fora do fluxo da aplicação. Sem ele, uma única linha com
        // código sequencial derrubaria toda leitura da tabela na reidratação.
        Func<Task> act = async () => await ctx.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO configuracao.tipo_documento
                (id, codigo, nome, categoria, created_at, is_deleted)
            VALUES ({0}, '01', 'Certificado', 'ESCOLARIDADE', now(), false)
            """,
            Guid.CreateVersion7());

        Npgsql.PostgresException pg = (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        pg.SqlState.Should().Be("23514", "violação de CHECK constraint");
        pg.ConstraintName.Should().Be("ck_tipo_documento_codigo_formato");
    }

    [Fact(DisplayName = "Código sobrevive ao round-trip pelo banco como value object")]
    public async Task Codigo_RoundTrip_PreservaValueObject()
    {
        string codigo = CodigoUnico();
        Guid id;

        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(AdminA))
        {
            TipoDocumento tipo = Novo(codigo);
            id = tipo.Id;
            ctx.TiposDocumento.Add(tipo);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext readCtx = _fixture.CreateDbContext(userId: null);
        TipoDocumento lido = await readCtx.TiposDocumento.AsNoTracking().SingleAsync(t => t.Id == id);

        lido.Codigo.Should().Be(CodigoTipoDocumento.Criar(codigo).Value!,
            "o conversor devolve o value object, não a string crua");
        lido.Codigo.Valor.Should().Be(codigo);
    }

    [Fact(DisplayName = "Ordenação por código é traduzida para SQL, sem avaliação no cliente")]
    public async Task OrdenacaoPorCodigo_TraduzParaSql()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        // A tradução do OrderBy sobre a propriedade convertida é o que sustenta a
        // listagem do leitor cross-módulo; se o EF não traduzisse, o provider
        // lançaria em vez de ordenar.
        Func<Task> act = async () => await ctx.TiposDocumento
            .AsNoTracking()
            .OrderBy(t => t.Codigo)
            .Take(1)
            .ToListAsync();

        await act.Should().NotThrowAsync();
    }

    [Theory(DisplayName = "Busca por código fora do formato responde não-encontrado sem consultar o banco")]
    [InlineData("01")]
    [InlineData("laudo_medico")]
    [InlineData("LAUDO-MEDICO")]
    public async Task ObterVivoPorCodigo_ForaDoFormato_DevolveNulo(string codigoInvalido)
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        TipoDocumentoReader reader = new(ctx);

        TipoDocumentoView? encontrado = await reader.ObterVivoPorCodigoAsync(codigoInvalido);

        encontrado.Should().BeNull(
            "nenhum registro pode ter esse código — converter o valor para comparar estouraria no conversor");
    }

    private static TipoDocumento Novo(
        string codigo,
        string categoria = "SAUDE",
        string? tipoEquivalente = null) =>
        TipoDocumento.Criar(codigo, "Laudo médico", null, categoria, "pdf,jpg", 10, tipoEquivalente).Value!;

    private static string CodigoUnico() => $"DOC_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

    // A comparação em LINQ é entre value objects: o conversor traduz o lado da
    // coluna para varchar, mas `t.Codigo.Valor` não tem tradução e cairia em
    // avaliação no cliente.
    private static CodigoTipoDocumento Vo(string codigo) => CodigoTipoDocumento.Criar(codigo).Value!;
}
