namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

using AwesomeAssertions;

/// <summary>
/// Fitness test da issue #850: o vocabulário <c>Edital</c> que a #804 eliminou do módulo
/// Seleção não pode reaparecer como identificador de código C#. Detecção por identificador
/// EXATO (<c>\bEdital\b</c> e o resto da lista negra), nunca por substring — o que faz
/// <c>DocumentoEdital</c>/<c>DocumentoEditalId</c>/<c>StatusDocumentoEdital</c> ficarem de
/// fora sem precisar de allowlist: não há fronteira de palavra entre "Documento" e "Edital"
/// (os dois são caracteres \w), então o regex já os ignora pela própria definição de
/// <c>\b</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Comentários e strings são neutralizados antes do scan</b> — não apenas removidos por
/// linha, mas por um scanner lexical de um passe só que reconhece as quatro formas de string
/// do C# (regular, verbatim, interpolada, raw, e as combinações delas) e trata cada uma de
/// forma diferente: string regular/verbatim têm o miolo inteiro neutralizado (não têm
/// interpolação); string interpolada (<c>$"..."</c>, <c>$@"..."</c>, raw interpolada com N
/// cifrões) neutraliza só o texto FORA dos holes <c>{...}</c> — o conteúdo dentro do hole é
/// código de verdade e continua sujeito ao mesmo detector. Isso fecha o falso positivo de
/// string literal (ex.: "Referência ao documento do Edital é obrigatória.") sem esconder
/// código real dentro de interpolação (ex.: <c>$"{command.EditalId}"</c> ainda acusa).
/// </para>
/// <para>
/// <b>Escopo por design, não por omissão:</b> este detector varre <c>**/*.cs</c> de Seleção
/// — o schema fonte <c>ProcessoPublicado.avsc</c> (Avro) também declara <c>EditalId</c>, mas
/// fica FORA do escopo porque não é identificador de código C#. A família
/// <c>hashEdital</c>/<c>hashesEdital</c>/<c>SerializarHashesEdital</c> (chaves do envelope
/// canônico) também fica fora — renomeação endereçada em task própria, que deve ACRESCENTAR
/// sua entrada a este mesmo detector, não criar um segundo.
/// </para>
/// </remarks>
public sealed class SelecaoNaoDeclaraTipoEditalTests
{
    /// <summary>
    /// Lista negra por identificador exato. <c>TipoEditalCodigo</c> já não tem ocorrência
    /// viva (renomeado para <c>TipoProcessoCodigo</c> pela migration
    /// <c>20260716030741_RenomeiaTipoEditalCodigoParaTipoProcessoCodigo</c>) — incluí-lo
    /// fecha essa família sem custo, em vez de deixá-la como lacuna.
    /// </summary>
    private static readonly string[] ListaNegra =
    [
        "Edital",
        "NaturezaEdital",
        "StatusEdital",
        "SnapshotPublicacao",
        "EditalGovernanceSnapshot",
        "PublicarEditalCommandHandler",
        "EditalPublicadoEvent",
        "EditalId",
        "TipoEditalCodigo",
    ];

    /// <summary>
    /// Allowlist por PAR (arquivo, identificador) — nunca por arquivo inteiro. Libera só o
    /// identificador <c>EditalId</c> nesses 5 arquivos (contrato durável de
    /// <c>ProcessoPublicadoEvent</c>: outbox e schema Avro sob compatibilidade BACKWARD).
    /// Qualquer OUTRO identificador da lista negra continua acusado nesses mesmos arquivos.
    /// </summary>
    private static readonly string[] ArquivosComEditalIdPermitido =
    [
        Path.Combine("Unifesspa.UniPlus.Selecao.Domain", "Events", "ProcessoPublicadoEvent.cs"),
        Path.Combine("Unifesspa.UniPlus.Selecao.Application", "Events", "ProcessosSeletivos", "ProcessoPublicadoEventHandler.cs"),
        Path.Combine("Unifesspa.UniPlus.Selecao.Infrastructure", "Messaging", "ProcessoPublicadoToKafkaCascadeHandler.cs"),
        Path.Combine("Unifesspa.UniPlus.Selecao.Infrastructure", "Messaging", "ProcessoPublicadoToAvroMapper.cs"),
        Path.Combine("Unifesspa.UniPlus.Selecao.Infrastructure", "Messaging", "Avro", "ProcessoPublicado.cs"),
    ];

