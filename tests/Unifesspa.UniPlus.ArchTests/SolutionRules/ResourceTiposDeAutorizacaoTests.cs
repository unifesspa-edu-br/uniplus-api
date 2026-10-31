namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// O alvo técnico da decisão de acesso se chama resource, e "recurso" fica para o termo de negócio. O tipo de
/// resource é declarado numa única classe por módulo, no formato de <see cref="ResourceTipos"/>, e nenhum valor se
/// repete entre módulos.
/// </summary>
public sealed class ResourceTiposDeAutorizacaoTests
{
    private const string PrefixoDoTipo = "ResourceTipos";

    [Fact(DisplayName = "Todo tipo de resource declarado pelos módulos segue a convenção")]
    public void TiposDeclarados_SeguemAConvencao()
    {
        ConferenciaDeResourceTipos.ForaDaConvencao(TiposPorModulo()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Nenhum tipo de resource se repete, nem dentro nem entre módulos")]
    public void TiposDeclarados_SaoUnicos()
    {
        ConferenciaDeResourceTipos.Repetidos(TiposPorModulo()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Todo tipo usado em [ExigePermissao] e [ExigePermissaoNoProcesso] vem da classe do módulo")]
    public void TiposUsados_VemDaClasseDoModulo()
    {
        IReadOnlyDictionary<string, List<string>> declarados = TiposPorModulo();

        IReadOnlyList<(string Modulo, string Local, string? Tipo)> usos = [.. ApisDoProduto()
            .SelectMany(static api => UsosDeTipo(api.Assembly).Select(uso => (api.Modulo, uso.Local, uso.Tipo)))];

        usos.Should().NotBeEmpty("a varredura precisa alcançar os usos para valer como conferência");
        ConferenciaDeResourceTipos.UsadosForaDaClasse(declarados, usos).Should().BeEmpty();
    }

    [Fact(DisplayName = "Nenhum controller declara constante própria de tipo de resource")]
    public void Controllers_NaoDeclaramTipoProprio()
    {
        IEnumerable<Type> controllers = ApisDoProduto()
            .SelectMany(static api => Tipos(api.Assembly))
            .Where(static t => t.Name.EndsWith("Controller", StringComparison.Ordinal));

        ConferenciaDeResourceTipos.ConstantesDeTipoNoTipo(controllers).Should().BeEmpty();
    }

    [Fact(DisplayName = "Nenhum identificador da autorização usa Recurso para o alvo da decisão")]
    public void Autorizacao_NaoUsaRecursoNosIdentificadores()
    {
        IEnumerable<Type> escopo = EscopoDaAutorizacao();

        escopo.Should().NotBeEmpty();
        ConferenciaDeResourceTipos.IdentificadoresComRecurso(escopo).Should().BeEmpty();
    }

    [Fact(DisplayName = "Nomes de negócio do recurso ficam fora da conferência")]
    public void NomesDeNegocio_NaoSaoApontados()
    {
        Type regraDeRecurso = Assembly.Load("Unifesspa.UniPlus.Selecao.Domain")
            .GetTypes().Single(static t => t.Name == "RegraRecursoFase");

        EscopoDaAutorizacao().Should().NotContain(regraDeRecurso);
    }

    [Fact(DisplayName = "A conferência aponta tipo repetido entre módulos")]
    public void Conferencia_ApontaTipoRepetido()
    {
        Dictionary<string, List<string>> declarados = new()
        {
            ["Configuracao"] = ["modalidade"],
            ["Selecao"] = ["modalidade"],
        };

        ConferenciaDeResourceTipos.Repetidos(declarados).Should().ContainSingle()
            .Which.Should().Contain("Configuracao").And.Contain("Selecao");
    }

    [Fact(DisplayName = "ExigePermissaoNoProcesso recusa na construção o tipo de resource fora da convenção")]
    public void ExigePermissaoNoProcesso_TipoForaDaConvencao_Lanca()
    {
        Func<Unifesspa.UniPlus.Selecao.API.Authorization.ExigePermissaoNoProcessoAttribute> criar = () =>
            new(Unifesspa.UniPlus.Selecao.Application.Permissoes.PermissaoSelecao.ProcessosConfigurar) { ResourceTipo = "ProcessoSeletivo" };

        criar.Should().Throw<ArgumentException>().WithMessage("*kebab-case*");
    }

    [Fact(DisplayName = "ExigePermissaoNoProcesso recusa o tipo nulo com a mesma exceção orientadora")]
    public void ExigePermissaoNoProcesso_TipoNulo_Lanca()
    {
        Func<Unifesspa.UniPlus.Selecao.API.Authorization.ExigePermissaoNoProcessoAttribute> criar = () =>
            new(Unifesspa.UniPlus.Selecao.Application.Permissoes.PermissaoSelecao.ProcessosConfigurar) { ResourceTipo = null! };

        criar.Should().Throw<ArgumentException>().WithMessage("*kebab-case*");
    }

    [Fact(DisplayName = "A conferência de unicidade inclui o tipo fixo operacao")]
    public void TiposPorModulo_IncluiOTipoOperacao()
    {
        TiposPorModulo().Values.SelectMany(static v => v).Should().Contain(ResourceTipos.Operacao);
    }

    [Fact(DisplayName = "A conferência aponta tipo repetido dentro do mesmo módulo")]
    public void Conferencia_ApontaTipoRepetidoNoMesmoModulo()
    {
        Dictionary<string, List<string>> declarados = new() { ["Selecao"] = ["inscricao", "inscricao"] };

        ConferenciaDeResourceTipos.Repetidos(declarados).Should().ContainSingle().Which.Should().Contain("inscricao");
    }

    [Fact(DisplayName = "A conferência aponta tipo usado fora da classe do módulo")]
    public void Conferencia_ApontaTipoForaDaClasse()
    {
        Dictionary<string, List<string>> declarados = new() { ["Selecao"] = ["inscricao"] };

        ConferenciaDeResourceTipos.UsadosForaDaClasse(declarados, [("Selecao", "Fixture.Acao", "TipoSolto")])
            .Should().ContainSingle().Which.Should().Contain("Fixture.Acao");
    }

    [Fact(DisplayName = "A conferência aponta controller com constante de tipo de resource")]
    public void Conferencia_ApontaControllerComConstanteDeTipo()
    {
        ConferenciaDeResourceTipos
            .ConstantesDeTipoNoTipo([typeof(ControllerComTipoProprio)])
            .Should().ContainSingle().Which.Should().Contain(nameof(ControllerComTipoProprio));
    }

    [Fact(DisplayName = "A conferência aponta identificador da autorização que usa Recurso")]
    public void Conferencia_ApontaIdentificadorComRecurso()
    {
        ConferenciaDeResourceTipos.IdentificadoresComRecurso([typeof(ContratoComRecursoId)])
            .Should().Contain(static achado => achado.Contains("RecursoId", StringComparison.Ordinal));
    }

    private static Dictionary<string, List<string>> TiposPorModulo()
    {
        Dictionary<string, List<string>> tipos = [];

        foreach ((string modulo, Assembly assembly) in Assemblies("Application"))
        {
            Type[] classes = [.. Tipos(assembly).Where(static t => t.Name.StartsWith(PrefixoDoTipo, StringComparison.Ordinal))];
            classes.Should().HaveCountLessThanOrEqualTo(1, $"o módulo {modulo} declara os tipos de resource numa única classe");

            // Módulo sem classe de tipos não declara nenhum: se algum atributo seu usar ResourceTipo, a conferência
            // de origem reprova o uso.
            tipos[modulo] = [.. classes
                .SelectMany(static c => c.GetFields(BindingFlags.Public | BindingFlags.Static))
                .Where(static f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(static f => (string)f.GetRawConstantValue()!)];
        }

        tipos.Should().NotBeEmpty();
        tipos["shared"] = [ResourceTipos.Operacao];
        return tipos;
    }

    private static IEnumerable<(string Modulo, Assembly Assembly)> ApisDoProduto() => Assemblies("API");

    private static IEnumerable<(string Modulo, Assembly Assembly)> Assemblies(string camada) => Directory
        .GetFiles(AppContext.BaseDirectory, $"Unifesspa.UniPlus.*.{camada}.dll")
        .Select(static caminho => Path.GetFileNameWithoutExtension(caminho))
        .Where(static nome => nome.Count(static c => c == '.') == 3)
        .Select(static nome => (nome.Split('.')[2], Assembly.Load(nome)));

    private static IEnumerable<(string Local, string? Tipo)> UsosDeTipo(Assembly assembly)
    {
        foreach (Type tipo in Tipos(assembly))
        {
            foreach (MemberInfo membro in tipo.GetMembers(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Append(tipo))
            {
                foreach (Attribute atributo in membro.GetCustomAttributes(inherit: false).OfType<Attribute>())
                {
                    string nome = atributo.GetType().Name;
                    if (!nome.StartsWith("ExigePermissao", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    PropertyInfo? propriedade = atributo.GetType().GetProperty("ResourceTipo");
                    if (propriedade?.GetValue(atributo) is string valor)
                    {
                        yield return ($"{tipo.Name}.{membro.Name}", valor);
                    }
                }
            }
        }
    }

    private static IEnumerable<Type> EscopoDaAutorizacao()
    {
        Assembly autorizacao = typeof(ResourceTipos).Assembly;
        Assembly infraCore = Assembly.Load("Unifesspa.UniPlus.Infrastructure.Core");
        Assembly selecaoApplication = Assembly.Load("Unifesspa.UniPlus.Selecao.Application");
        Assembly selecaoApi = Assembly.Load("Unifesspa.UniPlus.Selecao.API");

        return
        [
            .. Tipos(autorizacao),
            .. Tipos(infraCore).Where(static t => t.Namespace == "Unifesspa.UniPlus.Infrastructure.Core.Authorization"),
            .. Tipos(selecaoApplication).Where(static t => t.Name == "AcessoAoProcesso"),
            .. Tipos(selecaoApi).Where(static t => t.Name == "ExigePermissaoNoProcessoAttribute"),
        ];
    }

    private static IEnumerable<Type> Tipos(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }

    private sealed class ControllerComTipoProprio
    {
        private const string ResourceTipo = "inscricao";

        public static string Ler() => ResourceTipo;
    }

    private sealed class ContratoComRecursoId
    {
        public Guid? RecursoId { get; init; }
    }
}

/// <summary>As regras do teste de arquitetura, separadas dos dados para que cada uma tenha a sua contraprova.</summary>
internal static class ConferenciaDeResourceTipos
{
    private const BindingFlags Todos =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static IReadOnlyList<string> ForaDaConvencao(IReadOnlyDictionary<string, List<string>> declarados) =>
    [
        .. declarados.SelectMany(static par => par.Value
            .Where(static tipo => !ResourceTipos.SegueAConvencao(tipo))
            .Select(tipo => $"{par.Key}: '{tipo}' fora do kebab-case")),
    ];

    public static IReadOnlyList<string> Repetidos(IReadOnlyDictionary<string, List<string>> declarados) =>
    [
        .. declarados
            .SelectMany(static par => par.Value.Select(tipo => (Modulo: par.Key, Tipo: tipo)))
            .GroupBy(static x => x.Tipo)
            .Where(static g => g.Count() > 1)
            .Select(static g => $"'{g.Key}' em {string.Join(", ", g.Select(static x => x.Modulo))}"),
    ];

    public static IReadOnlyList<string> UsadosForaDaClasse(
        IReadOnlyDictionary<string, List<string>> declarados,
        IEnumerable<(string Modulo, string Local, string? Tipo)> usos) =>
    [
        .. usos
            .Where(uso => uso.Tipo is not null
                && !(declarados.TryGetValue(uso.Modulo, out List<string>? doModulo) && doModulo.Contains(uso.Tipo)))
            .Select(static uso => $"{uso.Local}: '{uso.Tipo}' não vem da classe de tipos de {uso.Modulo}"),
    ];

    public static IReadOnlyList<string> ConstantesDeTipoNoTipo(IEnumerable<Type> tipos) =>
    [
        .. tipos.SelectMany(tipo => tipo.GetFields(Todos)
            .Where(static f => f.IsLiteral && f.FieldType == typeof(string)
                && f.Name.Contains("ResourceTipo", StringComparison.Ordinal))
            .Select(f => $"{tipo.Name}.{f.Name} declara o tipo de resource '{f.GetRawConstantValue()}'")),
    ];

    public static IReadOnlyList<string> IdentificadoresComRecurso(IEnumerable<Type> tipos) =>
    [
        .. tipos
            .Where(static t => !t.Name.StartsWith('<'))
            .SelectMany(static tipo => Nomes(tipo).Where(static n => n.Nome.Contains("recurso", StringComparison.OrdinalIgnoreCase))
                .Select(n => $"{tipo.Name}: {n.Nome} ({n.Tipo})")),
    ];

    private static IEnumerable<(string Nome, string Tipo)> Nomes(Type tipo)
    {
        yield return (tipo.Name, "tipo");

        foreach (MemberInfo membro in tipo.GetMembers(Todos).Where(static m => !m.Name.StartsWith('<')))
        {
            yield return (membro.Name, "membro");

            if (membro is FieldInfo { IsLiteral: true } campo && campo.GetRawConstantValue() is string valor)
            {
                yield return (valor, $"valor de {campo.Name}");
            }

            if (membro is MethodBase metodo)
            {
                foreach (ParameterInfo parametro in metodo.GetParameters())
                {
                    yield return (parametro.Name ?? string.Empty, $"parâmetro de {metodo.Name}");
                }
            }
        }
    }
}
