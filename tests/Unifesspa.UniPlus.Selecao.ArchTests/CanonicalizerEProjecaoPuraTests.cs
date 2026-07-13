namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

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

    /// <summary>Dependências que denunciam que a projeção deixou de ser pura.</summary>
    private static readonly string[] DependenciasProibidas =
    [
        "Repository",      // leitura de banco
        "DbContext",       // idem
        "TimeProvider",    // relógio — a mesma entrada produziria bytes distintos
        "IServiceProvider", // service locator: esconde qualquer uma das anteriores
        "HttpClient",      // rede
    ];

    [Fact(DisplayName = "Canonicalizer_NaoTemDependencias — a projeção não recebe repositório, DbContext, relógio, service provider nem HttpClient")]
    public void Canonicalizer_NaoTemDependencias()
    {
        IEnumerable<Type> tiposInjetados = Canonicalizer
            .GetConstructors()
            .SelectMany(static c => c.GetParameters())
            .Select(static p => p.ParameterType);

        IEnumerable<Type> tiposDeCampo = Canonicalizer
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(static f => f.FieldType);

        foreach (Type tipo in tiposInjetados.Concat(tiposDeCampo))
        {
            foreach (string proibida in DependenciasProibidas)
            {
                tipo.Name.Should().NotContain(proibida,
                    $"o canonicalizador é uma projeção PURA (ADR-0109 D6) — '{tipo.Name}' denuncia estado externo. " +
                    "Dado que falta ao envelope entra por EntradaCanonicalizacao, montada pelo handler.");
            }
        }
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
