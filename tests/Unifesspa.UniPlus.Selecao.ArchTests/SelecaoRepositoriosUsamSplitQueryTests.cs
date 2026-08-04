namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.IO;
using System.Runtime.CompilerServices;

using AwesomeAssertions;

/// <summary>
/// Fitness test da issue #850 (§3.4): guarda de regressão para <c>.AsSplitQuery()</c> no
/// carregamento do agregado — a proteção contra o produto cartesiano de coleções irmãs num
/// único <c>JOIN</c>. O código de produção JÁ tem <c>.AsSplitQuery()</c>
/// (<c>ProcessoSeletivoRepository.ComConfiguracao</c>, story #851); o que faltava era a prova
/// mecânica (ver <c>ObterComConfiguracaoAsync_ComAsSplitQuery_EmiteMultiplosSelects</c>, em
/// <c>IntegrationTests</c>) e esta guarda, que impede alguém de remover o
/// <c>.AsSplitQuery()</c> ou acrescentar uma nova coleção sem ele.
/// </summary>
/// <remarks>
/// <para>
/// A unidade de varredura é o CORPO de um membro (método ou expression-bodied member),
/// dividido em ENUNCIADOS de nível superior (por <c>;</c>, respeitando o aninhamento de
/// <c>()</c>/<c>[]</c>/<c>{}</c> — um <c>;</c> dentro do corpo de uma lambda não separa
/// enunciados). Por enunciado: conta <c>.Include(</c>/<c>.ThenInclude(</c>; se o total for 2
/// ou mais e não houver <c>.AsSplitQuery()</c> no MESMO enunciado, acusa — a menos que o
/// enunciado componha via helper (ver abaixo).
/// </para>
/// <para>
/// <b>Composição via helper:</b> o caso real de <c>ObterParaMutacaoAsync</c>
/// (<c>ProcessoSeletivoRepository.cs</c>) devolve literalmente
/// <c>ComConfiguracao(_context.ProcessosSeletivos).Include(p =&gt; p.Rascunho).FirstOrDefaultAsync(...)</c>
/// — o helper é o RECEIVER TEXTUAL IMEDIATO da cadeia (o token que precede o primeiro
/// <c>.Include</c>/<c>.ThenInclude</c>/<c>.AsSplitQuery</c> do enunciado). A composição só
/// conta como coberta quando esse helper é resolvido (por nome, no mesmo arquivo) e o corpo
/// DELE já contém <c>.AsSplitQuery()</c> — não basta o helper ser chamado em qualquer lugar
/// do enunciado, nem <c>.AsSplitQuery()</c> aparecer em outro enunciado/variável do mesmo
/// método.
/// </para>
/// <para>
/// Mesma família de detector regex/texto dos precedentes (sem ArchUnitNET, sem Roslyn) —
/// deliberadamente conservador: não distingue referência 0..1 de coleção por análise de
/// tipo (exigiria Roslyn), então dois <c>.Include(</c> encadeados que sejam ambos referências
/// 0..1 ainda acusam (falso positivo raro e barato de silenciar, preferível a um falso
/// negativo). Funções locais aninhadas dentro de um membro são varridas como membros
/// PRÓPRIOS pelo mesmo extrator — o corpo delas soma nos dois lugares; nenhum caso real do
/// código hoje tem função local usando <c>Include</c>, então essa sobreposição não afeta o
/// resultado sobre <c>ProcessoSeletivoRepository.cs</c>.
/// </para>
/// </remarks>
public sealed class SelecaoRepositoriosUsamSplitQueryTests
{
    // ── Canários (§3.4) ──

    [Fact(DisplayName = "(a) 2 Include sem AsSplitQuery no mesmo enunciado acusa")]
    public void Detector_DoisIncludeSemAsSplitQuery_Acusa()
    {
        const string codigo = """
            class Repo
            {
                public IQueryable<Foo> Obter(IQueryable<Foo> q) =>
                    q.Include(x => x.A).Include(x => x.B);
            }
            """;

        Violacoes(codigo).Should().ContainSingle(v => v.Metodo == "Obter");
    }

    [Fact(DisplayName = "(b) o mesmo com AsSplitQuery() no enunciado passa")]
    public void Detector_DoisIncludeComAsSplitQuery_Passa()
    {
        const string codigo = """
            class Repo
            {
                public IQueryable<Foo> Obter(IQueryable<Foo> q) =>
                    q.Include(x => x.A).Include(x => x.B).AsSplitQuery();
            }
            """;

        Violacoes(codigo).Should().BeEmpty();
    }

