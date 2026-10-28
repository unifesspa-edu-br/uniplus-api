namespace Unifesspa.UniPlus.Authorization.UnitTests.Catalogo;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Enums;

/// <summary>
/// O catálogo recusa, com mensagem que nomeia o membro, todo enum que derrubaria a
/// decisão em execução: atributo ausente, código repetido ou fora do módulo, texto
/// vazio ou repetido e metadado que a decisão não sabe verificar (ADR-0151).
/// </summary>
public sealed class CatalogoDePermissoesTests
{
    [Fact(DisplayName = "Enum válido expõe código, textos, partes do código e requisito")]
    public void Todas_EnumValido_ExpoeADefinicao()
    {
        DefinicaoDePermissao definicao = CatalogoDePermissoes<Valido>.Obter(Valido.Manter);

        definicao.Codigo.Should().Be("teste:itens:manter");
        definicao.Nome.Should().Be("Manter itens");
        definicao.Descricao.Should().Be("Cria e edita itens.");
        (definicao.Modulo, definicao.Recurso, definicao.Acao).Should().Be(("teste", "itens", "manter"));
        definicao.Requisito.Permissao.Should().Be("teste:itens:manter");
        definicao.Requisito.Sensibilidade.Should().Be(Sensibilidade.Pessoal);
        definicao.Requisito.BaseLegalPadrao.Should().Be("LGPD art. 7º, III");
        definicao.Requisito.EscopoContextoObrigatorio.Should().Equal("processoId");
    }

    [Fact(DisplayName = "O código vem do atributo, não do nome do membro")]
    public void Obter_CodigoNaoEDerivadoDoNomeDoMembro() =>
        CatalogoDePermissoes<Valido>.Obter(Valido.Consultar).Codigo.Should().Be("teste:itens:ler");

    [Fact(DisplayName = "Valor que não é membro do enum falha nomeando o enum e o valor recebido")]
    public void Obter_ValorIndefinido_NomeiaOEnumEOValor()
    {
        Action obter = () => CatalogoDePermissoes<Valido>.Obter((Valido)99);

        obter.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*99*Valido*");
    }

    [Fact(DisplayName = "Membro sem atributo derruba o catálogo e é nomeado")]
    public void Todas_MembroSemAtributo_Falha() =>
        Falha<SemAtributo>().Should().ContainSingle(p => p.Contains("SemAtributo.Orfao") && p.Contains("falta [PermissaoAttribute]"));

    [Fact(DisplayName = "Código repetido no enum derruba o catálogo e nomeia os dois membros")]
    public void Todas_CodigoRepetido_Falha() =>
        Falha<CodigoRepetido>().Should().Contain(p => p.Contains("CodigoRepetido.B") && p.Contains("CodigoRepetido.A"));

    [Fact(DisplayName = "Prefixo diferente do módulo do enum derruba o catálogo")]
    public void Todas_PrefixoErrado_Falha() =>
        Falha<PrefixoErrado>().Should().ContainSingle(p => p.Contains("PrefixoErrado.Fora") && p.Contains("prefixo 'outro'"));

    [Fact(DisplayName = "Enum sem módulo declarado derruba o catálogo")]
    public void Todas_SemModulo_Falha() =>
        Falha<SemModulo>().Should().Contain(p => p.Contains("SemModulo") && p.Contains("ModuloDePermissoesAttribute"));

    [Fact(DisplayName = "Nome vazio, descrição vazia e nome repetido derrubam o catálogo")]
    public void Todas_TextosInvalidos_Falham()
    {
        IReadOnlyList<string> problemas = Falha<TextosInvalidos>();

        problemas.Should().Contain(p => p.Contains("TextosInvalidos.SemNome") && p.Contains("nome está vazio"));
        problemas.Should().Contain(p => p.Contains("TextosInvalidos.SemDescricao") && p.Contains("descrição está vazia"));
        problemas.Should().Contain(p => p.Contains("TextosInvalidos.NomeRepetido") && p.Contains("TextosInvalidos.Primeiro"));
    }

    [Fact(DisplayName = "Código fora do formato derruba o catálogo")]
    public void Todas_CodigoForaDoFormato_Falha() =>
        Falha<CodigoMalFormado>().Should().ContainSingle(p => p.Contains("CodigoMalFormado.Maiusculo"));

    [Fact(DisplayName = "Campo de contexto que a decisão não verifica derruba o catálogo")]
    public void Todas_ContextoDesconhecido_Falha() =>
        Falha<ContextoDesconhecido>().Should().ContainSingle(p => p.Contains("ContextoDesconhecido.Item") && p.Contains("campoInexistente"));

