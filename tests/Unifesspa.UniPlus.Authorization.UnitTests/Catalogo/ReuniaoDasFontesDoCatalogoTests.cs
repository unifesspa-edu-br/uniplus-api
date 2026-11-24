namespace Unifesspa.UniPlus.Authorization.UnitTests.Catalogo;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// O que só se vê na reunião das fontes do catálogo da aplicação: código ou nome repetido entre enums e par repetido no
/// módulo, com o texto exato de cada problema e a ordem em que são acusados (primeiro as repetições, na ordem das
/// fontes e das definições, e por fim os pares).
/// </summary>
public sealed class ReuniaoDasFontesDoCatalogoTests
{
    private const string PrefixoDaMensagem = "O catálogo de permissões é inválido:";

    [Fact(DisplayName = "O código repetido em outro enum é acusado no enum que repete, com o texto exato")]
    public void CodigoRepetido_AcusaOEnumQueRepete()
    {
        IReadOnlyList<string> problemas = Problemas(
            Fonte<EnumAlfa>(Definicao("teste:itens:ler", "Ler itens")),
            Fonte<EnumBeta>(Definicao("teste:itens:ler", "Outro nome")));

        problemas.Should().Equal("EnumBeta: o código 'teste:itens:ler' já está declarado em outro enum.");
    }

    [Theory(DisplayName = "O nome repetido, sem distinguir caixa e ignorando espaços nas pontas, é acusado com o nome como veio")]
    [InlineData("LER ITENS")]
    [InlineData("  ler itens  ")]
    public void NomeRepetido_IgnoraCaixaEEspacosNasPontas(string nomeRepetido)
    {
        IReadOnlyList<string> problemas = Problemas(
            Fonte<EnumAlfa>(Definicao("teste:itens:ler", "Ler itens")),
            Fonte<EnumBeta>(Definicao("outro:itens:ler", nomeRepetido)));

        problemas.Should().Equal($"EnumBeta: o nome '{nomeRepetido}' já está declarado em outro enum.");
    }

    [Fact(DisplayName = "O código e o nome repetidos na mesma definição geram as duas mensagens, o código primeiro")]
    public void CodigoENomeRepetidos_AcusaOCodigoAntesDoNome()
    {
        IReadOnlyList<string> problemas = Problemas(
            Fonte<EnumAlfa>(Definicao("teste:itens:ler", "Ler itens")),
            Fonte<EnumBeta>(Definicao("teste:itens:ler", "Ler itens")));

        problemas.Should().Equal(
            "EnumBeta: o código 'teste:itens:ler' já está declarado em outro enum.",
            "EnumBeta: o nome 'Ler itens' já está declarado em outro enum.");
    }

    [Fact(DisplayName = "O par repetido no módulo é acusado depois das repetições, com as permissões em ordem ordinal")]
    public void ParRepetido_AcusaDepoisDasRepeticoes()
    {
        IReadOnlyList<string> problemas = Problemas(
            Fonte<EnumAlfa>(
                Definicao("teste:itens:ver", "Ver itens", NaturezaDoItem.Dado),
                Definicao("teste:itens:ler", "Ler itens", NaturezaDoItem.Dado)),
            Fonte<EnumBeta>(Definicao("teste:itens:ler", "Ler os itens", NaturezaDoItem.Dado)));

        problemas.Should().Equal(
            "EnumBeta: o código 'teste:itens:ler' já está declarado em outro enum.",
            "O par (Dado, Interno) do módulo 'teste' aponta para mais de uma permissão: teste:itens:ler, teste:itens:ler, teste:itens:ver.");
    }

    [Fact(DisplayName = "O par em módulos diferentes não é repetição")]
    public void ParIgualEmOutroModulo_NaoRepete()
    {
        Action reunir = () => _ = new CatalogoDaAplicacao(
        [
            Fonte<EnumAlfa>(Definicao("teste:itens:ver", "Ver itens", NaturezaDoItem.Dado)),
            Fonte<EnumBeta>(Definicao("outro:itens:ver", "Ver outros itens", NaturezaDoItem.Dado)),
        ]);

        reunir.Should().NotThrow();
    }

