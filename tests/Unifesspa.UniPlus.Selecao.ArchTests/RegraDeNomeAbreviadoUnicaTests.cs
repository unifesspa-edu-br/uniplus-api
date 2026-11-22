namespace Unifesspa.UniPlus.Selecao.ArchTests;


using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class RegraDeNomeAbreviadoUnicaTests
{
    [Fact]
    public void RegrasDeNomeAbreviado_ConheceExatamenteUmaRegra()
    {
        ArchUnitNET.Domain.Architecture arquitetura = ArquiteturaDaSelecao.Produto;

        FieldMembers().That().AreDeclaredIn(typeof(RegrasDeNomeAbreviado)).And().ArePublic()
            .GetObjects(arquitetura).Should().ContainSingle(
                "a regra vigente só reproduz a versão congelada enquanto houver uma única regra conhecida");
    }
}
