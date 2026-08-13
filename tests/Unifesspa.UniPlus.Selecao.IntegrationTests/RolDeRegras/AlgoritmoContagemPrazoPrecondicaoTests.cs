namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

/// <summary>
/// Prova executável da precondição de migration da fronteira append-only do
/// <c>rol_de_regras</c> (ADR-0112): substituir ou remover uma entrada
/// referenciada por configuração congelada aborta ANTES de alterar qualquer
/// linha. O SQL exercitado é o mesmo do <c>Down</c> da migration
/// <c>AddAlgoritmosContagemPrazo</c>. A classe tem fixture próprio porque
/// fabrica <see cref="VersaoConfiguracao"/> — append-only por trigger,
/// impossível de remover depois — e não pode contaminar as asserções de
/// contagem das demais classes do rol.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo escrito no próprio teste, sem valor externo interpolado.")]
public sealed class AlgoritmoContagemPrazoPrecondicaoTests : IClassFixture<RegraCatalogoDbFixture>
{
    private readonly RegraCatalogoDbFixture _fixture;

    public AlgoritmoContagemPrazoPrecondicaoTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// O mesmo bloco do <c>Down</c> da migration <c>AddAlgoritmosContagemPrazo</c>:
    /// a verificação mecânica da ADR-0112 — cruza as configurações congeladas
    /// com os códigos que a migration pretende remover e aborta diante de
    /// qualquer referência.
    /// </summary>
    private const string PrecondicaoDaMigration = """
        DO $adr0112$
        BEGIN
            IF EXISTS (
                SELECT 1
                FROM selecao.versoes_configuracao
                WHERE configuracao_congelada @? '$.** ? (@.codigo == "CONTAGEM-PRAZO-EXCLUI-DIA-INICIAL" && @.versao == "v1" && exists(@.hash))'
                   OR configuracao_congelada @? '$.** ? (@.codigo == "CONTAGEM-PRAZO-HORAS-UTEIS-DESDE-ANCORA" && @.versao == "v1" && exists(@.hash))'
            ) THEN
                RAISE EXCEPTION 'rol_de_regras: entrada de algoritmo de contagem referenciada por versão de configuração congelada; remover viola o append-only (ADR-0112)';
            END IF;
        END
        $adr0112$;
        """;

    [Fact(DisplayName = "A precondição da migration aborta diante de referência congelada — e só diante dela")]
    public async Task Precondicao_AbortaDianteDeReferenciaCongelada()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        // Sem referência alguma, a precondição passa em silêncio: reverter a
        // migration é legítimo num banco onde nenhuma configuração congelou as
        // entradas de contagem — elas ainda são vocabulário (ADR-0112).
        await ExecutarPrecondicaoAsync(context);

        // Identidade, não substring: uma configuração congelada que referencia
        // um código DIFERENTE, que apenas contém o semeado como prefixo, não
        // bloqueia a reversão.
        await FabricarConfiguracaoCongeladaAsync(
            context,
            "prefixo",
            TriplaDeReferencia(AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial + "-LEGADO", "v1"));
        await ExecutarPrecondicaoAsync(context);

        // A busca é por referência de regra — a tripla {codigo, versao, hash} —,
        // não por ocorrência do texto: o snapshot congela muitos outros objetos
        // com a chave bare `codigo` cujo valor é declarado pelo administrador, e
        // uma fase batizada com o código de um algoritmo não é uma referência a
        // ele. Casar por texto abortaria esta reversão legítima.
        await FabricarConfiguracaoCongeladaAsync(
            context,
            "fase-homonima",
            $$$"""
            {"cronograma":{"fases":[{"codigo":"{{{AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial}}}","ordem":1,"donoInstitucional":"CEPS"}]}}
            """);
        await ExecutarPrecondicaoAsync(context);

        // Referência a uma versão que este Down não remove: a v1 continua
        // removível enquanto ninguém a referenciar.
        await FabricarConfiguracaoCongeladaAsync(
            context,
            "outra-versao",
            TriplaDeReferencia(AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial, "v2"));
        await ExecutarPrecondicaoAsync(context);