    // ── Canários — o detector acusa/não acusa o que deveria (§3.3) ──

    [Theory(DisplayName = "O detector acusa identificador real da lista negra fora de comentário/string")]
    [InlineData("Guid EditalId { get; }")]
    [InlineData("command.EditalId")]
    [InlineData("class Edital { }")]
    [InlineData("var x = new Edital();")]
    [InlineData("NaturezaEdital.Abertura")]
    [InlineData(@"$""{command.EditalId}""")]
    public void Detector_AcusaVocabularioEdital_ForaDaAllowlist(string trecho)
    {
        ArgumentNullException.ThrowIfNull(trecho);

        Violacoes(trecho, arquivo: "QualquerArquivo.cs").Should().NotBeEmpty(
            $"'{trecho}' contém um identificador real da lista negra, fora de comentário ou string");
    }

    [Theory(DisplayName = "O detector não acusa identificador legítimo, string literal nem raw string")]
    [InlineData("class DocumentoEdital : EntityBase { }")]
    [InlineData("Guid DocumentoEditalId { get; }")]
    [InlineData("record DadosEdital(string Numero);")]
    [InlineData("class DocumentosEditalController : ControllerBase { }")]
    [InlineData(@"return Result.Failure(new DomainError(Codes.X, ""Referência ao documento do Edital é obrigatória.""));")]
    [InlineData("\"\"\"Edital\"\"\"")]
    [InlineData(@"$""{{EditalId}}""")]
    public void Detector_NaoAcusaVocabularioLegitimoOuStringLiteral(string trecho)
    {
        ArgumentNullException.ThrowIfNull(trecho);

        Violacoes(trecho, arquivo: "QualquerArquivo.cs").Should().BeEmpty(
            $"'{trecho}' não é identificador da lista negra fora de comentário/string — ou é chave escapada (não interpolação)");
    }

    [Fact(DisplayName = "O detector acusa a parte interpolada de uma raw string, ignorando o texto literal")]
    public void Detector_AcusaRawStringInterpolada_SoAParteInterpolada()
    {
        const string trecho = "$\"\"\"texto {command.EditalId}\"\"\"";

        Violacoes(trecho, arquivo: "QualquerArquivo.cs").Should().NotBeEmpty(
            "o hole de interpolação com command.EditalId é código de verdade, mesmo dentro de uma raw string");
    }

    [Fact(DisplayName = "O detector acusa raw string interpolada C# 14 com delimitador $$ (hole de 2 chaves)")]
    public void Detector_AcusaRawStringInterpoladaComDoisCifroes()
    {
        const string trecho = "$$\"\"\"texto {{command.EditalId}}\"\"\"";

        Violacoes(trecho, arquivo: "QualquerArquivo.cs").Should().NotBeEmpty(
            "com delimitador $$, o par de chaves duplas é o hole de interpolação real — command.EditalId é código");
    }

    [Fact(DisplayName = "Plantar 'class Edital' DENTRO de um arquivo permitido ainda é acusado — a allowlist é por identificador, não por arquivo")]
    public void Detector_AcusaOutroIdentificadorDentroDeArquivoParcialmentePermitido()
    {
        const string trecho = "public sealed record EditalId(Guid Value); class Edital { }";

        Violacoes(trecho, arquivo: ArquivosComEditalIdPermitido[0]).Should().Contain(
            v => v.Contains("Edital", StringComparison.Ordinal) && !v.Contains("EditalId", StringComparison.Ordinal),
            "EditalId é permitido nesse arquivo, mas 'class Edital' é um identificador DIFERENTE — a allowlist " +
            "é por (arquivo, identificador), nunca por arquivo inteiro");
    }

