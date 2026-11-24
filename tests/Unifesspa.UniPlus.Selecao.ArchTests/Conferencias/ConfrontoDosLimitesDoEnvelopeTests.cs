namespace Unifesspa.UniPlus.Selecao.ArchTests.Conferencias;

using ArchUnitNET.Domain;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.TestesCompartilhados;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class ConfrontoDosLimitesDoEnvelopeTests
{
    [Fact]
    public void TodaConstante_TemEntradaNoQuadro()
    {
        ConferirQuadro(QuadroDeConfrontoDosLimites.Entradas);
        QuadroDeConfrontoDosLimites.Entradas.Select(entrada => entrada.Nome).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void QuadroSemUmaConstante_IdentificaOFaltante()
    {
        IEnumerable<EntradaDeConfrontoDoLimite> incompleto = QuadroDeConfrontoDosLimites.Entradas.Where(entrada => entrada.Nome != nameof(LimitesDoEnvelope.TipoAtoNome));
        Exception? falha = Record.Exception(() => ConferirQuadro(incompleto));
        falha.Should().NotBeNull();
        falha.Message.Should().Contain($"constante {nameof(LimitesDoEnvelope.TipoAtoNome)} de LimitesDoEnvelope sem confronto no quadro");
    }

    private static void ConferirQuadro(IEnumerable<EntradaDeConfrontoDoLimite> entradas) =>
        ConstantesSemConfronto(entradas).Should().BeEmpty();

    private static string[] ConstantesSemConfronto(IEnumerable<EntradaDeConfrontoDoLimite> entradas)
    {
        HashSet<string> nomes = entradas.Select(entrada => entrada.Nome).ToHashSet(StringComparer.Ordinal);
        IEnumerable<FieldMember> campos = FieldMembers().That().AreDeclaredIn(typeof(LimitesDoEnvelope)).GetObjects(ArquiteturaDaSelecao.Produto);
        return campos.Where(campo => !nomes.Contains(campo.Name))
            .Select(campo => $"constante {campo.Name} de LimitesDoEnvelope sem confronto no quadro").ToArray();
    }
}