    [Fact(DisplayName = "(c) método com só 1 Include passa mesmo sem AsSplitQuery")]
    public void Detector_UmIncludeSemAsSplitQuery_Passa()
    {
        const string codigo = """
            class Repo
            {
                public IQueryable<Foo> Obter(IQueryable<Foo> q) =>
                    q.Include(x => x.A);
            }
            """;

        Violacoes(codigo).Should().BeEmpty();
    }

    [Fact(DisplayName = "(d) 1 Include próprio encadeado a partir de um helper já coberto por AsSplitQuery passa — caso real de ObterParaMutacaoAsync")]
    public void Detector_UmIncludeProprioComposicaoViaHelperCoberto_Passa()
    {
        const string codigo = """
            class Repo
            {
                private static IQueryable<Foo> ComConfiguracao(IQueryable<Foo> q) =>
                    q.Include(x => x.A).Include(x => x.B).AsSplitQuery();

                public async Task<Foo?> ObterParaMutacaoAsync(Guid id)
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1");
                    return await ComConfiguracao(_context.Foos).Include(p => p.Rascunho).FirstOrDefaultAsync();
                }
            }
            """;

        Violacoes(codigo).Should().BeEmpty();
    }

    [Fact(DisplayName = "Composição via helper cobre também 2+ Include próprios, quando o helper é o receiver textual imediato da cadeia")]
    public void Detector_DoisIncludeProprios_ComposicaoViaHelperCoberto_Passa()
    {
        const string codigo = """
            class Repo
            {
                private static IQueryable<Foo> ComConfiguracao(IQueryable<Foo> q) =>
                    q.Include(x => x.A).AsSplitQuery();

                public IQueryable<Foo> Obter(IQueryable<Foo> q) =>
                    ComConfiguracao(q).Include(p => p.B).Include(p => p.C);
            }
            """;

        Violacoes(codigo).Should().BeEmpty(
            "o helper é o receiver textual imediato da cadeia, e o corpo dele já tem AsSplitQuery()");
    }

    [Fact(DisplayName = "(e) helper coberto chamado ISOLADAMENTE, seguido de cadeia PRÓPRIA com 2+ Include sem AsSplitQuery, acusa")]
    public void Detector_HelperChamadoIsoladamente_CadeiaPropriaSemAsSplitQuery_Acusa()
    {
        const string codigo = """
            class Repo
            {
                private static IQueryable<Foo> ComConfiguracao(IQueryable<Foo> q) =>
                    q.Include(x => x.A).AsSplitQuery();

                public IQueryable<Foo> Obter(IQueryable<Foo> q, IQueryable<Foo> outra)
                {
                    var descartado = ComConfiguracao(q);
                    return outra.Include(p => p.B).Include(p => p.C);
                }
            }
            """;

        Violacoes(codigo).Should().ContainSingle(v => v.Metodo == "Obter",
            "o helper não é o receiver da cadeia nova — chamá-lo em outro enunciado não cobre a cadeia própria");
    }

    [Fact(DisplayName = "(f) AsSplitQuery() numa query DIFERENTE da que tem os 2+ Include acusa")]
    public void Detector_AsSplitQueryEmQueryDiferente_Acusa()
    {
        const string codigo = """
            class Repo
            {
                public IQueryable<Foo> Obter(IQueryable<Foo> outra, IQueryable<Foo> principal)
                {
                    var outraSplit = outra.AsSplitQuery();
                    return principal.Include(p => p.B).Include(p => p.C);
                }
            }
            """;

        Violacoes(codigo).Should().ContainSingle(v => v.Metodo == "Obter",
            "o AsSplitQuery() pertence a outra query — não cobre a cadeia com os 2+ Include");
    }

    [Fact(DisplayName = "(g) dois Include hipotéticos de referências 0..1 sem AsSplitQuery ainda acusam — detector deliberadamente conservador")]
    public void Detector_DoisIncludeDeReferenciasSingulares_AindaAcusa()
    {
        const string codigo = """
            class Repo
            {
                public IQueryable<Foo> Obter(IQueryable<Foo> q) =>
                    q.Include(p => p.BonusRegional).Include(p => p.Rascunho);
            }
            """;

        Violacoes(codigo).Should().ContainSingle(v => v.Metodo == "Obter",
            "o detector não distingue referência 0..1 de coleção por análise de tipo — falso positivo aceito conscientemente");
    }

    // ── Real: ProcessoSeletivoRepository.cs ──

