namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Permissoes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// Cada par (natureza, classificação) tem uma permissão de consulta na Seleção, dado e arquivo são sempre
/// permissões à parte, e o acesso ao resource não libera item algum (ADR-0153).
/// </summary>
public sealed class PortaDeProjecaoDaSelecaoTests
{
    [Theory(DisplayName = "A porta responde a permissão de cada par do catálogo da Seleção")]
    [InlineData(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Publico, "selecao:dados-publicos:consultar")]
    [InlineData(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Interno, "selecao:dados-internos:consultar")]
    [InlineData(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Pessoal, "selecao:dados-pessoais:consultar")]
    [InlineData(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Identificador, "selecao:dados-identificadores:revelar")]
    [InlineData(NaturezaDoItem.Dado, ClassificacaoDeProtecao.Sensivel, "selecao:dados-sensiveis:consultar")]
    [InlineData(NaturezaDoItem.Arquivo, ClassificacaoDeProtecao.Publico, "selecao:documentos-publicos:consultar")]
    [InlineData(NaturezaDoItem.Arquivo, ClassificacaoDeProtecao.Interno, "selecao:documentos-internos:consultar")]
    [InlineData(NaturezaDoItem.Arquivo, ClassificacaoDeProtecao.Pessoal, "selecao:documentos-pessoais:consultar")]
    [InlineData(NaturezaDoItem.Arquivo, ClassificacaoDeProtecao.Identificador, "selecao:documentos-identificadores:consultar")]
    [InlineData(NaturezaDoItem.Arquivo, ClassificacaoDeProtecao.Sensivel, "selecao:documentos-sensiveis:consultar")]
    public void Requisito_ParDoCatalogo_ResponderAPermissao(NaturezaDoItem natureza, ClassificacaoDeProtecao classificacao, string codigo) =>
        PortaDeProjecaoDaSelecao.Instancia.Requisito(natureza, classificacao)!.Permissao.Should().Be(codigo);

    [Fact(DisplayName = "O acesso ao resource é interno, não libera item e a revelação de CPF não existe mais como permissão própria")]
    public void Catalogo_AcessoAoResourceNaoLiberaItem()
    {
        IReadOnlyList<DefinicaoDePermissao> definicoes = CatalogoDePermissoes<PermissaoSelecao>.Todas();

        DefinicaoDePermissao acesso = definicoes.Single(static d => d.Codigo == "selecao:inscricoes:consultar");
        acesso.Natureza.Should().Be(NaturezaDoItem.Nenhuma);
        acesso.Requisito.Sensibilidade.Should().Be(ClassificacaoDeProtecao.Interno);
        definicoes.Should().NotContain(static d => d.Codigo == "selecao:inscricoes:revelar-cpf");
    }

    [Fact(DisplayName = "As ações de importar notas e gerar o envio ao INEP não participam da projeção")]
    public void Catalogo_AcoesNaoTemNatureza() =>
        CatalogoDePermissoes<PermissaoSelecao>.Todas()
            .Where(static d => d.Acao is "importar" or "gerar")
            .Should().OnlyContain(static d => d.Natureza == NaturezaDoItem.Nenhuma);
}
