namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// As fronteiras do agregado <see cref="ProcessoSeletivo"/>: as regras vivem em políticas puras que
/// recebem uma vista somente leitura, e só a raiz e os planos que ela aplica mexem nas coleções.
/// </summary>
public sealed class FronteirasDoProcessoSeletivoTests
{
    private const string NamespaceDasPoliticas = "Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos";
    private const string EstadoFullName = NamespaceDasPoliticas + ".Edicao.EstadoDaConfiguracao";
    private const string ColecoesFullName = NamespaceDasPoliticas + ".Edicao.ColecoesDoProcesso";
    private const string PartesFullName = NamespaceDasPoliticas + ".Edicao.PartesDoProcesso";
    private const string ParteFullName = "Unifesspa.UniPlus.Selecao.Domain.Entities.IParteDoProcesso";

    private static readonly Architecture Arquitetura = new ArchLoader().LoadAssemblies(
        typeof(ProcessoSeletivo).Assembly,
        typeof(Application.Commands.ProcessosSeletivos.CriarProcessoSeletivoCommand).Assembly,
        typeof(Infrastructure.Persistence.SelecaoDbContext).Assembly).Build();

    /// <summary>Os tipos que calculam um plano e o aplicam sobre as coleções da raiz, com os registros do plano.</summary>
    private static readonly string[] Planos =
    [
        "ReconciliacaoDoCronograma", "PlanoDoCronograma",
        "AplicacaoDeModeloDeFormulario", "PlanoDoModelo",
        "RegrasDaColeta", "PlanoDaColeta", "PlanoDaRecopiaDoCatalogo",
        "ReposicaoDaConfiguracao",
        "RegrasDasOpcoesDeclaradas",
        "RegrasDosFormularios", "PlanoDoFormulario", "PlanoDosTermos",
        "RegrasDasExigencias", "PlanoDasExigencias",
        "RegrasDaClassificacao", "PlanoDaClassificacao",
    ];

    private static readonly string[] OperacoesDeVinculoEMutacao =
    [
        "VincularProcesso", "VincularProcessoSeletivo", "VincularFinalidade", "Substituir", "AtualizarDados",
        "ReporDe", "AtualizarDe", "RemapearFase", "RemapearAncora", "RemapearDocumentoExigido",
        "RecopiarDoCatalogo", "RecopiarApresentacaoDoCatalogo",
    ];

    [Fact(DisplayName = "A1: nenhuma política depende da raiz, salvo a fábrica da vista")]
    public void A1_PoliticasNaoDependemDaRaiz()
    {
        IArchRule regra = Types()
            .That().ResideInNamespaceMatching($@"^{NamespaceDasPoliticas.Replace(".", @"\.", StringComparison.Ordinal)}(\.|$)")
            .And().DoNotHaveFullNameStartingWith(EstadoFullName)
            .Should().NotDependOnAnyTypesThat().HaveFullName(typeof(ProcessoSeletivo).FullName!)
            .Because("a política recebe a vista da configuração; só a fábrica da vista conhece a raiz");

        regra.Check(Arquitetura);
    }

    [Fact(DisplayName = "A2: só os planos dependem de ColecoesDoProcesso, e só a raiz o constrói")]
    public void A2_SoOsPlanosTocamAsColecoes()
    {
        string[] dependentes = [.. Arquitetura.Types
            .Where(static t => t.Dependencies.Any(static d => d.Target.FullName == ColecoesFullName))
            .Select(static t => DonoDe(t.FullName))
            .Distinct(StringComparer.Ordinal)
            .Where(static dono => !EhPlanoOuRaiz(dono))
            .Order(StringComparer.Ordinal)];

        dependentes.Should().BeEmpty("a mutação das coleções é da raiz e dos planos que ela aplica");

        string[] construtores = [.. ChamadasNoDomain()
            .Where(static c => c.Callee is ConstructorInfo { DeclaringType.FullName: ColecoesFullName })
            .Select(static c => DonoDe(c.Caller.FullName!))
            .Where(static dono => dono != ColecoesFullName)
            .Distinct(StringComparer.Ordinal)];

        construtores.Should().Equal([typeof(ProcessoSeletivo).FullName], "só a raiz constrói as coleções vivas");
    }

