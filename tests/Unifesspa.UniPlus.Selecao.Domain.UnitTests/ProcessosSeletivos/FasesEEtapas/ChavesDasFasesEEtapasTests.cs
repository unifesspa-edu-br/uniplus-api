namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ChavesDasFasesEEtapasTests
{
    [Fact]
    public void Reidratar_Fase_PreservaChaveRecebida()
    {
        ModeloDeFase modelo = new("INSCRICAO", 1);
        ChaveLocal chave = ChaveLocal.Criar("fase_inscricao_2").Value;
        FaseCronograma fase = FaseCronograma.Reidratar(chave, Guid.CreateVersion7(), modelo.Origem(), modelo.Decisoes(), modelo.Publicacoes(), modelo.Bancas);
        fase.Chave.Should().Be(chave);
    }

    [Fact]
    public void Reidratar_Etapa_PreservaChaveRecebida()
    {
        ModeloDeEtapa modelo = new("Prova");
        ChaveLocal chave = ChaveLocal.Criar("etapa_prova_2").Value;
        EtapaProcesso etapa = EtapaProcesso.Reidratar(chave, Guid.CreateVersion7(), modelo.Identificacao(), modelo.Avaliacao(), []);
        etapa.Chave.Should().Be(chave);
    }

    [Fact]
    public void Adotar_Fase_GuardaChaveRecebida()
    {
        ModeloDeFase modelo = new("INSCRICAO", 1);
        ChaveLocal chave = ChaveLocal.Criar("fase_declarada").Value;
        FaseCronograma fase = FaseCronograma.Adotar(chave, modelo.Origem(), modelo.Decisoes(), modelo.Publicacoes(), modelo.Bancas);
        fase.Chave.Should().Be(chave);
    }

    [Fact]
    public void Adotar_Etapa_GuardaChaveRecebida()
    {
        ModeloDeEtapa modelo = new("Prova");
        ChaveLocal chave = ChaveLocal.Criar("etapa_declarada").Value;
        EtapaProcesso etapa = EtapaProcesso.Adotar(chave, modelo.Identificacao(),
            new AvaliacaoDeclaradaDaEtapa(modelo.Avaliacao(), [], null),
            new JanelaDeTempo(null, null), new PublicacoesDaEtapa([], false, [], null), []);
        etapa.Chave.Should().Be(chave);
    }

    [Fact]
    public void AtualizarDados_AoRenomearEtapa_PreservaChave()
    {
        ModeloDeEtapa modelo = new("Prova");
        ChaveLocal chave = ChaveLocal.Criar("etapa_prova").Value;
        EtapaProcesso etapa = EtapaProcesso.Criar(chave, modelo.Identificacao(), modelo.Avaliacao()).Value!;
        etapa.AtualizarDados((modelo with { Nome = "Redação" }).Identificacao(), modelo.Avaliacao()).IsSuccess.Should().BeTrue();
        etapa.Chave.Should().Be(chave);
    }
}
