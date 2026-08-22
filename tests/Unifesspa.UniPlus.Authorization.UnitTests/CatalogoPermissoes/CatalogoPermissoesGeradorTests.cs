namespace Unifesspa.UniPlus.Authorization.UnitTests.CatalogoPermissoes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.PermissionCatalogGenerator;

public sealed class CatalogoPermissoesGeradorTests
{
    [Fact]
    public void Gerar_FonteVersionada_ReproduzArtefatoVersionado()
    {
        string raiz = LocalizarRaizDaSolucao();
        string fonte = Path.Combine(raiz, "permissions.yml");
        string artefato = Path.Combine(
            raiz,
            "src",
            "shared",
            "Unifesspa.UniPlus.Authorization",
            "Generated",
            "UniPlusPermissions.g.cs");

        string gerado = CatalogoPermissoesGerador.GerarDeArquivo(fonte);

        gerado.Should().Be(File.ReadAllText(artefato));
    }

    [Fact]
    public void Ler_EntradaSemCampoObrigatorio_Rejeita()
    {
        const string yaml = """
            permissions:
              - codigo: configuracao:motivos:manter
                modulo: configuracao
                recurso: motivos
                acao: manter
                descricao: Manter motivos
                experimental: false
                sensibilidade: interna
                exportavel: false
                audit_level: caso-uso
                base_legal_default: ""
                requires_mfa: false
                requires_dual_approval: false
                context_scope: []
                allowed_subject_kind: []
            """;

        Action acao = () => CatalogoPermissoesGerador.Ler(yaml);

        acao.Should().Throw<FormatException>()
            .WithMessage("*Campo obrigatório ausente: 'decision_checks'*");
    }

    [Fact]
    public void Gerar_MesmaFonte_ReproduzResultadoDeterministico()
    {
        const string yaml = """
            permissions:
              - codigo: configuracao:motivos:manter
                modulo: configuracao
                recurso: motivos
                acao: manter
                descricao: Manter motivos
                experimental: false
                sensibilidade: interna
                exportavel: false
                audit_level: caso-uso
                base_legal_default: ""
                requires_mfa: false
                requires_dual_approval: false
                context_scope: []
                allowed_subject_kind: []
                decision_checks: []
            """;

        string primeiraGeracao = CatalogoPermissoesGerador.Gerar(yaml);
        string segundaGeracao = CatalogoPermissoesGerador.Gerar(yaml);

        segundaGeracao.Should().Be(primeiraGeracao);
    }

    [Fact]
    public void Gerar_ValorComQuebraDeLinha_EscapaComoLiteralCSharpValido()
    {
        // Bloco literal YAML (|) produz quebra de linha real no valor
        // desserializado — sem escapar, o C# gerado teria newline literal
        // dentro de uma string comum, o que não compila.
        string yaml = File.ReadAllText(Path.Combine(LocalizarRaizDaSolucao(), "permissions.yml"))
            .Replace(
                "base_legal_default: \"\"",
                "base_legal_default: |\n      art. 7\n      LGPD",
                StringComparison.Ordinal);

        string gerado = CatalogoPermissoesGerador.Gerar(yaml);

        gerado.Should().Contain("\\n").And.NotMatchRegex("baseLegalPadrao: \"[^\"]*\n");
    }

    [Fact]
    public void Gerar_QualquerPlataforma_EmiteSomenteLf()
    {
        string yaml = File.ReadAllText(Path.Combine(LocalizarRaizDaSolucao(), "permissions.yml"))
            .ReplaceLineEndings("\r\n");

        string gerado = CatalogoPermissoesGerador.Gerar(yaml);

        gerado.Should().NotContain("\r");
    }

    [Fact]
    public void Ler_NivelAuditoriaPorAcessoIndividual_Rejeita()
    {
        string raiz = LocalizarRaizDaSolucao();
        string yaml = File.ReadAllText(Path.Combine(raiz, "permissions.yml"))
            .Replace("audit_level: caso-uso", "audit_level: cada-acesso", StringComparison.Ordinal);

        Action acao = () => CatalogoPermissoesGerador.Ler(yaml);

        acao.Should().Throw<FormatException>()
            .WithMessage("*cada-acesso*proibido*");
    }

