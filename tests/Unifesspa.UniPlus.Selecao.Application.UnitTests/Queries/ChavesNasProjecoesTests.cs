namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ChavesNasProjecoesTests
{
    [Fact]
    public void ProjetarFase_FaseComChave_DevolveChaveGravada()
    {
        FaseCronograma fase = FaseCronograma.Reidratar(
            ChaveLocal.Criar("fase_avaliacao_2").Value!, Guid.NewGuid(),
            OrigensCanonicasDeTeste.Da(Guid.NewGuid(), "AVALIACAO", OrigemDataFase.NoEdital, true, false, false, false),
            new DecisoesDeCronogramaDaFase(1, null, null, null),
            new PublicacoesDaFase([], false, null), []);

        ProjecaoDaFaseDoCronograma.ProjetarFase(fase).Chave.Should().Be("fase_avaliacao_2");
    }

    [Fact]
    public void ProjetarEtapa_EtapaComChave_DevolveChaveGravada()
    {
        TipoEtapaSnapshot tipo = TipoEtapaSnapshot.Criar(Guid.NewGuid(), "PROVA", "Prova",
            admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;
        EtapaProcesso etapa = EtapaProcesso.Reidratar(ChaveLocal.Criar("etapa_prova_2").Value!, Guid.NewGuid(),
            new IdentificacaoDaEtapa("Prova", CaraterEtapa.Ambas, tipo, 1, null),
            new DeclaracaoDaAvaliacaoDaEtapa(null, null, null, null, null, null, null), null);

        ProjecaoDaEtapaDoProcesso.ProjetarEtapa(etapa).Chave.Should().Be("etapa_prova_2");
    }
}
