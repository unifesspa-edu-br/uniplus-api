namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Conferencias;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

/// <summary>
/// Confere a premissa de <see cref="RegrasDeNomeAbreviado"/> que sustenta a decisão de
/// congelamento (issue #563, D3): <b>derivar</b> a regra vigente no instante da
/// canonicalização só é indistinguível de <b>reinjetar</b> a regra congelada enquanto existir
/// <b>exatamente uma</b> regra conhecida — com uma só, os dois mecanismos produzem sempre o
/// mesmo literal.
/// </summary>
/// <remarks>
/// <para>
/// O canonicalizador (<c>SerializarDivulgacao</c>) não recebe a regra congelada por parâmetro —
/// ele deriva <c>abrevia ? RegrasDeNomeAbreviado.Vigente : null</c>, direto do agregado. Isso é
/// seguro <b>hoje</b> porque não há como a vigente ter mudado entre a publicação de uma versão
/// antiga e a recanonicalização dela na conferência de uma retificação: as duas acontecem sob o
/// mesmo binário, sem regime de codec por versão em produção.
/// </para>
/// <para>
/// No dia em que uma <b>segunda</b> regra de abreviação for registrada, essa garantia
/// desaparece — e este teste é quem avisa. Ele não impede a segunda regra; ele impede que ela
/// entre <b>em silêncio</b>, sem que alguém decida o que fazer com a derivação estática que
/// deixou de valer.
/// </para>
/// </remarks>
public sealed class RegraDeNomeAbreviadoUnicaTests
{
    [Fact(DisplayName = "RegrasDeNomeAbreviado conhece exatamente UMA regra — derivar a vigente na canonicalização deixa de ser seguro no dia em que houver uma segunda")]
    public void RegrasDeNomeAbreviado_ConheceExatamenteUmaRegra()
    {
        FieldInfo[] identificadoresConhecidos = typeof(RegrasDeNomeAbreviado)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static f => f.IsLiteral && f.FieldType == typeof(string))
            .ToArray();

        identificadoresConhecidos.Should().ContainSingle(
            "a regra vigente só reproduz a versão congelada enquanto houver uma única regra conhecida");
    }
}