    [Fact(DisplayName = "EditalId não é acusado no arquivo permitido, mas é acusado em qualquer outro")]
    public void Detector_NaoAcusaEditalIdNoArquivoPermitido_MasAcusaEmOutro()
    {
        const string trecho = "public Guid EditalId { get; }";

        Violacoes(trecho, arquivo: ArquivosComEditalIdPermitido[0]).Should().BeEmpty(
            "EditalId é proposital nesse arquivo — contrato durável de outbox/Avro");
        Violacoes(trecho, arquivo: "OutroArquivoQualquer.cs").Should().NotBeEmpty(
            "fora dos 5 arquivos da allowlist, EditalId continua proibido");
    }

    // ── Real: Seleção inteira, zero vocabulário Edital fora da allowlist ──

    [Fact(DisplayName = "Selecao.* reais: zero vocabulário Edital fora da allowlist (escopo **/*.cs, .avsc fora por design)")]
    public void Selecao_NaoDeclaraTipoEdital()
    {
        string raizDoRepo = RaizDoRepo();
        string raizSelecao = Path.Combine(raizDoRepo, "src", "selecao");

        Directory.Exists(raizSelecao).Should().BeTrue("o módulo Seleção precisa existir para ser varrido");

        string[] camadas =
        [
            "Unifesspa.UniPlus.Selecao.Domain",
            "Unifesspa.UniPlus.Selecao.Application",
            "Unifesspa.UniPlus.Selecao.Infrastructure",
            "Unifesspa.UniPlus.Selecao.API",
        ];

        List<string> violacoes = [];
        int arquivosExaminados = 0;

        foreach (string camada in camadas)
        {
            string raizCamada = Path.Combine(raizSelecao, camada);
            Directory.Exists(raizCamada).Should().BeTrue($"a camada {camada} precisa existir para ser varrida");

            foreach (string arquivo in ArquivosFonte(raizCamada))
            {
                arquivosExaminados++;
                string caminhoRelativo = Path.GetRelativePath(raizSelecao, arquivo);
                string codigo = File.ReadAllText(arquivo);

                foreach (string violacao in Violacoes(codigo, caminhoRelativo))
                {
                    violacoes.Add($"{caminhoRelativo}: {violacao}");
                }
            }
        }

        arquivosExaminados.Should().BeGreaterThan(30, "as quatro camadas de Seleção juntas têm dezenas de arquivos");

        violacoes.Should().BeEmpty(
            "o vocabulário Edital foi eliminado do módulo (#804) — reintroduzi-lo como identificador de código é " +
            "regressão, exceto EditalId nos 5 arquivos do contrato durável de outbox/Avro (allowlist)");
    }

    // ── Mecânica do detector ──

    /// <summary>
    /// <c>arquivo</c> é um caminho RELATIVO (ou nome de arquivo avulso, nos canários) — só é
    /// comparado contra <see cref="ArquivosComEditalIdPermitido"/> por sufixo, então os
    /// canários podem passar qualquer nome desde que não termine com um dos 5 caminhos reais.
    /// </summary>
    private static List<string> Violacoes(string codigoFonte, string arquivo)
    {
        string neutralizado = NeutralizarComentariosEStrings(codigoFonte);
        bool editalIdPermitido = ArquivosComEditalIdPermitido.Any(
            permitido => arquivo.EndsWith(permitido, StringComparison.Ordinal));

        List<string> violacoes = [];
        foreach (string identificador in ListaNegra)
        {
            if (editalIdPermitido && string.Equals(identificador, "EditalId", StringComparison.Ordinal))
            {
                continue;
            }

            Regex padrao = new($@"\b{Regex.Escape(identificador)}\b");
            if (padrao.IsMatch(neutralizado))
            {
                violacoes.Add(identificador);
            }
        }

        return violacoes;
    }