    [Fact(DisplayName = "Selecao_RepositoriosUsamSplitQueryQuandoMultiplasColecoes — ProcessoSeletivoRepository.cs real: zero violação")]
    public void Selecao_RepositoriosUsamSplitQueryQuandoMultiplasColecoes()
    {
        string caminho = CaminhoRepositorio();
        File.Exists(caminho).Should().BeTrue("o repositório precisa existir para ser varrido");

        string codigo = File.ReadAllText(caminho);
        List<(string Metodo, string Motivo)> violacoes = Violacoes(codigo);

        violacoes.Should().BeEmpty(
            "ComConfiguracao já termina em .AsSplitQuery() (story de cronograma de fases, 15/07) e " +
            "ObterParaMutacaoAsync é coberto pela regra de composição via helper — nenhuma cadeia real " +
            "combina 2+ Include/ThenInclude sem AsSplitQuery hoje");
    }

    private static string CaminhoRepositorio([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(
            Path.GetDirectoryName(origem)!, "..", "..",
            "src", "selecao", "Unifesspa.UniPlus.Selecao.Infrastructure",
            "Persistence", "Repositories", "ProcessoSeletivoRepository.cs"));

    // ── Mecânica do detector ──

    private static readonly string[] MarcadoresDeCadeia = [".Include(", ".ThenInclude(", ".AsSplitQuery("];

    private static List<(string Metodo, string Motivo)> Violacoes(string codigoDoArquivo)
    {
        List<(string Nome, string Corpo)> membros = [.. ExtrairCorposDeMembros(codigoDoArquivo)];
        Dictionary<string, string> corposPorNome = new(StringComparer.Ordinal);
        foreach ((string nome, string corpo) in membros)
        {
            // Sobrecarga/duplicata de nome: mantém o primeiro — resolução por nome já é uma
            // aproximação (sem overload resolution real), não vale complicar mais que isso.
            corposPorNome.TryAdd(nome, corpo);
        }

        List<(string Metodo, string Motivo)> violacoes = [];
        foreach ((string nome, string corpo) in membros)
        {
            string interior = DespirChavesExternas(corpo);
            foreach (string enunciado in DividirEmEnunciados(interior))
            {
                int totalIncludes = ContarOcorrencias(enunciado, ".Include(") + ContarOcorrencias(enunciado, ".ThenInclude(");
                if (totalIncludes == 0)
                {
                    continue;
                }

                if (enunciado.Contains(".AsSplitQuery(", StringComparison.Ordinal))
                {
                    continue;
                }

                string? helper = ObterHelperReceptorDaCadeia(enunciado);
                if (helper is not null
                    && corposPorNome.TryGetValue(helper, out string? corpoDoHelper)
                    && corpoDoHelper.Contains(".AsSplitQuery(", StringComparison.Ordinal))
                {
                    continue;
                }

                if (totalIncludes >= 2)
                {
                    violacoes.Add((nome, enunciado.Trim()));
                }
            }
        }

        return violacoes;
    }

    /// <summary>
    /// Remove as chaves externas de um corpo <c>{ ... }</c> (bloco) — expression-bodied
    /// members já vêm sem elas (extraídos entre <c>=&gt;</c> e o <c>;</c> final).
    /// </summary>
    private static string DespirChavesExternas(string corpo)
    {
        string aparado = corpo.Trim();
        return aparado.Length >= 2 && aparado[0] == '{' && aparado[^1] == '}'
            ? aparado[1..^1]
            : aparado;
    }

    private static List<string> DividirEmEnunciados(string corpo)
    {
        List<string> enunciados = [];
        int inicio = 0;
        int depth = 0;

        for (int i = 0; i < corpo.Length; i++)
        {
            char c = corpo[i];
            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}')
            {
                depth--;
            }
            else if (c == ';' && depth <= 0)
            {
                enunciados.Add(corpo[inicio..(i + 1)]);
                inicio = i + 1;
            }
        }

        if (inicio < corpo.Length)
        {
            string resto = corpo[inicio..].Trim();
            if (resto.Length > 0)
            {
                enunciados.Add(resto);
            }
        }

        return enunciados;
    }

