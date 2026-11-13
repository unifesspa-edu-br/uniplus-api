namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class EtapasDoProcessoTests
{
    [Fact(DisplayName = "Conjunto vazio não tem recusa: o processo sem prova não tem etapa")]
    public void ConjuntoVazio_NaoTemRecusa()
    {
        new EtapasDoProcesso([]).RecusaDaColecao(null).Should().BeNull();
    }

    [Fact(DisplayName = "Duas etapas com a mesma ordem são recusadas")]
    public void OrdemRepetida_ERecusada()
    {
        EtapasDoProcesso etapas = new([EtapasDeTeste.Etapa("Prova", 1), EtapasDeTeste.Etapa("Redação", 1)]);

        etapas.RecusaDaColecao(null)!.Code.Should().Be("ProcessoSeletivo.OrdemEtapaDuplicada");
    }

    [Fact(DisplayName = "Etapas sem ordem informada não contam como ordem repetida")]
    public void EtapasSemOrdem_NaoSaoOrdemRepetida()
    {
        EtapasDoProcesso etapas = new([EtapasDeTeste.Etapa("Prova"), EtapasDeTeste.Etapa("Redação")]);

        etapas.RecusaDaColecao(null).Should().BeNull();
    }

    [Fact(DisplayName = "Nenhuma etapa compor a nota é recusado, e uma que componha basta")]
    public void NenhumaEtapaComponeNota_ERecusada()
    {
        EtapaProcesso semNota = EtapasDeTeste.Etapa("Entrevista", 1, componeNota: false);

        new EtapasDoProcesso([semNota]).RecusaDaColecao(null)!.Code.Should().Be("ProcessoSeletivo.NenhumaEtapaComponeNota");
        new EtapasDoProcesso([semNota, EtapasDeTeste.Etapa("Prova", 2)]).RecusaDaColecao(null).Should().BeNull();
    }

    [Fact(DisplayName = "A etapa do ENEM vence a regra de nenhuma etapa compor a nota")]
    public void EtapaDoEnemForaDaMedia_VenceNenhumaEtapaComponeNota()
    {
        EtapasDoProcesso etapas = new([EtapasDeTeste.Etapa("Nota do ENEM", 1, notaDoEnem: true, componeNota: false)]);

        etapas.RecusaDaColecao(null)!.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemNaoCompoeNota");
    }

    [Fact(DisplayName = "Sem classificação gravada, a coerência com ela não é conferida e a duplicidade do ENEM é a recusa")]
    public void ClassificacaoNula_SeguePelaDuplicidadeDoEnem()
    {
        EtapasDoProcesso etapas = new([EtapasDeTeste.Etapa("ENEM 1", 1, notaDoEnem: true), EtapasDeTeste.Etapa("ENEM 2", 2, notaDoEnem: true)]);

        DomainError recusa = etapas.RecusaDaEtapaDoEnem(null)!;

        recusa.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemDuplicada");
        recusa.Message.Should().Contain("\"ENEM 1\", \"ENEM 2\"");
    }

    [Fact(DisplayName = "A etapa do ENEM sob classificação que não é por ENEM é recusada antes de qualquer outra causa")]
    public void ClassificacaoSemEnem_VenceADuplicidadeDoEnem()
    {
        EtapasDoProcesso etapas = new([EtapasDeTeste.Etapa("ENEM 1", 1, notaDoEnem: true), EtapasDeTeste.Etapa("ENEM 2", 2, notaDoEnem: true)]);

        etapas.RecusaDaEtapaDoEnem(EtapasDeTeste.ClassificacaoSemEnem())!.Code
            .Should().Be("ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem");
    }

    [Fact(DisplayName = "O fechamento do ciclo aponta o campo de produtos da etapa, na ordem das etapas")]
    public void ViolacoesDoCiclo_ApontamAEtapaNaOrdemDoConjunto()
    {
        EtapaProcesso fechada = EtapasDeTeste.Etapa("Redação", 1);
        EtapaProcesso aberta = EtapasDeTeste.Etapa("Prova", 2);
        EtapaProcesso aberta2 = EtapasDeTeste.Etapa("Entrevista", 3);
        aberta.DefinirProdutos([ProdutoDaEtapa.Criar("GABARITO", PapelProdutoFase.Preliminar)]).IsSuccess.Should().BeTrue();
        aberta2.DefinirProdutos([ProdutoDaEtapa.Criar("PARECER", PapelProdutoFase.Preliminar)]).IsSuccess.Should().BeTrue();

        List<FieldError> violacoes = new EtapasDoProcesso([fechada, aberta, aberta2]).ViolacoesDoCiclo();

        violacoes.Select(static v => v.Field).Should().Equal("etapas[1].produtos", "etapas[2].produtos");
        violacoes[0].Error.Code.Should().Be("ProcessoSeletivo.EtapaComPreliminarSemDefinitivo");
        violacoes[0].Error.Message.Should().Contain("Prova").And.Contain("GABARITO");
    }

    [Fact(DisplayName = "A ausência na etapa do ENEM é recusada, e na etapa comum ou em outra regra não é")]
    public void AusenciaNaEtapaDoEnem_SoERecusadaNaEtapaDoEnem()
    {
        EtapaProcesso doEnem = EtapasDeTeste.Etapa("Nota do ENEM", 1, notaDoEnem: true);
        EtapaProcesso comum = EtapasDeTeste.Etapa("Prova", 2);
        EtapasDoProcesso etapas = new([doEnem, comum]);

        etapas.RecusaDaAusenciaNaEtapaDoEnem(new ArgsElimAusenciaNaEtapa(doEnem.Id))!.Code
            .Should().Be("ProcessoSeletivo.AusenciaNaEtapaDoEnem");
        etapas.RecusaDaAusenciaNaEtapaDoEnem(new ArgsElimAusenciaNaEtapa(comum.Id)).Should().BeNull();
        etapas.RecusaDaAusenciaNaEtapaDoEnem(new ArgsElimNotaMinimaEtapa(doEnem.Id, 5m)).Should().BeNull();
    }
}
