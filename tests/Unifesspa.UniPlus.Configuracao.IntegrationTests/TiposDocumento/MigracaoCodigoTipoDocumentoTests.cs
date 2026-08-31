namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposDocumento;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

/// <summary>
/// Prova que a migration que fecha o formato do código atravessa um banco que já
/// tem dado gravado sob a regra antiga — e não um banco vazio, que é o estado em
/// que a fixture compartilhada sempre nasce.
/// </summary>
/// <remarks>
/// <para>É a ordem que está sob teste. O conversor recusa reidratar código fora do
/// formato, e o CHECK recusa mantê-lo na tabela: se a reescrita dos dados não
/// acontecesse antes das alterações de schema, a própria migration abortaria com
/// <c>23514</c>, e um deploy que passasse dela deixaria toda leitura do cadastro
/// respondendo <c>500</c> por causa de uma única linha.</para>
/// <para>As linhas semeadas reproduzem o cadastro real de homologação, que foi
/// preenchido com códigos sequenciais, incluindo uma soft-deleted: o índice único
/// parcial a ignora, mas o CHECK e o <c>NOT NULL</c> valem para a tabela inteira.</para>
/// <para>O que sobra na tabela ao fim não é vazio: a carga do catálogo consolidado
/// roda numa migration posterior e repovoa o cadastro. O que este teste exige é que
/// nenhum código sequencial atravesse — e que a leitura funcione, o que a própria
/// materialização das entidades comprova, já que o conversor recusaria qualquer
/// linha fora do formato.</para>
/// </remarks>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo de teste; os valores interpolados são Guid gerados localmente.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Recursos liberados por IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class MigracaoCodigoTipoDocumentoTests : IAsyncLifetime
{
    private const string MigrationAnterior = "20260831002351_RemoveDominioFechadoDaCategoriaDoTipoDocumento";

    private const string MigrationQueFechaOFormato = "20260831180217_MigraCodigoTipoDocumentoParaFormatoFechado";

    private static readonly string[] CodigosSequenciais = ["01", "08", "16", "99"];

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_migracao_codigo_tipo_documento_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync().ConfigureAwait(false);

    public async Task DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);

    [Fact(DisplayName = "Migration atravessa cadastro com códigos sequenciais e deixa a leitura sã")]
    public async Task Migration_ComCodigoLegado_LimpaAntesDeFecharOFormato()
    {
        await using (ConfiguracaoDbContext contextoLegado = CriarContexto())
        {
            IMigrator migrator = contextoLegado.GetService<IMigrator>();
            await migrator.MigrateAsync(MigrationAnterior);
        }

        await SemearCodigosSequenciaisAsync();

        await using (ConfiguracaoDbContext contextoNovo = CriarContexto())
        {
            Func<Task> migrar = async () => await contextoNovo.Database.MigrateAsync();

            await migrar.Should().NotThrowAsync(
                "a reescrita dos dados precede as alterações de schema — sem ela o CHECK de formato abortaria a migration");
        }

        await using (ConfiguracaoDbContext contextoLeitura = CriarContexto())
        {
            List<TipoDocumento> lidos = await contextoLeitura.TiposDocumento
                .AsNoTracking()
                .IgnoreQueryFilters()
                .ToListAsync();

            string[] sobreviventes = [.. lidos
                .Select(t => t.Codigo.Valor)
                .Where(codigo => CodigosSequenciais.Contains(codigo))];

            sobreviventes.Should().BeEmpty(
                "nenhum código sequencial sobrevive — nem o soft-deleted, que o índice único ignora mas o CHECK não");

            // A leitura sã é o desfecho que importa: se qualquer linha tivesse ficado
            // fora do formato, a materialização acima teria estourado no conversor
            // antes de chegar a esta asserção, e nenhum código sobrevivente seria
            // sequer listado.
            //
            // Conferir o catálogo inteiro, e não apenas que sobrou alguma coisa: um
            // erro que deixasse passar só parte da carga produziria uma tabela não
            // vazia e igualmente errada.
            string[] esperados = [.. TipoDocumentoSeed.Itens.Select(item => item.Codigo).Order(StringComparer.Ordinal)];
            string[] presentes = [.. lidos.Select(t => t.Codigo.Valor).Order(StringComparer.Ordinal)];

            presentes.Should().Equal(esperados,
                "a carga do catálogo consolidado roda depois desta migration e repovoa o cadastro por inteiro");
        }
    }


    [Fact(DisplayName = "A carga do catálogo preserva o tipo que o operador já cadastrou com o mesmo código")]
    public async Task Carga_ComCodigoJaOcupado_PreservaODoOperadorESemeiaOResto()
    {
        // Um ambiente pode aplicar a migration que fecha o formato, ficar disponível, e
        // só depois receber a carga do catálogo — janela em que o operador cadastra um
        // tipo pela interface. Se ele escolher um código que o catálogo também usa, o
        // índice único parcial rejeitaria a linha do seed e a falha travaria a migração
        // inteira, e com ela o deploy. O INSERT tolerante pula a linha em conflito.
        await using (ConfiguracaoDbContext contexto = CriarContexto())
        {
            IMigrator migrator = contexto.GetService<IMigrator>();
            await migrator.MigrateAsync(MigrationQueFechaOFormato);
        }

        Guid idDoOperador = Guid.CreateVersion7();
        string codigoDisputado = TipoDocumentoSeed.Itens[0].Codigo;
        await ExecutarAsync(
            $"""
            INSERT INTO configuracao.tipo_documento
                (id, codigo, nome, categoria, created_at, is_deleted)
            VALUES ('{idDoOperador}', '{codigoDisputado}', 'Cadastrado pelo operador', 'IDENTIFICACAO', now(), false);
            """);

        await using (ConfiguracaoDbContext contexto = CriarContexto())
        {
            Func<Task> migrar = async () => await contexto.Database.MigrateAsync();
            await migrar.Should().NotThrowAsync("o INSERT da carga tolera o código já ocupado");
        }

        await using (ConfiguracaoDbContext leitura = CriarContexto())
        {
            List<TipoDocumento> todos = await leitura.TiposDocumento.AsNoTracking().ToListAsync();

            todos.Should().ContainSingle(t => t.Id == idDoOperador)
                .Which.Nome.Should().Be("Cadastrado pelo operador",
                    "o que o operador cadastrou não é sobrescrito pela carga");

            // Os outros 69 entram normalmente: a tolerância pula a linha em conflito,
            // não a carga inteira.
            int semeadosPresentes = todos.Count(t => t.Id != idDoOperador
                && TipoDocumentoSeed.Itens.Any(item => item.Id == t.Id));
            semeadosPresentes.Should().Be(TipoDocumentoSeed.Itens.Count - 1);
        }
    }

    private async Task ExecutarAsync(string sql)
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        await comando.ExecuteNonQueryAsync();
    }

    private ConfiguracaoDbContext CriarContexto()
    {
        DbContextOptions<ConfiguracaoDbContext> options = new DbContextOptionsBuilder<ConfiguracaoDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ConfiguracaoDbContext(options);
    }

    private async Task SemearCodigosSequenciaisAsync()
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();

        (string Codigo, string Nome, string Categoria, bool Excluido)[] legado =
        [
            ("01", "Certificado", "ESCOLARIDADE", false),
            ("08", "LAUDO MÉDICO", "SAUDE", false),
            ("16", "RG", "IDENTIFICACAO", false),
            ("99", "Tipo abandonado", "OUTROS", true),
        ];

        foreach ((string codigo, string nome, string categoria, bool excluido) in legado)
        {
            await using NpgsqlCommand comando = new(
                $$"""
                INSERT INTO configuracao.tipo_documento
                    (id, codigo, nome, categoria, created_at, is_deleted)
                VALUES ('{{Guid.CreateVersion7()}}', '{{codigo}}', '{{nome}}', '{{categoria}}', now(), {{excluido.ToString().ToUpperInvariant()}});
                """,
                conexao);
            await comando.ExecuteNonQueryAsync();
        }
    }
}