    /// <summary>
    /// Extrai (nome, corpo) de todo membro com forma <c>nome(...) { ... }</c> ou
    /// <c>nome(...) =&gt; expressão;</c> — varredura de texto genérica, sem distinguir
    /// método/construtor/função local (mesma técnica dos precedentes: aceita ler algum
    /// candidato a mais, como uma chamada `if (...)`, que nunca é seguida de `{`/`=>`
    /// imediatamente após o `)` de um jeito que produza um corpo utilizável — na prática
    /// filtra-se sozinho).
    /// </summary>
    private static List<(string Nome, string Corpo)> ExtrairCorposDeMembros(string codigo)
    {
        List<(string Nome, string Corpo)> membros = [];
        int i = 0;

        while (i < codigo.Length)
        {
            int abreParen = codigo.IndexOf('(', i);
            if (abreParen < 0)
            {
                break;
            }

            int fimNome = abreParen;
            int inicioNome = fimNome;
            while (inicioNome > 0 && (char.IsLetterOrDigit(codigo[inicioNome - 1]) || codigo[inicioNome - 1] == '_'))
            {
                inicioNome--;
            }

            if (inicioNome == fimNome)
            {
                i = abreParen + 1;
                continue;
            }

            string nome = codigo[inicioNome..fimNome];

            int fechaParen = EncontrarFechamento(codigo, abreParen, '(', ')');
            if (fechaParen < 0)
            {
                i = abreParen + 1;
                continue;
            }

            int j = fechaParen + 1;
            while (j < codigo.Length && char.IsWhiteSpace(codigo[j]))
            {
                j++;
            }

            if (j < codigo.Length && codigo[j] == '{')
            {
                int fechaChave = EncontrarFechamento(codigo, j, '{', '}');
                if (fechaChave > 0)
                {
                    membros.Add((nome, codigo[j..(fechaChave + 1)]));
                    i = fechaChave + 1;
                    continue;
                }
            }
            else if (j + 1 < codigo.Length && codigo[j] == '=' && codigo[j + 1] == '>')
            {
                int fimExpressao = EncontrarFimDeExpressao(codigo, j + 2);
                membros.Add((nome, codigo[(j + 2)..fimExpressao]));
                i = fimExpressao + 1;
                continue;
            }

            i = abreParen + 1;
        }

        return membros;
    }

    private static int EncontrarFechamento(string codigo, int posAbertura, char abre, char fecha)
    {
        int depth = 0;
        for (int i = posAbertura; i < codigo.Length; i++)
        {
            if (codigo[i] == abre)
            {
                depth++;
            }
            else if (codigo[i] == fecha)
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static int EncontrarFimDeExpressao(string codigo, int inicio)
    {
        int depth = 0;
        int i = inicio;
        while (i < codigo.Length)
        {
            char c = codigo[i];
            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}')
            {
                depth--;
            }
            else if (c == ';' && depth <= 0)
            {
                return i;
            }

            i++;
        }

        return codigo.Length - 1;
    }

    /// <summary>
    /// O helper candidato: o identificador imediatamente antes de uma chamada de método que
    /// termina bem em cima do primeiro <c>.Include(</c>/<c>.ThenInclude(</c>/<c>.AsSplitQuery(</c>
    /// do enunciado — ou seja, <c>Helper(args)</c> é o RECEIVER textual direto da cadeia.
    /// </summary>
    private static string? ObterHelperReceptorDaCadeia(string enunciado)
    {
        int posPrimeiraChamada = PrimeiraOcorrencia(enunciado, MarcadoresDeCadeia);
        if (posPrimeiraChamada < 0)
        {
            return null;
        }

        int i = posPrimeiraChamada - 1;
        while (i >= 0 && char.IsWhiteSpace(enunciado[i]))
        {
            i--;
        }

        if (i < 0 || enunciado[i] != ')')
        {
            return null;
        }

        int abreParen = EncontrarAberturaCorrespondente(enunciado, i, '(', ')');
        if (abreParen < 0)
        {
            return null;
        }

        int j = abreParen - 1;
        while (j >= 0 && char.IsWhiteSpace(enunciado[j]))
        {
            j--;
        }

        int fimNome = j + 1;
        while (j >= 0 && (char.IsLetterOrDigit(enunciado[j]) || enunciado[j] == '_'))
        {
            j--;
        }

        int inicioNome = j + 1;
        return inicioNome < fimNome ? enunciado[inicioNome..fimNome] : null;
    }

    private static int PrimeiraOcorrencia(string texto, IReadOnlyList<string> marcadores)
    {
        int melhor = -1;
        foreach (string marcador in marcadores)
        {
            int pos = texto.IndexOf(marcador, StringComparison.Ordinal);
            if (pos >= 0 && (melhor < 0 || pos < melhor))
            {
                melhor = pos;
            }
        }

        return melhor;
    }

    private static int EncontrarAberturaCorrespondente(string texto, int posFechamento, char abre, char fecha)
    {
        int depth = 0;
        for (int i = posFechamento; i >= 0; i--)
        {
            if (texto[i] == fecha)
            {
                depth++;
            }
            else if (texto[i] == abre)
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static int ContarOcorrencias(string texto, string marcador)
    {
        int count = 0;
        int pos = 0;
        while ((pos = texto.IndexOf(marcador, pos, StringComparison.Ordinal)) >= 0)
        {
            count++;
            pos += marcador.Length;
        }

        return count;
    }
}
