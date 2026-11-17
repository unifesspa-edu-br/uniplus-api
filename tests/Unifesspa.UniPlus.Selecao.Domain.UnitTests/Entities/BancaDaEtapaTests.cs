namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

public sealed class BancaDaEtapaTests
{
    private const string Codigo = "BANCA_EXAMINADORA";

    [Fact(DisplayName = "A banca que assume a declaração copia origem, nome e regra e preserva o Id")]
    public void AssumirDeclaracao_CopiaOrigemNomeERegra_PreservaOId()
    {
        BancaDaEtapa viva = BancaDaEtapa.Criar(Guid.CreateVersion7(), Codigo, "Antigo", BancasDeTeste.Regra(3, 2));
        Guid id = viva.Id;
        Guid origemDeclarada = Guid.CreateVersion7();
        BancaDaEtapa declarada = BancaDaEtapa.Criar(origemDeclarada, Codigo, "Novo", BancasDeTeste.Regra(5, 4));

        viva.AssumirDeclaracao(declarada);

        viva.Id.Should().Be(id);
        viva.TipoBancaOrigemId.Should().Be(origemDeclarada);
        viva.Nome.Should().Be("Novo");
        viva.RegraDeDeliberacao.Should().Be(declarada.RegraDeDeliberacao);
    }

    [Fact(DisplayName = "A banca que assume uma declaração sem regra fica sem regra")]
    public void AssumirDeclaracao_DeclaracaoSemRegra_RemoveARegra()
    {
        BancaDaEtapa viva = BancaDaEtapa.Criar(Guid.CreateVersion7(), Codigo, "Antigo", BancasDeTeste.Regra(3, 2));

        viva.AssumirDeclaracao(BancaDaEtapa.Criar(Guid.CreateVersion7(), Codigo, "Novo"));

        viva.RegraDeDeliberacao.Should().BeNull();
    }
}