    [Fact(DisplayName = "A3: as classes das políticas são estáticas, salvo os registros")]
    public void A3_PoliticasSaoEstaticas()
    {
        TiposDasPoliticas().Count().Should().BeGreaterThan(30, "a varredura tem de encontrar as políticas");

        string[] naoEstaticas = [.. TiposDasPoliticas()
            .Where(static t => t.IsClass && !(t.IsAbstract && t.IsSealed) && !EhRegistro(t))
            .Select(static t => t.FullName!)];

        naoEstaticas.Should().BeEmpty("a política é função pura da vista; estado vive na raiz");
    }

    [Fact(DisplayName = "A4: fora dos planos, ninguém vincula, substitui ou repõe partes do processo")]
    public void A4_SoOsPlanosVinculamEMutam()
    {
        (string Dono, string Tipo, string Nome)[] controles =
        [
            (NamespaceDasPoliticas + ".Restauracao.ReposicaoDaConfiguracao", PartesFullName, "Substituir"),
            (PartesFullName, ParteFullName, "VincularProcesso"),
        ];
        foreach ((string dono, string tipo, string nome) in controles)
        {
            ChamadasNoDomain()
                .Any(c => DonoDe(c.Caller.FullName!) == dono && c.Callee.DeclaringType!.FullName == tipo && c.Callee.Name == nome)
                .Should().BeTrue($"a leitura do IL tem de enxergar {dono} chamando {tipo}.{nome}, inclusive pela interface");
        }

        string[] infratores = [.. ChamadasNoDomain()
            .Where(static c => c.Callee.DeclaringType is { } tipo
                && (tipo.FullName is ParteFullName or PartesFullName || tipo.Namespace == typeof(ProcessoSeletivo).Namespace)
                && EhOperacaoDeVinculoOuMutacao(c.Callee.Name))
            .Select(static c => DonoDe(c.Caller.FullName!))
            .Where(static dono => dono.StartsWith(NamespaceDasPoliticas, StringComparison.Ordinal) && !EhPlanoOuRaiz(dono))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        infratores.Should().BeEmpty("quem vincula ou repõe uma parte é a raiz ou o plano que ela aplica");
    }

    [Fact(DisplayName = "A5: o Domain não depende de Application, Infrastructure, Wolverine nem EF Core")]
    public void A5_DomainNaoDependeDeCamadasExternas()
    {
        IArchRule regra = Types()
            .That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.Domain(\.|$)")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.(Application|Infrastructure)(\.|$)")
            .AndShould().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Wolverine(?!\.Attributes(\.|$))(\.|$)")
            .AndShould().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Microsoft\.EntityFrameworkCore(\.|$)");

        regra.Check(Arquitetura);
    }

    [Fact(DisplayName = "A6: toda parte do processo está em Domain.Entities")]
    public void A6_PartesDoProcessoVivemNasEntidades()
    {
        string[] fora = [.. typeof(ProcessoSeletivo).Assembly.GetTypes()
            .Where(static t => t.IsClass && t.GetInterfaces().Any(static i => i.FullName == ParteFullName))
            .Where(static t => t.Namespace != typeof(ProcessoSeletivo).Namespace)
            .Select(static t => t.FullName!)];

        fora.Should().BeEmpty("o vínculo com o processo é da entidade filha, não de um tipo de política");
    }

    private static bool EhOperacaoDeVinculoOuMutacao(string nome) =>
        OperacoesDeVinculoEMutacao.Any(op => nome.EndsWith(op, StringComparison.Ordinal));

