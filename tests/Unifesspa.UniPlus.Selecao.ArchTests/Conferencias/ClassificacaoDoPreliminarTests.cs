namespace Unifesspa.UniPlus.Selecao.ArchTests.Conferencias;

using ArchUnitNET.Domain;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class ClassificacaoDoPreliminarTests
{
    [Fact]
    public void Fase_PropriedadesPublicasCorrespondemAClassificacao() =>
        Conferir(typeof(FaseCronograma), ClassificacaoDasPropriedadesDoPreliminarTests.DaFase.Keys);

    [Fact]
    public void Etapa_PropriedadesPublicasCorrespondemAClassificacao() =>
        Conferir(typeof(EtapaProcesso), ClassificacaoDasPropriedadesDoPreliminarTests.DaEtapa.Keys);

    [Fact]
    public void ClassificacaoSemUmaPropriedade_EncontraAFalta()
    {
        IEnumerable<string> classificadas = ClassificacaoDasPropriedadesDoPreliminarTests.DaFase.Keys
            .Where(static nome => nome != nameof(FaseCronograma.UnidadeResponsavel));

        Propriedades(typeof(FaseCronograma)).Except(classificadas).Should().ContainSingle()
            .Which.Should().Be(nameof(FaseCronograma.UnidadeResponsavel));
    }

    private static void Conferir(Type tipo, IEnumerable<string> classificadas)
    {
        string[] propriedades = [.. Propriedades(tipo)];
        propriedades.Except(classificadas).Should().BeEmpty();
        classificadas.Except(propriedades).Should().BeEmpty();
    }

    private static IEnumerable<string> Propriedades(Type tipo) =>
        Propriedades(Types().That().Are(tipo).GetObjects(ArquiteturaDaSelecao.Produto).Single());

    private static IEnumerable<string> Propriedades(IType tipo) =>
        PropertyMembers().That().AreDeclaredIn(tipo).And().ArePublic().GetObjects(ArquiteturaDaSelecao.Produto)
            .Where(static propriedade => propriedade.IsStatic == false).Select(static propriedade => propriedade.Name)
            .Concat(tipo is Class { BaseClass: not null } classe ? Propriedades(classe.BaseClass) : []);
}
