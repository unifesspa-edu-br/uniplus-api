namespace Unifesspa.UniPlus.Authorization.UnitTests.Catalogo;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// O que a conferência de um enum de permissões acusa, com o texto exato de cada problema e a ordem em que aparecem: os
/// membros na ordem de declaração e, em cada membro, o atributo, os textos, o código, o escopo e a natureza.
/// </summary>
public sealed class ConferenciaDoEnumDePermissoesTests
{
    [Fact(DisplayName = "Cada defeito de um enum é acusado com o texto exato, na ordem dos membros")]
    public void EnumComVariosDefeitos_AcusaCadaUmNaOrdemDosMembros()
    {
        IReadOnlyList<string> problemas = Falha<VariosDefeitos>();

        problemas.Should().Equal(
            "VariosDefeitos.Orfao: falta [PermissaoAttribute].",
            "VariosDefeitos.SemNome: o nome está vazio.",
            "VariosDefeitos.SemDescricao: a descrição está vazia.",
            "VariosDefeitos.CodigoRepetido: o código 'teste:itens:ler' já é de VariosDefeitos.SemNome.",
            "VariosDefeitos.PrefixoErrado: o código 'outro:itens:ver' tem o prefixo 'outro', mas o módulo do enum é 'teste'.",
            "VariosDefeitos.CodigoMalFormado: o código 'Teste:itens' não segue {módulo}:{resource}:{ação} em minúsculas, dígitos e hífen.",
            "VariosDefeitos.NomeRepetido: o nome ' ver ' já é de VariosDefeitos.SemDescricao.",
            "VariosDefeitos.ContextoDesconhecido: o campo de contexto 'campoInexistente' não é verificável pela decisão.",
            "VariosDefeitos.NaturezaInvalida: a natureza '99' não é dado nem arquivo.");
    }

    [Fact(DisplayName = "O enum sem módulo declarado é acusado uma vez, sem acusar o prefixo dos códigos")]
    public void EnumSemModulo_AcusaSoOModulo() =>
        Falha<SemModuloDeclarado>().Should().Equal("SemModuloDeclarado: falta [ModuloDePermissoesAttribute] com o módulo do enum.");

    [Fact(DisplayName = "O par repetido no módulo é acusado no fim, com as permissões em ordem ordinal de código")]
    public void ParRepetido_AcusaNoFimComOsCodigosEmOrdem() =>
        Falha<ParDuplicado>().Should().Equal(
            "O par (Dado, Pessoal) do módulo 'teste' aponta para mais de uma permissão: teste:dados-a:consultar, teste:dados-b:consultar.");

    [Fact(DisplayName = "O membro com problema não entra no par: só os válidos disputam o par")]
    public void MembroComProblema_NaoDisputaOPar() =>
        Falha<ParComMembroInvalido>().Should().Equal("ParComMembroInvalido.Invalido: o nome está vazio.");

    private static IReadOnlyList<string> Falha<TPermissao>()
        where TPermissao : struct, Enum
    {
        Action carregar = () => CatalogoDePermissoes<TPermissao>.Todas();

        return carregar.Should().Throw<CatalogoDePermissoesInvalidoException>().Which.Problemas;
    }

    [ModuloDePermissoes("teste")]
    private enum VariosDefeitos
    {
        Orfao = 1,

        [Permissao("teste:itens:ler", "", "d", ClassificacaoDeProtecao.Interno)]
        SemNome = 2,

        [Permissao("teste:itens:ver", "Ver", " ", ClassificacaoDeProtecao.Interno)]
        SemDescricao = 3,

        [Permissao("teste:itens:ler", "Ler de novo", "d", ClassificacaoDeProtecao.Interno)]
        CodigoRepetido = 4,

        [Permissao("outro:itens:ver", "Fora do módulo", "d", ClassificacaoDeProtecao.Interno)]
        PrefixoErrado = 5,

        [Permissao("Teste:itens", "Mal formado", "d", ClassificacaoDeProtecao.Interno)]
        CodigoMalFormado = 6,

        [Permissao("teste:itens:dois", " ver ", "d", ClassificacaoDeProtecao.Interno)]
        NomeRepetido = 7,

        [Permissao("teste:itens:tres", "Com contexto", "d", ClassificacaoDeProtecao.Interno,
            EscopoContextoObrigatorio = ["campoInexistente"])]
        ContextoDesconhecido = 8,

        [Permissao("teste:itens:quatro", "Natureza inválida", "d", ClassificacaoDeProtecao.Interno,
            Natureza = (NaturezaDoItem)99)]
        NaturezaInvalida = 9,
    }

    private enum SemModuloDeclarado
    {
        [Permissao("teste:itens:manter", "Qualquer", "d", ClassificacaoDeProtecao.Interno)]
        Qualquer = 1,
    }

    [ModuloDePermissoes("teste")]
    private enum ParDuplicado
    {
        [Permissao("teste:dados-b:consultar", "Consultar B", "d", ClassificacaoDeProtecao.Pessoal, Natureza = NaturezaDoItem.Dado)]
        B = 1,

        [Permissao("teste:dados-a:consultar", "Consultar A", "d", ClassificacaoDeProtecao.Pessoal, Natureza = NaturezaDoItem.Dado)]
        A = 2,
    }

    [ModuloDePermissoes("teste")]
    private enum ParComMembroInvalido
    {
        [Permissao("teste:dados-a:consultar", "Consultar A", "d", ClassificacaoDeProtecao.Pessoal, Natureza = NaturezaDoItem.Dado)]
        Valido = 1,

        [Permissao("teste:dados-b:consultar", "", "d", ClassificacaoDeProtecao.Pessoal, Natureza = NaturezaDoItem.Dado)]
        Invalido = 2,
    }
}
