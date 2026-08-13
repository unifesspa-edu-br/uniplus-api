namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Data.Common;
using System.Text;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

/// <summary>
/// Apoio comum às provas da fronteira append-only do <c>rol_de_regras</c>
/// (ADR-0112): fabricar uma configuração congelada com a forma real da
/// referência e executar a precondição que cada migration carrega no seu
/// <c>Down</c>.
/// </summary>
/// <remarks>
/// Cada migration guarda apenas as entradas que ela mesma remove, então cada
/// prova precisa do seu próprio banco: a <see cref="VersaoConfiguracao"/>
/// fabricada é append-only por gatilho e não pode ser removida depois, e uma
/// referência fabricada por uma prova bloquearia a precondição da outra. Por
/// isso as classes de teste que usam este apoio têm fixture própria.
/// </remarks>
internal static class FronteiraAppendOnlyDoRol
{
    /// <summary>
    /// Uma referência de regra como o canonicalizador a serializa: a tripla
    /// <c>{codigo, versao, hash}</c>, aninhada como fica no snapshot real.
    /// </summary>
    public static string TriplaDeReferencia(string codigo, string versao) =>
        $$$"""
        {"cronograma":{"fases":[{"regraContagem":{"codigo":"{{{codigo}}}","versao":"{{{versao}}}","hash":"{{{new string('b', 64)}}}"}}]}}
        """;

    /// <summary>
    /// Um objeto com a chave bare <c>codigo</c> de mesmo valor, sem a tripla —
    /// uma fase batizada com o código de uma regra. Homônimo, não referência.
    /// </summary>
    public static string FaseHomonima(string codigo) =>
        $$$"""
        {"cronograma":{"fases":[{"codigo":"{{{codigo}}}","ordem":1,"donoInstitucional":"CEPS"}]}}
        """;

    /// <summary>
    /// Fabrica uma <see cref="VersaoConfiguracao"/> real com a configuração
    /// congelada indicada — um recorte do snapshot, não o documento completo de
    /// publicação: para a fronteira da ADR-0112 o que importa é a forma da
    /// referência dentro do jsonb congelado.
    /// </summary>
    public static async Task FabricarConfiguracaoCongeladaAsync(
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

    /// <summary>Executa o SQL da precondição no banco do fixture.</summary>
    public static async Task ExecutarAsync(SelecaoDbContext context, string sql)
    {
        DbConnection conexao = context.Database.GetDbConnection();
        if (conexao.State != System.Data.ConnectionState.Open)
        {
            await conexao.OpenAsync(CancellationToken.None);
        }

        await using DbCommand comando = conexao.CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync(CancellationToken.None);
    }

    /// <summary>
    /// Lê o arquivo de uma migration do módulo Seleção, para provar que o SQL
    /// exercitado no teste é o que a migration realmente carrega.
    /// </summary>
    public static string LerMigration(string arquivo, string origemDoTeste) =>
        File.ReadAllText(Path.GetFullPath(Path.Join(
            Path.GetDirectoryName(origemDoTeste)!,
            "..",
            "..",
            "..",
            "src",
            "selecao",
            "Unifesspa.UniPlus.Selecao.Infrastructure",
            "Persistence",
            "Migrations",
            arquivo)));

    /// <summary>
    /// Recorta o corpo do <c>Down</c> de uma migration. Asserir sobre o arquivo
    /// inteiro confundiria a guarda com o <c>Up</c>, que cita os mesmos códigos
    /// dentro do texto das entradas semeadas — o recorte deixa a asserção falar
    /// só da guarda, sem depender de pontuação para desambiguar.
    /// </summary>
    public static string BlocoDown(string migration)
    {
        const string AssinaturaDown = "protected override void Down(";

        int inicio = migration.IndexOf(AssinaturaDown, StringComparison.Ordinal);
        if (inicio < 0)
        {
            throw new InvalidOperationException(
                "A migration não declara Down — sem ele não há guarda de reversão a provar.");
        }

        return migration[inicio..];
    }
}
