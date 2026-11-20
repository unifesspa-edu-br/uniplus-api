namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A conferência na subida (ADR-0176) só vê os assemblies que o executável alcança pelas referências estáticas do
/// metadado. O assembly que nenhum dos três executáveis alcança, como o de marcador vazio que nenhum tipo de produção
/// referencia, ficaria de fora em silêncio se declarasse um enum com código: esta regra transforma o limite do alcance
/// em falha de CI.
/// </summary>
public sealed class EnumComCodigoSempreNoAlcanceDeUmExecutavelTests
{
    private static readonly Assembly[] ForaDoAlcance = AssembliesForaDoAlcanceDosExecutaveis(AssembliesDoProduto.Todos);

    [Fact(DisplayName = "Há assemblies do produto fora do alcance dos três executáveis, e a regra examina os tipos deles")]
    public void Produto_AssembliesForaDoAlcance_TemTiposParaExaminar()
    {
        ForaDoAlcance.Should().NotBeEmpty("os assemblies de marcador vazio ficam fora do alcance, e sem eles a regra passaria vazia");
        ForaDoAlcance.SelectMany(static a => a.GetTypes()).Should().NotBeEmpty("o assembly de marcador declara ao menos a classe de marcador");
    }

    [Fact(DisplayName = "Nenhum assembly do produto fora do alcance dos executáveis declara enum com código")]
    public void Produto_AssemblyForaDoAlcance_NaoDeclaraEnumComCodigo() =>
        EnumsComCodigoEm(ForaDoAlcance).Should().BeEmpty(
            "a conferência na subida não vê o assembly que nenhum executável alcança; declare o enum num assembly que um executável referencie");

    [Fact(DisplayName = "O assembly fora do alcance que declara enum com código é reprovado, com o assembly e o enum na mensagem")]
    public void AssemblyDeExemploForaDoAlcanceComEnumComCodigo_Reprova()
    {
        // Os dois enums do assembly emitido declaram código em algum membro, e isso basta; o defeito deles não importa aqui.
        Assembly emitido = ExecutavelComEnumsDefeituosos.Criar();

        string[] reprovados = EnumsComCodigoEm(AssembliesForaDoAlcanceDosExecutaveis([emitido]));

        reprovados.Should().BeEquivalentTo(
        [
            $"{emitido.GetName().Name}: {ExecutavelComEnumsDefeituosos.EnumComCodigoRepetido}",
            $"{emitido.GetName().Name}: {ExecutavelComEnumsDefeituosos.EnumComCodigoAusente}",
        ]);
    }

    [Fact(DisplayName = "O assembly que um executável alcança e que declara enum com código não é reprovado")]
    public void AssemblyDeExemploNoAlcanceDeUmExecutavelComEnumComCodigo_NaoEReprovado()
    {
        Assembly dominioDaAutorizacao = typeof(SituacaoDoAcessoDoPerfil).Assembly;

        EnumsComCodigoEm([dominioDaAutorizacao]).Should().NotBeEmpty("o assembly de exemplo declara enum com código");
        AssembliesForaDoAlcanceDosExecutaveis([dominioDaAutorizacao]).Should().BeEmpty("o Host o alcança");
    }

    private static Assembly[] AssembliesForaDoAlcanceDosExecutaveis(IEnumerable<Assembly> assemblies)
    {
        HashSet<Assembly> alcancados = [.. ExecutaveisDoProduto.Todos.SelectMany(AssembliesDoUniPlus.AlcancadosAPartirDe)];

        return [.. assemblies.Where(a => !alcancados.Contains(a))];
    }

    private static string[] EnumsComCodigoEm(IEnumerable<Assembly> assemblies) =>
    [
        .. assemblies.SelectMany(static a => a.GetTypes().Where(EnumsDeContrato.EhEnumQueDeclaraCodigo).Select(t => $"{a.GetName().Name}: {t.FullName}")),
    ];
}
