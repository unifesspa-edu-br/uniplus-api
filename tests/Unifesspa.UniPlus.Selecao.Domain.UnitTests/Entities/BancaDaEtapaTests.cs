namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

public sealed class BancaDaEtapaTests
{
    private const string Codigo = "BANCA_EXAMINADORA";

    private static void Substituir(BancaDaEtapa viva, BancaDaEtapa declarada) =>
        ReconciliacaoDasBancas.Reconciliar([viva], [declarada]);

    [Fact(DisplayName = "A banca que assume a declaração copia origem, nome e regra e preserva o Id")]
    public void AssumirDeclaracao_CopiaOrigemNomeERegra_PreservaOId()
    {
        BancaDaEtapa viva = BancaDaEtapa.Criar(Guid.CreateVersion7(), Codigo, "Antigo", BancasDeTeste.Regra(3, 2));
        Guid id = viva.Id;
        Guid origemDeclarada = Guid.CreateVersion7();
        BancaDaEtapa declarada = BancaDaEtapa.Criar(origemDeclarada, Codigo, "Novo", BancasDeTeste.Regra(5, 4), idDaBancaQueSubstitui: id);

        Substituir(viva, declarada);

        viva.Id.Should().Be(id);
        viva.TipoBancaOrigemId.Should().Be(origemDeclarada);
        viva.Nome.Should().Be("Novo");
        viva.RegraDeDeliberacao.Should().Be(declarada.RegraDeDeliberacao);
    }

    [Fact(DisplayName = "A banca que assume uma declaração sem regra fica sem regra")]
    public void AssumirDeclaracao_DeclaracaoSemRegra_RemoveARegra()
    {
        BancaDaEtapa viva = BancaDaEtapa.Criar(Guid.CreateVersion7(), Codigo, "Antigo", BancasDeTeste.Regra(3, 2));

        Substituir(viva, BancaDaEtapa.Criar(Guid.CreateVersion7(), Codigo, "Novo", idDaBancaQueSubstitui: viva.Id));

        viva.RegraDeDeliberacao.Should().BeNull();
    }

    [Fact(DisplayName = "A banca reconstruída dos dados salvos guarda o Id congelado")]
    public void ReconstruirDosDadosSalvos_GuardaOIdCongelado()
    {
        Guid id = Guid.CreateVersion7();

        BancaDaEtapa banca = BancaDaEtapa.ReconstruirDosDadosSalvos(id, Guid.CreateVersion7(), Codigo, "Banca");

        banca.Id.Should().Be(id);
    }

    [Fact(DisplayName = "A banca reconstruída sem o Id congelado é erro de programação")]
    public void ReconstruirDosDadosSalvos_IdVazio_Lanca()
    {
        Action reconstruir = () => BancaDaEtapa.ReconstruirDosDadosSalvos(Guid.Empty, Guid.CreateVersion7(), Codigo, "Banca");

        reconstruir.Should().Throw<ArgumentException>();
    }
}