    [Fact(DisplayName = "A exceção da subida lista cada problema numa linha, depois do cabeçalho")]
    public void Excecao_ListaUmProblemaPorLinha()
    {
        Action reunir = () => _ = new CatalogoDaAplicacao(
        [
            Fonte<EnumAlfa>(Definicao("teste:itens:ler", "Ler itens")),
            Fonte<EnumBeta>(Definicao("teste:itens:ler", "Ler itens")),
        ]);

        reunir.Should().Throw<CatalogoDePermissoesInvalidoException>().WithMessage(
            PrefixoDaMensagem
            + $"{Environment.NewLine} - EnumBeta: o código 'teste:itens:ler' já está declarado em outro enum."
            + $"{Environment.NewLine} - EnumBeta: o nome 'Ler itens' já está declarado em outro enum.");
    }

    [Fact(DisplayName = "As definições e as linhas saem ordenadas, e a busca por código só acha o que existe")]
    public void CatalogoValido_OrdenaEBuscaPorCodigo()
    {
        CatalogoDaAplicacao catalogo = new(
        [
            Fonte<EnumBeta>(Definicao("teste:itens:manter", "Manter itens")),
            Fonte<EnumAlfa>(Definicao("teste:itens:ler", "Ler itens")),
        ]);

        catalogo.Definicoes.Select(static d => d.Codigo).Should().Equal("teste:itens:ler", "teste:itens:manter");
        catalogo.Linhas.Select(static l => l.Codigo.Valor).Should().Equal("teste:itens:ler", "teste:itens:manter");
        catalogo.TentarObter("teste:itens:ler", out DefinicaoDePermissao? achada).Should().BeTrue();
        achada!.Nome.Should().Be("Ler itens");
        catalogo.TentarObter("teste:itens:inexistente", out _).Should().BeFalse();
    }

    [Fact(DisplayName = "A impressão de linhas conhecidas gera o hash esperado, byte a byte")]
    public void Impressao_DeLinhasConhecidas_EAConstante()
    {
        LinhaDoCatalogo[] linhas =
        [
            new(CodigoDePermissao.De("beta:itens:manter"), ClassificacaoDeProtecao.Pessoal, true, false),
            new(CodigoDePermissao.De("alfa:itens:ler"), ClassificacaoDeProtecao.Interno, false, false),
        ];

        LinhaDoCatalogo.ImpressaoDe(linhas).Valor.Should().Be("a8d7713738473dc19f3e1fe9922d648c37eaffee6a5a1a655397de6bc20e178a");
    }

    private static IReadOnlyList<string> Problemas(params IFonteDeCatalogo[] fontes)
    {
        Action reunir = () => _ = new CatalogoDaAplicacao(fontes);

        return reunir.Should().Throw<CatalogoDePermissoesInvalidoException>().Which.Problemas;
    }

    private static FonteFalsa Fonte<TEnum>(params DefinicaoDePermissao[] definicoes)
        where TEnum : struct, Enum =>
        new(typeof(TEnum), definicoes);

    private static DefinicaoDePermissao Definicao(string codigo, string nome, NaturezaDoItem natureza = NaturezaDoItem.Nenhuma)
    {
        string[] partes = codigo.Split(':');
        PermissionRequirement requisito = PermissionRequirement.From(codigo, ClassificacaoDeProtecao.Interno, "LGPD art. 7º, III").Value!;

        return new DefinicaoDePermissao(codigo, nome, $"Descrição de {nome}.", partes[0], partes[1], partes[2], requisito, Natureza: natureza);
    }

    private sealed class FonteFalsa(Type tipo, IReadOnlyList<DefinicaoDePermissao> definicoes) : IFonteDeCatalogo
    {
        public Type TipoDoEnum => tipo;

        public IReadOnlyList<DefinicaoDePermissao> Definicoes() => definicoes;
    }

    private enum EnumAlfa
    {
        Um,
    }

    private enum EnumBeta
    {
        Um,
    }
}