    private static bool EhPlanoOuRaiz(string dono) =>
        dono == typeof(ProcessoSeletivo).FullName
        || dono is PartesFullName or ColecoesFullName
        || Planos.Contains(dono[(dono.LastIndexOf('.') + 1)..], StringComparer.Ordinal);

    private static bool EhRegistro(System.Type tipo) => tipo.GetMethod("<Clone>$") is not null;

    private static string DonoDe(string nomeCompleto)
    {
        int aninhado = nomeCompleto.IndexOf('+', StringComparison.Ordinal);
        return aninhado < 0 ? nomeCompleto : nomeCompleto[..aninhado];
    }

    private static IEnumerable<System.Type> TiposDasPoliticas() =>
        typeof(ProcessoSeletivo).Assembly.GetTypes()
            .Where(static t => t.Namespace is { } ns && ns.StartsWith(NamespaceDasPoliticas, StringComparison.Ordinal))
            .Where(static t => !System.Attribute.IsDefined(t, typeof(CompilerGeneratedAttribute)));

    private static IEnumerable<(System.Type Caller, MethodBase Callee)> ChamadasNoDomain()
    {
        foreach (System.Type tipo in typeof(ProcessoSeletivo).Assembly.GetTypes())
        {
            foreach (MethodBase metodo in tipo.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                         .OfType<MethodBase>())
            {
                foreach (MethodBase alvo in AlvosDasChamadas(metodo))
                {
                    yield return (tipo, alvo);
                }
            }
        }
    }

    private static IEnumerable<MethodBase> AlvosDasChamadas(MethodBase metodo)
    {
        byte[]? il = metodo.GetMethodBody()?.GetILAsByteArray();
        if (il is null)
        {
            yield break;
        }

        System.Type[]? tiposDoTipo = metodo.DeclaringType is { IsGenericType: true } declarante ? declarante.GetGenericArguments() : null;
        System.Type[]? tiposDoMetodo = metodo.IsGenericMethod ? metodo.GetGenericArguments() : null;
        for (int i = 0; i < il.Length;)
        {
            OpCode opcode = LerOpcode(il, ref i);
            if (opcode.OperandType is OperandType.InlineMethod)
            {
                MethodBase? alvo = Resolver(metodo.Module, BitConverter.ToInt32(il, i), tiposDoTipo, tiposDoMetodo);
                if (alvo is not null)
                {
                    yield return alvo;
                }
            }

            i += TamanhoDoOperando(opcode, il, i);
        }
    }

    private static MethodBase? Resolver(Module modulo, int token, System.Type[]? tiposDoTipo, System.Type[]? tiposDoMetodo)
    {
        try
        {
            return modulo.ResolveMethod(token, tiposDoTipo, tiposDoMetodo);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static OpCode LerOpcode(byte[] il, ref int posicao)
    {
        byte primeiro = il[posicao++];
        return primeiro == 0xFE ? TabelaDeDoisBytes[il[posicao++]] : TabelaDeUmByte[primeiro];
    }

    private static int TamanhoDoOperando(OpCode opcode, byte[] il, int posicao) => opcode.OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + (BitConverter.ToInt32(il, posicao) * 4),
        _ => 4,
    };

    private static readonly OpCode[] TabelaDeUmByte = TabelaDeOpcodes(umByte: true);

    private static readonly OpCode[] TabelaDeDoisBytes = TabelaDeOpcodes(umByte: false);

    private static OpCode[] TabelaDeOpcodes(bool umByte)
    {
        OpCode[] tabela = new OpCode[256];
        foreach (OpCode opcode in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Select(static f => (OpCode)f.GetValue(null)!))
        {
            ushort valor = (ushort)opcode.Value;
            if (umByte && opcode.Size == 1)
            {
                tabela[valor] = opcode;
            }
            else if (!umByte && opcode.Size == 2)
            {
                tabela[valor & 0xFF] = opcode;
            }
        }

        return tabela;
    }
}