    [Fact]
    public void Ler_DecisionCheckNaoRegistrado_Rejeita()
    {
        string raiz = LocalizarRaizDaSolucao();
        string yaml = File.ReadAllText(Path.Combine(raiz, "permissions.yml"))
            .Replace("decision_checks: []", "decision_checks: [check_inexistente]", StringComparison.Ordinal);

        Action acao = () => CatalogoPermissoesGerador.Ler(yaml);

        acao.Should().Throw<FormatException>()
            .WithMessage("*Decision check não registrado no backend*");
    }

    [Theory]
    [InlineData("context_scope")]
    [InlineData("decision_checks")]
    public void Ler_ListaComElementoNulo_Rejeita(string campo)
    {
        // "~" é o null explícito do YAML — forma válida de produzir um
        // elemento ausente dentro de uma lista sem ser erro de sintaxe (ex.:
        // "[,]" já é rejeitado pelo próprio parser YAML como nó incompleto,
        // antes mesmo de chegar na validação do catálogo).
        string raiz = LocalizarRaizDaSolucao();
        string yaml = File.ReadAllText(Path.Combine(raiz, "permissions.yml"))
            .Replace($"{campo}: []", $"{campo}: [~]", StringComparison.Ordinal);

        Action acao = () => CatalogoPermissoesGerador.Ler(yaml);

        acao.Should().Throw<FormatException>()
            .WithMessage("*Lista YAML não pode conter elementos vazios*");
    }

    [Fact]
    public void Ler_ListaComSintaxeInvalida_Rejeita()
    {
        string raiz = LocalizarRaizDaSolucao();
        string yaml = File.ReadAllText(Path.Combine(raiz, "permissions.yml"))
            .Replace("context_scope: []", "context_scope: [,]", StringComparison.Ordinal);

        Action acao = () => CatalogoPermissoesGerador.Ler(yaml);

        acao.Should().Throw<FormatException>()
            .WithMessage("*Catálogo YAML inválido*");
    }

    [Theory]
    [InlineData("processoID")]
    [InlineData("\"\"")]
    public void Ler_CampoContextoNaoRegistrado_Rejeita(string campo)
    {
        string raiz = LocalizarRaizDaSolucao();
        string yaml = File.ReadAllText(Path.Combine(raiz, "permissions.yml"))
            .Replace("context_scope: []", $"context_scope: [{campo}]", StringComparison.Ordinal);

        Action acao = () => CatalogoPermissoesGerador.Ler(yaml);

        acao.Should().Throw<FormatException>()
            .WithMessage("*Campo de contexto não registrado no backend*");
    }

    [Theory]
    [InlineData("\"artigo \\\"X\\\"\"", "artigo \"X\"")]
    [InlineData("'artigo X'", "artigo X")]
    [InlineData("artigo sem abrir\"", "artigo sem abrir\"")]
    [InlineData("artigo sem abrir'", "artigo sem abrir'")]
    public void Ler_EscalarYamlValido_DecodificaConteudo(string valorNaFonte, string conteudoEsperado)
    {
        // Um escalar YAML só é "quoted" quando ABRE com a aspa — aspa solta
        // no meio ou no fim de um plain scalar é apenas texto, não delimitador.
        // Aspas simples bem formadas e aspas duplas com escape são YAML
        // válido: o catálogo não reimplementa uma restrição que o parser
        // caseiro antigo impunha sem corresponder à gramática real do YAML.
        string raiz = LocalizarRaizDaSolucao();
        string yaml = File.ReadAllText(Path.Combine(raiz, "permissions.yml"))
            .Replace("base_legal_default: \"\"", $"base_legal_default: {valorNaFonte}", StringComparison.Ordinal);

        IReadOnlyList<EntradaPermissaoCatalogo> entradas = CatalogoPermissoesGerador.Ler(yaml);

        entradas.Should().Contain(x => x.BaseLegalPadrao == conteudoEsperado);
    }

