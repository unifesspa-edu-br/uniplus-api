namespace Unifesspa.UniPlus.Authorization.UnitTests.Catalogo;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Decisao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// A impressão resume o que o catálogo registra de cada permissão e só muda quando esse conteúdo muda: a ordem de
/// registro dos módulos não a altera, e a classificação, o escopo, a reserva ou o módulo de qualquer permissão, sim.
/// </summary>
public sealed class ImpressaoDoCatalogoTests
{
    private static readonly LinhaDoCatalogo Primeira = new("alfa:itens:ler", "alfa", ClassificacaoDeProtecao.Interno, false, false);
    private static readonly LinhaDoCatalogo Segunda = new("beta:itens:manter", "beta", ClassificacaoDeProtecao.Pessoal, true, false);

    [Fact(DisplayName = "A impressão não depende da ordem das linhas")]
    public void Impressao_OrdemDasLinhas_NaoAltera() =>
        LinhaDoCatalogo.ImpressaoDe([Primeira, Segunda]).Should().Be(LinhaDoCatalogo.ImpressaoDe([Segunda, Primeira]));

    [Fact(DisplayName = "A impressão é um SHA-256 em hexadecimal minúsculo")]
    public void Impressao_Formato_EHexadecimalDe64Caracteres() =>
        LinhaDoCatalogo.ImpressaoDe([Primeira]).Should().MatchRegex("^[0-9a-f]{64}$");

    [Fact(DisplayName = "A impressão muda com a classificação, o escopo, a reserva e o módulo de uma permissão")]
    public void Impressao_ConteudoDiferente_Muda()
    {
        string base0 = LinhaDoCatalogo.ImpressaoDe([Primeira, Segunda]);

        LinhaDoCatalogo[] variacoes =
        [
            Segunda with { Classificacao = ClassificacaoDeProtecao.Sensivel },
            Segunda with { DeUnidade = false },
            Segunda with { Reservada = true },
            Segunda with { Modulo = "gama" },
        ];

        foreach (LinhaDoCatalogo variada in variacoes)
        {
            LinhaDoCatalogo.ImpressaoDe([Primeira, variada]).Should().NotBe(base0);
        }
    }

    [Fact(DisplayName = "A impressão do catálogo da aplicação é a mesma em qualquer ordem de registro dos módulos")]
    public void CatalogoDaAplicacao_OrdemDeRegistro_NaoAlteraAImpressao()
    {
        FonteFalsa alfa = new(Definicao("alfa:itens:ler", "Ler itens", ClassificacaoDeProtecao.Interno));
        FonteFalsa beta = new(Definicao("beta:itens:manter", "Manter itens", ClassificacaoDeProtecao.Pessoal, unidade: true));

        CatalogoDaAplicacao direta = new([alfa, beta]);
        CatalogoDaAplicacao invertida = new([beta, alfa]);

        direta.Impressao.Should().Be(invertida.Impressao);
        direta.Linhas.Select(static l => l.Codigo).Should().Equal("alfa:itens:ler", "beta:itens:manter");
        direta.Linhas[1].Should().Be(Segunda);
    }

    private static DefinicaoDePermissao Definicao(string codigo, string nome, ClassificacaoDeProtecao classe, bool unidade = false)
    {
        string[] partes = codigo.Split(':');
        PermissionRequirement requisito = PermissionRequirement
            .From(codigo, classe, "LGPD art. 7º, III", escopoContextoObrigatorio: unidade ? [CamposDeContexto.UnidadeProprietariaId] : null)
            .Value!;

        return new DefinicaoDePermissao(codigo, nome, $"Descrição de {nome}.", partes[0], partes[1], partes[2], requisito);
    }

    private sealed class FonteFalsa(DefinicaoDePermissao definicao) : IFonteDeCatalogo
    {
        public Type TipoDoEnum => typeof(FonteFalsa);

        public IReadOnlyList<DefinicaoDePermissao> Definicoes() => [definicao];
    }
}
