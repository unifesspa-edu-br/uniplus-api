namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Reflection;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using Type = System.Type;

/// <summary>
/// Fitness test do contrato de <b>pureza</b> do canonicalizador (ADR-0109 D6).
/// </summary>
/// <remarks>
/// <para>
/// O canonicalizador é a função que produz a evidência jurídica do certame. Ele
/// tem de ser uma <b>projeção pura</b>: mesma entrada, mesmos bytes, sempre —
/// em qualquer runtime, máquina ou momento. Uma dependência de repositório, de
/// relógio ou de rede quebra a reprodutibilidade <b>em silêncio</b>: o envelope
/// continuaria sendo produzido, só que dois recálculos do mesmo processo
/// poderiam divergir.
/// </para>
/// <para>
/// Este teste é o que impede que a próxima story "resolva" a falta de um dado
/// injetando um repositório aqui dentro — que é a saída tentadora e errada. O
/// caminho certo é acrescentar um campo a <see cref="EntradaCanonicalizacao"/>,
/// montado pelo handler, que é quem tem os repositórios (ADR-0042).
/// </para>
/// </remarks>
public sealed class CanonicalizerEProjecaoPuraTests
{
    private static readonly Type Canonicalizer = typeof(SnapshotPublicacaoCanonicalizer);

    private static readonly Architecture Arquitetura = new ArchLoader()
        .LoadAssemblies(
        [
            typeof(SnapshotPublicacaoCanonicalizer).Assembly,
            typeof(ISnapshotPublicacaoCanonicalizer).Assembly,
        ])
        .Build();

    /// <summary>Os tipos da projeção do certame, que vivem na Application ao lado dos handlers.</summary>
    private static readonly string[] NomesDaProjecaoDoCertame =
    [
        "ProjecaoDoCertamePublicado",
        "PrimitivasDaProjecao",
        "LeituraDoDocumentoDivulgado",
        "LeituraDoCertameEdital",
        "LeituraDoCertameVagas",
        "LeituraDoCertameCronograma",
        "LeituraDoCertameDocumentos",
    ];

    private static readonly string[] EsperadosNoCanonicalizador =
    [
        "SnapshotPublicacaoCanonicalizer",
        "EscritaCanonica",
        "SerializacaoDaCascata",
        "SerializacaoDaClassificacao",
        "SerializacaoDaDivulgacaoEDaTaxa",
        "SerializacaoDasEtapas",
        "SerializacaoDasExigencias",
        "SerializacaoDasVagas",
        "SerializacaoDoBonusEDoAtendimento",
        "SerializacaoDoCronograma",
        "SerializacaoDoEdital",
        "SerializacaoDosFatos",
        "SerializacaoDosFormularios",
        "SerializacaoDosPredicados",
    ];

    [Fact(DisplayName = "Todo tipo do namespace do envelope é puro — não depende de repositório, DbContext, relógio, service provider nem HttpClient")]
    public void NamespaceDoEnvelope_NaoDependeDeEstadoExterno()
    {
        IEnumerable<IType> tipos = Arquitetura.Types.Where(static t => t.Namespace.FullName == Canonicalizer.Namespace);

        tipos.Select(static t => t.Name).Should().Contain(EsperadosNoCanonicalizador,
            "a varredura tem de encontrar os serializadores; sem isso a regra passaria vazia");

        BarrarEstadoExterno(Types().That().Are(tipos), "a projeção do envelope é PURA (ADR-0109 D6)").Check(Arquitetura);
    }

    [Fact(DisplayName = "A projeção do certame publicado é pura — não depende de repositório, DbContext, relógio, service provider nem HttpClient")]
    public void ProjecaoDoCertame_NaoDependeDeEstadoExterno()
    {
        IEnumerable<IType> tipos = Arquitetura.Types.Where(static t => NomesDaProjecaoDoCertame.Contains(t.Name));

        tipos.Select(static t => t.Name).Should().Contain(NomesDaProjecaoDoCertame,
            "a varredura tem de encontrar todos os tipos da projeção do certame; sem isso a regra passaria vazia");

        BarrarEstadoExterno(Types().That().Are(tipos), "a mesma entrada e o mesmo endereço base produzem sempre a mesma resposta").Check(Arquitetura);
    }