    [Theory]
    [InlineData("\"artigo sem fechar")]
    [InlineData("'artigo sem fechar")]
    public void Ler_EscalarQuotedMalFormado_Rejeita(string valor)
    {
        // Abre o delimitador (aspa simples ou dupla) e não fecha — sintaxe
        // YAML incompleta, distinto do caso "aspa solta sem abrir" acima.
        string raiz = LocalizarRaizDaSolucao();
        string yaml = File.ReadAllText(Path.Combine(raiz, "permissions.yml"))
            .Replace("base_legal_default: \"\"", $"base_legal_default: {valor}", StringComparison.Ordinal);

        Action acao = () => CatalogoPermissoesGerador.Ler(yaml);

        acao.Should().Throw<FormatException>()
            .WithMessage("*Catálogo YAML inválido*");
    }

    [Fact]
    public void Ler_AcaoComDoisSegmentos_Rejeita()
    {
        string raiz = LocalizarRaizDaSolucao();
        string yaml = File.ReadAllText(Path.Combine(raiz, "permissions.yml"))
            .Replace(
                "codigo: configuracao:motivos-decisao-recursal:manter",
                "codigo: configuracao:motivos-decisao-recursal:manter:extra",
                StringComparison.Ordinal)
            .Replace("acao: manter", "acao: manter:extra", StringComparison.Ordinal);

        Action acao = () => CatalogoPermissoesGerador.Ler(yaml);

        acao.Should().Throw<FormatException>()
            .WithMessage("*Campo 'acao'*sem ':'*");
    }

    [Fact]
    public void Ler_CodigosDistintosGeramMesmoIdentificadorCSharp_Rejeita()
    {
        string yaml = string.Join(
            '\n',
            "permissions:",
            "  - codigo: configuracao:motivos-decisao:manter",
            "    modulo: configuracao",
            "    recurso: motivos-decisao",
            "    acao: manter",
            "    descricao: Primeira entrada",
            "    experimental: false",
            "    sensibilidade: interna",
            "    exportavel: false",
            "    audit_level: caso-uso",
            "    base_legal_default: \"\"",
            "    requires_mfa: false",
            "    requires_dual_approval: false",
            "    context_scope: []",
            "    allowed_subject_kind: []",
            "    decision_checks: []",
            "  - codigo: configuracao-motivos:decisao:manter",
            "    modulo: configuracao-motivos",
            "    recurso: decisao",
            "    acao: manter",
            "    descricao: Segunda entrada, hifen deslocado pelo separador dois-pontos",
            "    experimental: false",
            "    sensibilidade: interna",
            "    exportavel: false",
            "    audit_level: caso-uso",
            "    base_legal_default: \"\"",
            "    requires_mfa: false",
            "    requires_dual_approval: false",
            "    context_scope: []",
            "    allowed_subject_kind: []",
            "    decision_checks: []");

        Action acao = () => CatalogoPermissoesGerador.Ler(yaml);

        acao.Should().Throw<FormatException>()
            .WithMessage("*mesmo identificador C#*");
    }

    [Fact]
    public void CatalogoMinimo_GeraRequisitosSemConcessaoOuEscopo()
    {
        IReadOnlyList<EntradaPermissaoCatalogo> entradas = CatalogoPermissoesGerador.LerDeArquivo(
            Path.Combine(LocalizarRaizDaSolucao(), "permissions.yml"));

        entradas.Should().HaveCount(2);
        entradas.Should().OnlyContain(x => x.AllowedSubjectKind.Count == 0);
        entradas.Should().OnlyContain(x => x.ContextScope.Count == 0);
        entradas.Should().OnlyContain(x => !x.Exportavel);
        UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalManterRequirement.Permissao
            .Should().Be(UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalManter);
        UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalManterRequirement.Sensibilidade
            .Should().Be(Sensibilidade.Interna);
        UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalManterRequirement.EscopoContextoObrigatorio
            .Should().BeEmpty();
        UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalConsultarAuditoriaRequirement.Permissao
            .Should().Be(UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalConsultarAuditoria);
        UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalConsultarAuditoriaRequirement.EscopoContextoObrigatorio
            .Should().BeEmpty();
    }

    private static string LocalizarRaizDaSolucao()
    {
        DirectoryInfo? atual = new(AppContext.BaseDirectory);
        while (atual is not null && !File.Exists(Path.Combine(atual.FullName, "UniPlus.slnx")))
        {
            atual = atual.Parent;
        }

        return atual?.FullName
            ?? throw new DirectoryNotFoundException("UniPlus.slnx não encontrado a partir de AppContext.BaseDirectory.");
    }
}
