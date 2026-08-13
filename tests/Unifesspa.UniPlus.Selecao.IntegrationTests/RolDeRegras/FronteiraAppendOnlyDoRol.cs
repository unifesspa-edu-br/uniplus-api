namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text;

using AwesomeAssertions;

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
    /// Referência mutilada: tem código e versão, mas não o <c>hash</c> que prova
    /// o conteúdo. Não é referência de regra — e um detector que só olhasse
    /// código e versão a aceitaria.
    /// </summary>
    public static string ReferenciaSemHash(string codigo, string versao) =>
        $$$"""
        {"cronograma":{"fases":[{"regraContagem":{"codigo":"{{{codigo}}}","versao":"{{{versao}}}"}}]}}
        """;

    /// <summary>
    /// O predicado que identifica uma referência de regra dentro do jsonb
    /// congelado: a tripla inteira, não só o código. É a forma única usada
    /// pelas provas e pelas guardas de reversão — mesma convenção que
    /// <c>EnvelopeCanonicoGoldenTests.Envelope_ReferenciasDeRegraSaoTripla</c>
    /// fixa para o envelope canônico.
    /// </summary>
    public static string PredicadoDeReferencia(string codigo, string versao) =>
        $"""$.** ? (@.codigo == "{Literal(codigo)}" && @.versao == "{Literal(versao)}" && exists(@.hash))""";

    /// <summary>
    /// O predicado é montado por interpolação — o jsonpath não tem parâmetro de
    /// ligação, só a expressão inteira entra por <c>DbParameter</c>. Hoje os
    /// valores vêm de constantes do domínio, mas aspa ou barra invertida
    /// quebrariam a expressão em silêncio, então recusam-se na entrada em vez de
    /// virarem erro de sintaxe obscuro — ou, no dia em que a origem for
    /// dinâmica, uma injeção.
    /// </summary>
    private static string Literal(string valor)
    {
        if (valor.AsSpan().IndexOfAny('"', '\\') >= 0)
        {
            throw new ArgumentException(
                $"Código ou versão com aspa ou barra invertida não pode entrar num predicado jsonpath: '{valor}'.",
                nameof(valor));
        }

        return valor;
    }

    /// <summary>Aplica o predicado a uma amostra avulsa — usado pelos canários.</summary>
    public const string DetectaEmAmostra = """
        WITH amostra(configuracao_congelada) AS (VALUES (@amostra::jsonb))
        SELECT count(*) FROM amostra
        WHERE configuracao_congelada @? @predicado::jsonpath
        """;

    /// <summary>Aplica o predicado às configurações congeladas de verdade.</summary>
    public const string ContaReferenciasReais = """
        SELECT count(*) FROM selecao.versoes_configuracao
        WHERE configuracao_congelada @? @predicado::jsonpath
        """;

    /// <summary>
    /// Executa uma das contagens acima. O predicado entra por parâmetro, nunca
    /// interpolado no texto do comando.
    /// </summary>
    public static async Task<long> ContarAsync(
        SelecaoDbContext context, string sql, string predicado, string? amostra)
    {
        DbConnection conexao = await AbrirAsync(context);

        await using DbCommand comando = conexao.CreateCommand();
        comando.CommandText = sql;
        AdicionarParametro(comando, "predicado", predicado);
        if (amostra is not null)
        {
            AdicionarParametro(comando, "amostra", amostra);
        }

        object? resultado = await comando.ExecuteScalarAsync(CancellationToken.None);
        return Convert.ToInt64(resultado, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Prova que nenhuma configuração congelada referencia <paramref name="codigo"/>
    /// na versão indicada — e que o detector usado para afirmar isso funciona.
    /// </summary>
    /// <remarks>
    /// A ausência só significa algo depois que o detector se prova capaz de ver
    /// a referência real, e incapaz de confundir com ela quatro coisas que não
    /// são referência: código com o procurado por prefixo, homônimo sob a chave
    /// bare <c>codigo</c>, referência a outra versão e referência sem o
    /// <c>hash</c> que prova o conteúdo. Sem os dois últimos, um detector que
    /// ignorasse a versão — ou que não exigisse o hash — passaria por aqui.
    /// </remarks>
    public static async Task NenhumaReferenciaCongeladaAsync(
        SelecaoDbContext context, string codigo, string versao)
    {
        string predicado = PredicadoDeReferencia(codigo, versao);

        Task<long> EmAmostra(string amostra) =>
            ContarAsync(context, DetectaEmAmostra, predicado, amostra);

        (await EmAmostra(TriplaDeReferencia(codigo, versao))).Should().Be(
            1, "o detector precisa enxergar a referência real para a ausência provar algo");

        (await EmAmostra(TriplaDeReferencia(codigo + "-LEGADO", versao))).Should().Be(
            0, "um código diferente que contém o procurado como prefixo não é o procurado");

        (await EmAmostra(FaseHomonima(codigo))).Should().Be(
            0, "objeto com a chave bare codigo, sem versao e hash, é homônimo — não referência de regra");

        (await EmAmostra(TriplaDeReferencia(codigo, "v99"))).Should().Be(
            0, "referência a outra versão não é referência a esta");

        (await EmAmostra(ReferenciaSemHash(codigo, versao))).Should().Be(
            0, "sem o hash não há prova de conteúdo, e a fronteira do append-only é sobre conteúdo");

        (await ContarAsync(context, ContaReferenciasReais, predicado, amostra: null)).Should().Be(
            0, "alterar ou remover entrada já congelada por configuração violaria o append-only (RN08)");

        // Varredura mais larga sobre os dados reais: qualquer menção ao código
        // com FORMA de referência, ainda que incompleta. O predicado exato acima
        // pularia uma referência mal formada — sem hash, ou noutra versão —, e o
        // canonicalizador não produz essa forma, mas a afirmação "ninguém
        // referenciava" fica mais forte provando também a ausência dela.
        //
        // Exigir versao ou hash é o que mantém o homônimo de fora: uma fase
        // batizada com o código de uma regra não tem nenhum dos dois, e continua
        // legitimamente invisível aqui — do contrário esta varredura acusaria
        // justamente o caso que o canário acima declara benigno.
        (await ContarAsync(context, ContaReferenciasReais, PredicadoDeMencaoComFormaDeReferencia(codigo), amostra: null))
            .Should().Be(
                0, $"nenhuma configuração congelada menciona {codigo} em forma de referência, nem incompleta");
    }

    /// <summary>
    /// Menção ao código acompanhada de <c>versao</c> ou <c>hash</c> — algo que
    /// se apresenta como referência de regra, completa ou não. Serve de
    /// varredura de segurança sobre dados reais; não decide o que É referência,
    /// papel de <see cref="PredicadoDeReferencia"/>.
    /// </summary>
    private static string PredicadoDeMencaoComFormaDeReferencia(string codigo) =>
        $"""$.** ? (@.codigo == "{Literal(codigo)}" && (exists(@.versao) || exists(@.hash)))""";

    private static void AdicionarParametro(DbCommand comando, string nome, string valor)
    {
        DbParameter parametro = comando.CreateParameter();
        parametro.ParameterName = nome;
        parametro.Value = valor;
        comando.Parameters.Add(parametro);
    }

    private static async Task<DbConnection> AbrirAsync(SelecaoDbContext context)
    {
        DbConnection conexao = context.Database.GetDbConnection();
        if (conexao.State != System.Data.ConnectionState.Open)
        {
            await conexao.OpenAsync(CancellationToken.None);
        }

        return conexao;
    }

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
        DbConnection conexao = await AbrirAsync(context);

        await using DbCommand comando = conexao.CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync(CancellationToken.None);
    }

    /// <summary>
    /// Lê o arquivo de uma migration do módulo Seleção, para provar que o SQL
    /// exercitado no teste é o que a migration realmente carrega. O caminho de
    /// origem é preenchido pelo compilador no ponto de chamada, de modo que a
    /// classe chamadora não precisa de auxiliar próprio para descobri-lo.
    /// </summary>
    public static string LerMigration(string arquivo, [CallerFilePath] string origemDoTeste = "") =>
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