    [Fact(DisplayName = "Código repetido entre enums diferentes derruba a aplicação")]
    public void Aplicacao_CodigoRepetidoEntreModulos_Falha()
    {
        Action reunir = () => _ = new CatalogoDaAplicacao(
        [
            new FonteFalsa(typeof(Valido), CatalogoDePermissoes<Valido>.Todas()),
            new FonteFalsa(typeof(OutroEnumComOMesmoCodigo), CatalogoDePermissoes<OutroEnumComOMesmoCodigo>.Todas()),
        ]);

        reunir.Should().Throw<CatalogoDePermissoesInvalidoException>()
            .Which.Problemas.Should().Contain(p => p.Contains("OutroEnumComOMesmoCodigo") && p.Contains("teste:itens:manter"));
    }

    [Fact(DisplayName = "A aplicação ordena por módulo, recurso e ação e busca por código")]
    public void Aplicacao_OrdenaEBuscaPorCodigo()
    {
        CatalogoDaAplicacao catalogo = new([new FonteFalsa(typeof(Valido), CatalogoDePermissoes<Valido>.Todas())]);

        catalogo.Definicoes.Select(static d => d.Codigo).Should().Equal("teste:itens:ler", "teste:itens:manter");
        catalogo.TentarObter("teste:itens:ler", out DefinicaoDePermissao? achada).Should().BeTrue();
        achada!.Nome.Should().Be("Consultar itens");
        catalogo.TentarObter("teste:itens:inexistente", out _).Should().BeFalse();
    }

    private static IReadOnlyList<string> Falha<TPermissao>()
        where TPermissao : struct, Enum
    {
        Action carregar = () => CatalogoDePermissoes<TPermissao>.Todas();

        return carregar.Should().Throw<CatalogoDePermissoesInvalidoException>().Which.Problemas;
    }

    private sealed class FonteFalsa(Type tipo, IReadOnlyList<DefinicaoDePermissao> definicoes) : IFonteDeCatalogo
    {
        public Type TipoDoEnum => tipo;

        public IReadOnlyList<DefinicaoDePermissao> Definicoes() => definicoes;
    }

    [ModuloDePermissoes("teste")]
    private enum Valido
    {
        [Permissao("teste:itens:manter", "Manter itens", "Cria e edita itens.", Sensibilidade.Pessoal,
            BaseLegal = "LGPD art. 7º, III", EscopoContextoObrigatorio = ["processoId"])]
        Manter = 1,

        [Permissao("teste:itens:ler", "Consultar itens", "Lista itens.", Sensibilidade.Interna)]
        Consultar = 2,
    }

    [ModuloDePermissoes("teste")]
    private enum SemAtributo
    {
        Orfao = 1,
    }

    [ModuloDePermissoes("teste")]
    private enum CodigoRepetido
    {
        [Permissao("teste:itens:manter", "Primeiro", "d", Sensibilidade.Interna)]
        A = 1,

        [Permissao("teste:itens:manter", "Segundo", "d", Sensibilidade.Interna)]
        B = 2,
    }

    [ModuloDePermissoes("teste")]
    private enum PrefixoErrado
    {
        [Permissao("outro:itens:manter", "Fora", "d", Sensibilidade.Interna)]
        Fora = 1,
    }

    private enum SemModulo
    {
        [Permissao("teste:itens:manter", "Qualquer", "d", Sensibilidade.Interna)]
        Qualquer = 1,
    }

    [ModuloDePermissoes("teste")]
    private enum TextosInvalidos
    {
        [Permissao("teste:a:um", "", "d", Sensibilidade.Interna)]
        SemNome = 1,

        [Permissao("teste:a:dois", "Com nome", " ", Sensibilidade.Interna)]
        SemDescricao = 2,

        [Permissao("teste:a:tres", "Repetido", "d", Sensibilidade.Interna)]
        Primeiro = 3,

        [Permissao("teste:a:quatro", "Repetido", "d", Sensibilidade.Interna)]
        NomeRepetido = 4,
    }

    [ModuloDePermissoes("teste")]
    private enum CodigoMalFormado
    {
        [Permissao("teste:Itens:manter", "Maiúsculo", "d", Sensibilidade.Interna)]
        Maiusculo = 1,
    }

    [ModuloDePermissoes("teste")]
    private enum ContextoDesconhecido
    {
        [Permissao("teste:itens:manter", "Com contexto", "d", Sensibilidade.Interna,
            EscopoContextoObrigatorio = ["campoInexistente"])]
        Item = 1,
    }

    [ModuloDePermissoes("teste")]
    private enum OutroEnumComOMesmoCodigo
    {
        [Permissao("teste:itens:manter", "Outro nome", "d", Sensibilidade.Interna)]
        Manter = 1,
    }
}
