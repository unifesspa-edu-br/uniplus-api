namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.EtapasLidasDeTeste;

/// <summary>
/// O item e o nível conservam a chave que o documento declarou, com a ordem igual à posição mais 1, mesmo quando o documento muda a
/// ordem ou o rótulo; a regra de consolidação que o catálogo não tem é recusa da regra.
/// </summary>
public sealed class LeituraDaAvaliacaoDaEtapaTests
{
    [Fact(DisplayName = "Os itens de avaliação conservam a chave declarada e a ordem é a posição mais 1, inclusive quando o documento inverte a ordem")]
    public void Itens_ConservamAChaveDeclarada()
    {
        EtapasLidas lidas = Ler(Etapa(0, "etapa_a").ComItens(Item(0, "criterio_b", "Clareza", 4m), Item(1, "criterio_a", "Coesão", 6m)));

        lidas.Etapas.Single().ItensDeAvaliacao.Select(static item => (item.Chave.Valor, item.Ordem, item.Rotulo, item.PontuacaoMaxima))
            .Should().Equal(("criterio_b", 1, "Clareza", 4m), ("criterio_a", 2, "Coesão", 6m));
    }

    [Fact(DisplayName = "A chave do item não muda quando o rótulo muda entre dois documentos")]
    public void Item_ComRotuloDiferente_ConservaAChave()
    {
        ItemDeAvaliacao antes = Ler(Etapa(0, "etapa_a").ComItens(Item(0, "criterio_a", "Coesão"))).Etapas.Single().ItensDeAvaliacao.Single();
        ItemDeAvaliacao depois = Ler(Etapa(0, "etapa_a").ComItens(Item(0, "criterio_a", "Coesão textual"))).Etapas.Single().ItensDeAvaliacao.Single();

        depois.Chave.Should().Be(antes.Chave);
        depois.Rotulo.Should().Be("Coesão textual");
    }

    [Fact(DisplayName = "Os níveis de atendimento conservam a chave declarada e a ordem é a posição mais 1")]
    public void Niveis_ConservamAChaveDeclarada()
    {
        EtapasLidas lidas = Ler(Etapa(0, "etapa_a").ComNiveis(Nivel(0, "atendeu", "Atendeu", 1m), Nivel(1, "parcial", "Atendeu em parte", 0.5m)));

        lidas.Etapas.Single().EscalaDeAtendimento!.Select(static nivel => (nivel.Chave.Valor, nivel.Ordem, nivel.Rotulo, nivel.Fracao))
            .Should().Equal(("atendeu", 1, "Atendeu", 1m), ("parcial", 2, "Atendeu em parte", 0.5m));
    }

    [Fact(DisplayName = "A etapa sem níveis fica sem escala de atendimento")]
    public void Etapa_SemNiveis_FicaSemEscala()
    {
        Ler(Etapa(0, "etapa_a")).Etapas.Single().EscalaDeAtendimento.Should().BeNull();
    }

    [Fact(DisplayName = "O item sem rótulo ou sem pontuação entra na etapa e a recusa fica na declaração da etapa, não na leitura")]
    public void Item_SemRotuloOuPontuacao_EntraEARecusaEDaDeclaracao()
    {
        EtapasLidas lidas = Ler(Etapa(0, "etapa_a").ComItens(Item(0, "criterio_a", rotulo: null, pontuacao: null)));

        lidas.Etapas.Single().ItensDeAvaliacao.Should().ContainSingle();
        PartesRecusadas(lidas).Should().BeEmpty();
        lidas.Etapas.Single().Declaracao().Recusas.Select(static recusa => recusa.Error.Code).Should().Contain(ItemDeAvaliacao.RotuloInvalido);
    }

    [Fact(DisplayName = "A regra de consolidação do catálogo entra na etapa com a referência que o catálogo dá")]
    public void Regra_DoCatalogo_EntraNaEtapa()
    {
        EtapasLidas lidas = Ler(Etapa(0, "etapa_a").ComRegraDeApuracao(new(RegraDeApuracao, "v1")));

        lidas.Etapas.Single().RegraConsolidacao!.Codigo.Should().Be(RegraDeApuracao);
        PartesRecusadas(lidas).Should().BeEmpty();
    }

    [Fact(DisplayName = "A regra de consolidação que o catálogo não tem é recusada no campo da regra, sem o texto digitado, e a etapa segue sem ela")]
    public void Regra_ForaDoCatalogo_RecusaOCampoDaRegra()
    {
        EtapasLidas lidas = Ler(Etapa(0, "etapa_a").ComRegraDeApuracao(new("REGRA-DIGITADA", "v1")));

        lidas.Etapas.Single().RegraConsolidacao.Should().BeNull();
        PartesRecusadas(lidas).Should().Equal(("/etapas/0/regraDeApuracaoDaAvaliacao", EtapaProcessoErrorCodes.RegraDeConsolidacaoInvalida));
        Mensagens(lidas).Should().NotContain(mensagem => mensagem.Contains("DIGITADA", StringComparison.Ordinal));
        Caminhos(lidas).Should().Equal("etapas[0].regraConsolidacao");
    }

    [Fact(DisplayName = "A regra do catálogo que não é de consolidação é recusada como a que o catálogo não tem")]
    public void Regra_DeOutroTipo_RecusaOCampoDaRegra()
    {
        EtapasLidas lidas = Ler(Etapa(0, "etapa_a").ComRegraDeApuracao(new(DocumentoLidoDeTeste.CodigoDoProcedimento, "v1")));

        PartesRecusadas(lidas).Should().Equal(("/etapas/0/regraDeApuracaoDaAvaliacao", EtapaProcessoErrorCodes.RegraDeConsolidacaoInvalida));
    }

    [Fact(DisplayName = "A regra de consolidação com só o código, sem a versão, é recusada")]
    public void Regra_SemVersao_RecusaOCampoDaRegra()
    {
        EtapasLidas lidas = Ler(Etapa(0, "etapa_a").ComRegraDeApuracao(new(RegraDeApuracao, null)));

        PartesRecusadas(lidas).Should().Equal(("/etapas/0/regraDeApuracaoDaAvaliacao", EtapaProcessoErrorCodes.RegraDeConsolidacaoInvalida));
    }

    [Fact(DisplayName = "A etapa sem regra de consolidação, ou com a referência vazia, fica sem regra e sem recusa")]
    public void Regra_Ausente_FicaSemRegraESemRecusa()
    {
        Ler(Etapa(0, "etapa_a")).Etapas.Single().RegraConsolidacao.Should().BeNull();
        EtapasLidas vazia = Ler(Etapa(0, "etapa_a").ComRegraDeApuracao(new(null, null)));

        vazia.Etapas.Single().RegraConsolidacao.Should().BeNull();
        PartesRecusadas(vazia).Should().BeEmpty();
    }

    [Fact(DisplayName = "O caráter, o peso, a nota mínima, a nota máxima e a forma de avaliação passam para a etapa adotada")]
    public void Avaliacao_PassaOsDadosDaNota()
    {
        EtapaNoDocumento etapa = Etapa(0, "etapa_a") with { Identificacao = Etapa(0, "etapa_a").Identificacao with { Peso = 2m, NotaMinima = 3m } };

        EtapaProcesso adotada = Ler(etapa).Etapas.Single();

        (adotada.Peso, adotada.NotaMinima, adotada.NotaMaxima, adotada.FormaDeAvaliacao).Should().Be((2m, 3m, 10m, Unifesspa.UniPlus.Selecao.Domain.Enums.FormaDeAvaliacao.Objetiva));
    }
}