        // Referência real fabricada: a partir da primeira configuração
        // congelada que referencia a entrada, ela é fato — a precondição
        // encontra a referência e aborta antes de alterar qualquer linha, seja
        // a intenção substituir, seja remover.
        await FabricarConfiguracaoCongeladaAsync(
            context,
            "referencia-real",
            TriplaDeReferencia(AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial, "v1"));

        Func<Task> reversao = () => ExecutarPrecondicaoAsync(context);

        (await reversao.Should().ThrowAsync<DbException>(
            "a precondição da migration aborta diante da referência congelada"))
            .WithMessage("*ADR-0112*");
    }

    [Fact(DisplayName = "O Down da migration carrega a precondição — o SQL provado aqui é o executado lá")]
    public void MigrationDown_CarregaAPrecondicao()
    {
        string migration = File.ReadAllText(CaminhoDaMigration());

        // Marcas que só existem na guarda do Down (o InsertData do Up também
        // cita os códigos, então as marcas incluem a sintaxe do detector).
        foreach (string marca in new[]
        {
            "$adr0112$",
            "RAISE EXCEPTION",
            """@.codigo == "CONTAGEM-PRAZO-EXCLUI-DIA-INICIAL" && @.versao == "v1" && exists(@.hash)""",
            """@.codigo == "CONTAGEM-PRAZO-HORAS-UTEIS-DESDE-ANCORA" && @.versao == "v1" && exists(@.hash)""",
        })
        {
            migration.Should().Contain(
                marca,
                "sem a precondição no Down, a prova comportamental desta classe deixaria de refletir a migration real");
        }
    }

    private static string CaminhoDaMigration([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(
            Path.GetDirectoryName(origem)!,
            "..",
            "..",
            "..",
            "src",
            "selecao",
            "Unifesspa.UniPlus.Selecao.Infrastructure",
            "Persistence",
            "Migrations",
            "20260813180249_AddAlgoritmosContagemPrazo.cs"));

    private static async Task ExecutarPrecondicaoAsync(SelecaoDbContext context)
    {
        DbConnection conexao = context.Database.GetDbConnection();
        if (conexao.State != System.Data.ConnectionState.Open)
        {
            await conexao.OpenAsync(CancellationToken.None);
        }

        await using DbCommand comando = conexao.CreateCommand();
        comando.CommandText = PrecondicaoDaMigration;
        await comando.ExecuteNonQueryAsync(CancellationToken.None);
    }

    /// <summary>
    /// Uma referência de regra como o canonicalizador a serializa: a tripla
    /// <c>{codigo, versao, hash}</c>, aninhada como fica no snapshot real.
    /// </summary>
    private static string TriplaDeReferencia(string codigo, string versao) =>
        $$$"""
        {"cronograma":{"fases":[{"regraContagem":{"codigo":"{{{codigo}}}","versao":"{{{versao}}}","hash":"{{{new string('b', 64)}}}"}}]}}
        """;

    /// <summary>
    /// Fabrica uma <see cref="VersaoConfiguracao"/> real com a configuração
    /// congelada indicada — um recorte do snapshot, não o documento completo de
    /// publicação: para a fronteira da ADR-0112 o que importa é a forma da
    /// referência dentro do jsonb congelado.
    /// </summary>
    private static async Task FabricarConfiguracaoCongeladaAsync(
        SelecaoDbContext context, string cenario, string configuracaoCongelada)
    {
        ProcessoSeletivo processo = ProcessoSeletivoPublicacaoSeeder.NovoProcessoConforme(
            $"Fronteira append-only — {cenario}");
        context.ProcessosSeletivos.Add(processo);

        byte[] canonico = Encoding.UTF8.GetBytes(configuracaoCongelada);

        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processo.Id,
            canonico,
            schemaVersion: "0.0.1-teste",
            algoritmoHash: "canonico-fabricado-em-teste",
            atoCriadorId: Guid.CreateVersion7(),
            atoCriadorHash: new string('a', 64),
            atorUsuarioSub: "teste-fronteira-adr-0112",
            instante: DateTimeOffset.UtcNow);

        context.Set<VersaoConfiguracao>().Add(versao);
        await context.SaveChangesAsync(CancellationToken.None);
    }
}