    [Fact(DisplayName = "Controle: a regra de pureza reprova o handler que abre a retificação, que depende de repositório e de TimeProvider")]
    public void Controle_RegraReprovaTipoQueDependeDeEstadoExterno()
    {
        GivenTypesConjunction handler = Types().That().HaveFullName(
            "Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos.AbrirRetificacaoCommandHandler");

        foreach (string proibida in new[] { typeof(TimeProvider).FullName! })
        {
            IEnumerable<EvaluationResult> resultados = handler.Should().NotDependOnAnyTypesThat().HaveFullName(proibida).Evaluate(Arquitetura);
            resultados.Should().Contain(static r => !r.Passed, $"{proibida} está na arquitetura carregada e o handler depende dele");
        }

        IEnumerable<EvaluationResult> porNome = Types().That().HaveFullName(
            "Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos.AbrirRetificacaoCommandHandler")
            .Should().NotDependOnAnyTypesThat().HaveNameContaining("Repository").Evaluate(Arquitetura);
        porNome.Should().Contain(static r => !r.Passed);
    }

    [Fact(DisplayName = "Controle: cada tipo proibido é resolvido pela arquitetura — há ao menos um tipo carregado que depende dele")]
    public void Controle_CadaTipoProibidoEResolvido()
    {
        foreach (string proibida in TiposProibidos)
        {
            Arquitetura.Types
                .Any(t => t.Dependencies.Any(d => d.Target.FullName == proibida))
                .Should().BeTrue($"{proibida} precisa aparecer nas dependências carregadas, senão a regra que o proíbe seria vácua");
        }
    }

    private static readonly string[] TiposProibidos =
    [
        typeof(TimeProvider).FullName!,
        typeof(IServiceProvider).FullName!,
        typeof(HttpClient).FullName!,
    ];

    private static TypesShouldConjunctionWithDescription BarrarEstadoExterno(GivenTypesConjunction tipos, string motivo)
    {
        TypesShouldConjunction regra = tipos
            .Should()
            .NotDependOnAnyTypesThat()
            .HaveNameContaining("Repository")
            .AndShould()
            .NotDependOnAnyTypesThat()
            .HaveNameContaining("DbContext");

        foreach (string proibida in TiposProibidos)
        {
            regra = regra.AndShould().NotDependOnAnyTypesThat().HaveFullName(proibida);
        }

        return regra.Because($"{motivo}: dado que falta entra por parâmetro, montado pelo handler (ADR-0042)");
    }

    [Fact(DisplayName = "Canonicalizar_NaoEAssincrono — a projeção é síncrona: não há I/O a esperar")]
    public void Canonicalizar_NaoEAssincrono()
    {
        MethodInfo canonicalizar = Canonicalizer.GetMethod(nameof(ISnapshotPublicacaoCanonicalizer.Canonicalizar))!;

        canonicalizar.ReturnType.Should().Be<SnapshotCanonico>(
            "um retorno Task/ValueTask significaria que a projeção espera por I/O — e uma projeção pura não tem I/O a esperar");
    }

    [Fact(DisplayName = "Canonicalizar_RecebeEntradaUnica — a porta tem um único parâmetro, extensível sem quebrar a assinatura")]
    public void Canonicalizar_RecebeEntradaUnica()
    {
        MethodInfo canonicalizar = typeof(ISnapshotPublicacaoCanonicalizer)
            .GetMethod(nameof(ISnapshotPublicacaoCanonicalizer.Canonicalizar))!;

        ParameterInfo[] parametros = canonicalizar.GetParameters();

        parametros.Should().ContainSingle(
            "acrescentar dado ao envelope não pode significar mudar a assinatura da porta a cada story (ADR-0109 D6)");

        parametros[0].ParameterType.Should().Be<EntradaCanonicalizacao>();
    }
}
