namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

public sealed class BancaDaEtapaTests
{
    private static readonly Guid Origem = Guid.CreateVersion7();

    private static BancaDaEtapa Banca(Guid? origem = null, string nome = "Banca", RegraDeDeliberacao? regra = null, Guid? substitui = null) =>
        BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.Nomeada(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.Entrevista, origem ?? Origem), nome), regra, substitui);

    private static void Substituir(BancaDaEtapa viva, BancaDaEtapa declarada) =>
        ReconciliacaoDasBancas.Reconciliar([viva], [declarada]);

    [Fact(DisplayName = "A banca que assume a declaração de outro tipo troca a cópia e a regra e preserva o Id")]
    public void AssumirDeclaracao_OutroTipo_TrocaACopiaEARegra_PreservaOId()
    {
        BancaDaEtapa viva = Banca(nome: "Antigo", regra: BancasDeTeste.Regra(3, 2));
        Guid id = viva.Id;
        Guid origemDeclarada = Guid.CreateVersion7();
        BancaDaEtapa declarada = Banca(origemDeclarada, "Novo", BancasDeTeste.Regra(5, 4), id);

        Substituir(viva, declarada);

        viva.Id.Should().Be(id);
        viva.Tipo.Should().Be(declarada.Tipo);
        viva.Tipo.OrigemId.Should().Be(origemDeclarada);
        viva.Tipo.Nome.Should().Be("Novo");
        viva.RegraDeDeliberacao.Should().Be(declarada.RegraDeDeliberacao);
    }

    [Fact(DisplayName = "A banca que assume a declaração do mesmo tipo mantém a cópia que já tinha")]
    public void AssumirDeclaracao_MesmoTipo_MantemACopia()
    {
        BancaDaEtapa viva = Banca(nome: "Nome de quando a banca nasceu");
        TipoDeBancaCopiado copiaDaViva = viva.Tipo;

        Substituir(viva, Banca(nome: "Nome editado depois no cadastro", substitui: viva.Id));

        viva.Tipo.Should().BeSameAs(copiaDaViva);
        viva.Tipo.Nome.Should().Be("Nome de quando a banca nasceu");
    }

    [Fact(DisplayName = "A banca que assume uma declaração sem regra fica sem regra")]
    public void AssumirDeclaracao_DeclaracaoSemRegra_RemoveARegra()
    {
        BancaDaEtapa viva = Banca(regra: BancasDeTeste.Regra(3, 2));

        Substituir(viva, Banca(substitui: viva.Id));

        viva.RegraDeDeliberacao.Should().BeNull();
    }

    [Fact(DisplayName = "A banca reconstruída dos dados salvos guarda o Id congelado e a cópia do tipo")]
    public void ReconstruirDosDadosSalvos_GuardaOIdCongeladoEACopia()
    {
        Guid id = Guid.CreateVersion7();
        TipoDeBancaCopiado tipo = TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.Entrevista);

        BancaDaEtapa banca = BancaDaEtapa.ReconstruirDosDadosSalvos(ChaveLocal.Criar("banca").Value, id, tipo);

        banca.Id.Should().Be(id);
        banca.Tipo.Should().Be(tipo);
    }

    [Fact(DisplayName = "A banca reconstruída sem o Id congelado é erro de programação")]
    public void ReconstruirDosDadosSalvos_IdVazio_Lanca()
    {
        Action reconstruir = () => BancaDaEtapa.ReconstruirDosDadosSalvos(ChaveLocal.Criar("banca").Value, Guid.Empty, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.Entrevista));

        reconstruir.Should().Throw<ArgumentException>();
    }
}