    private static IEnumerable<string> ArquivosFonte(string root) =>
        Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(static p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(static p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            // Migrations *.Designer.cs são o snapshot histórico e imutável do EF — não são
            // varridas (mesmo espírito do precedente de Publicações, que também exclui
            // gerado). As migrations .cs normais continuam varridas normalmente.
            .Where(static p => !p.EndsWith(".Designer.cs", StringComparison.Ordinal));

    private static string RaizDoRepo([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(Path.GetDirectoryName(origem)!, "..", ".."));

    // ── Scanner lexical de um passe só: comentários + as quatro formas de string do C# ──

    /// <summary>
    /// Neutraliza comentários e o MIOLO de literais de string, preservando código real
    /// dentro de holes de interpolação (<c>{...}</c>). Um passe só, sem regex — comentários
    /// dentro de uma string (ex. uma URL com <c>//</c>) e strings dentro de um comentário
    /// não se confundem porque o scanner nunca troca de estado no meio de um token.
    /// </summary>
    private static string NeutralizarComentariosEStrings(string codigo)
    {
        StringBuilder sb = new(codigo.Length);
        int i = 0;
        int n = codigo.Length;

        while (i < n)
        {
            char c = codigo[i];

            if (c == '/' && i + 1 < n && codigo[i + 1] == '/')
            {
                while (i < n && codigo[i] != '\n')
                {
                    sb.Append(' ');
                    i++;
                }

                continue;
            }

            if (c == '/' && i + 1 < n && codigo[i + 1] == '*')
            {
                sb.Append("  ");
                i += 2;
                while (i < n && !(codigo[i] == '*' && i + 1 < n && codigo[i + 1] == '/'))
                {
                    sb.Append(codigo[i] == '\n' ? '\n' : ' ');
                    i++;
                }

                if (i < n)
                {
                    sb.Append("  ");
                    i += 2;
                }

                continue;
            }

            (int dollarCount, bool verbatim, int quoteRun, int prefixLength) = DetectarPrefixoDeString(codigo, i);
            if (prefixLength > 0)
            {
                for (int p = 0; p < prefixLength; p++)
                {
                    sb.Append(' ');
                }

                i += prefixLength;
                i = quoteRun >= 3
                    ? ConsumirRawStringBody(codigo, i, dollarCount, quoteRun, sb)
                    : ConsumirStringBody(codigo, i, dollarCount, verbatim, sb);
                continue;
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Detecta o prefixo de um literal de string na posição <paramref name="i"/>: combinações
    /// de <c>@</c>/<c>$</c> (em qualquer ordem) seguidas de aspas. 3+ aspas consecutivas é raw
    /// string (não combina com <c>@</c> — só <c>$</c> compõe com raw). Devolve
    /// <c>PrefixLength == 0</c> quando não há literal de string na posição.
    /// </summary>
    private static (int DollarCount, bool Verbatim, int QuoteRun, int PrefixLength) DetectarPrefixoDeString(string s, int i)
    {
        int j = i;
        int dollarCount = 0;
        bool verbatim = false;

        while (j < s.Length && (s[j] == '$' || s[j] == '@'))
        {
            if (s[j] == '$')
            {
                dollarCount++;
            }
            else
            {
                verbatim = true;
            }

            j++;
        }

        if (j >= s.Length || s[j] != '"')
        {
            return (0, false, 0, 0);
        }

        int quoteRun = 0;
        int k = j;
        while (k < s.Length && s[k] == '"')
        {
            quoteRun++;
            k++;
        }

        return quoteRun >= 3
            ? (dollarCount, false, quoteRun, k - i)
            : (dollarCount, verbatim, 1, (j - i) + 1);
    }

    /// <summary>Corpo de string regular/verbatim/interpolada (1 aspas de abertura, 0 ou 1 cifrão).</summary>
    private static int ConsumirStringBody(string s, int i, int dollarCount, bool verbatim, StringBuilder sb)
    {
        int n = s.Length;
        while (i < n)
        {
            char c = s[i];

            if (!verbatim && c == '\\' && i + 1 < n)
            {
                sb.Append("  ");
                i += 2;
                continue;
            }

            if (verbatim && c == '"')
            {
                if (i + 1 < n && s[i + 1] == '"')
                {
                    sb.Append("  ");
                    i += 2;
                    continue;
                }

                sb.Append(' ');
                return i + 1;
            }

            if (!verbatim && c == '"')
            {
                sb.Append(' ');
                return i + 1;
            }

            if (dollarCount > 0 && c == '{')
            {
                if (i + 1 < n && s[i + 1] == '{')
                {
                    sb.Append("  ");
                    i += 2;
                    continue;
                }

                sb.Append(c);
                i++;
                i = ConsumirHoleDeInterpolacao(s, i, sb);
                continue;
            }

            if (dollarCount > 0 && c == '}' && i + 1 < n && s[i + 1] == '}')
            {
                sb.Append("  ");
                i += 2;
                continue;
            }

            sb.Append(c == '\n' ? '\n' : ' ');
            i++;
        }

        return i;
    }

    /// <summary>Hole de interpolação de string NÃO-raw: um único par de chaves aninhável normalmente.</summary>
    private static int ConsumirHoleDeInterpolacao(string s, int i, StringBuilder sb)
    {
        int n = s.Length;
        int depth = 1;
        while (i < n && depth > 0)
        {
            char c = s[i];
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
            }

            sb.Append(c);
            i++;
            if (depth == 0)
            {
                return i;
            }
        }

        return i;
    }

    /// <summary>Corpo de raw string (3+ aspas de abertura), com ou sem interpolação de N cifrões.</summary>
    private static int ConsumirRawStringBody(string s, int i, int dollarCount, int quoteRun, StringBuilder sb)
    {
        int n = s.Length;
        while (i < n)
        {
            char c = s[i];

            if (c == '"')
            {
                int run = ContarRepeticoes(s, i, '"');
                if (run >= quoteRun)
                {
                    for (int p = 0; p < quoteRun; p++)
                    {
                        sb.Append(' ');
                    }

                    return i + quoteRun;
                }

                for (int p = 0; p < run; p++)
                {
                    sb.Append(' ');
                }

                i += run;
                continue;
            }

            if (dollarCount > 0 && c == '{')
            {
                int run = ContarRepeticoes(s, i, '{');
                if (run >= dollarCount)
                {
                    for (int p = 0; p < dollarCount; p++)
                    {
                        sb.Append(s[i + p]);
                    }

                    i += dollarCount;
                    i = ConsumirHoleRawDeInterpolacao(s, i, dollarCount, sb);
                    continue;
                }

                for (int p = 0; p < run; p++)
                {
                    sb.Append(' ');
                }

                i += run;
                continue;
            }

            sb.Append(c == '\n' ? '\n' : ' ');
            i++;
        }

        return i;
    }

    /// <summary>
    /// Hole de interpolação de RAW string: abre com N chaves consecutivas, fecha com N chaves
    /// consecutivas — chaves simples aninhadas no meio (código real, ex. um inicializador de
    /// objeto) são pareadas normalmente sem confundir com o fechamento do hole.
    /// </summary>
    private static int ConsumirHoleRawDeInterpolacao(string s, int i, int dollarCount, StringBuilder sb)
    {
        int n = s.Length;
        int depth = 0;
        while (i < n)
        {
            char c = s[i];

            if (c == '{')
            {
                depth++;
                sb.Append(c);
                i++;
                continue;
            }

            if (c == '}')
            {
                if (depth > 0)
                {
                    depth--;
                    sb.Append(c);
                    i++;
                    continue;
                }

                int run = ContarRepeticoes(s, i, '}');
                int consumir = Math.Min(run, dollarCount);
                for (int p = 0; p < consumir; p++)
                {
                    sb.Append(s[i + p]);
                }

                i += consumir;
                if (run >= dollarCount)
                {
                    return i;
                }

                continue;
            }

            sb.Append(c);
            i++;
        }

        return i;
    }

    private static int ContarRepeticoes(string s, int i, char alvo)
    {
        int run = 0;
        int k = i;
        while (k < s.Length && s[k] == alvo)
        {
            run++;
            k++;
        }

        return run;
    }
}
