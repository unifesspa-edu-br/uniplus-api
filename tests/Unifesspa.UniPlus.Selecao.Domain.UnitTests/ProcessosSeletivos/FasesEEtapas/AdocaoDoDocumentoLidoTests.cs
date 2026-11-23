namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.OrdemCanonica;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;

/// <summary>
/// A fase e a etapa que um documento lido entrega são adotadas como o autor as declarou, sem conferência: a recusa da declaração
/// fica para o congelamento, que a lê pela declaração. A origem do documento liga a posição canônica de cada item à posição em
/// que ele veio e guarda as partes que a leitura recusou.
/// </summary>
public sealed class AdocaoDoDocumentoLidoTests
{
    [Fact(DisplayName = "A fase de janela invertida, que a criação recusa, é adotada sem conferência e a recusa fica na declaração")]
    public void Fase_Adotada_NaoConfereADeclaracao()
    {
        ModeloDeFase invertida = new ModeloDeFase("INSCRICAO", 1).Janela(9, 1);

        FaseCronograma.Criar(invertida.Origem(), invertida.Decisoes(), invertida.Publicacoes(), invertida.Bancas).IsFailure.Should().BeTrue();
        FaseCronograma adotada = invertida.Adotada();

        adotada.Declaracao().Recusas.Select(static recusa => recusa.Error.Code).Should().Contain("FaseCronograma.JanelaInvertida");
    }

    [Fact(DisplayName = "A fase adotada leva os dados, os produtos e as bancas da declaração")]
    public void Fase_Adotada_LevaOQueODocumentoDeclara()
    {
        FaseCronograma fase = (new ModeloDeFase("RESULTADO", 2) { Bancas = [BancasDeTeste.DeRecursosDaFase()] }
            .Publicando(ModeloDeFase.Preliminar("RESULTADO_PROVA")).Janela(1, 5)).Adotada();

        (fase.Codigo, fase.Ordem, fase.Produtos.Count, fase.BancasRequeridas.Count).Should().Be(("RESULTADO", 2, 1, 1));
        fase.Declaracao().Recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase adotada nasce com identidade nova, distinta da origem canônica e de outra fase adotada")]
    public void Fase_Adotada_TemIdentidadeNova()
    {
        ModeloDeFase modelo = new ModeloDeFase("INSCRICAO", 1).Janela(1, 5);

        FaseCronograma primeira = modelo.Adotada();
        FaseCronograma segunda = modelo.Adotada();

        primeira.Id.Should().NotBe(Guid.Empty).And.NotBe(segunda.Id).And.NotBe(modelo.OrigemId);
    }

    [Fact(DisplayName = "A etapa de nome vazio, que a criação recusa, é adotada sem conferência e a recusa fica na declaração")]
    public void Etapa_Adotada_NaoConfereADeclaracao()
    {
        ModeloDeEtapa semNome = new(string.Empty);

        EtapaProcesso.Criar(semNome.Identificacao(), semNome.Avaliacao(), semNome.Itens).IsFailure.Should().BeTrue();
        EtapaProcesso adotada = semNome.Adotada();

        adotada.Declaracao().Recusas.Select(static recusa => recusa.Error.Code).Should().Contain("EtapaProcesso.NomeObrigatorio");
    }

    [Fact(DisplayName = "A etapa adotada leva a avaliação, os itens, a janela, os produtos e as bancas da declaração, com identidade nova")]
    public void Etapa_Adotada_LevaOQueODocumentoDeclara()
    {
        ModeloDeEtapa modelo = new ModeloDeEtapa("Redação")
        {
            Ordem = 1,
            Itens = [new ItemDeAvaliacaoDeclarado(null, "Coesão", 10m)],
            Produtos = [ModeloDeEtapa.Preliminar("RESULTADO_PROVA")],
        }.Humana();

        EtapaProcesso etapa = modelo.Adotada();
        EtapaDeclarada declarada = etapa.Declaracao();

        (declarada.Nome, declarada.Ordem, declarada.Peso).Should().Be(("Redação", 1, 1m));
        declarada.PontuacoesDosItens.Should().Equal(10m);
        (declarada.Atos.Produtos.Count, declarada.Bancas.Declaradas.Count).Should().Be((1, 1));
        etapa.Id.Should().NotBe(Guid.Empty).And.NotBe(modelo.Id);
    }

    [Fact(DisplayName = "A origem da lista tem, em cada posição canônica, a posição em que o item veio no documento")]
    public void Origem_DaLista_TemAPosicaoDeOrigemDeCadaPosicaoCanonica()
    {
        FaseCronograma terceira = new ModeloDeFase("C", 3).Adotada();
        FaseCronograma primeira = new ModeloDeFase("A", 1).Adotada();
        FaseCronograma segunda = new ModeloDeFase("B", 2).Adotada();

        OrigemNoDocumento origem = OrigemNoDocumento.DaLista("fases", [(terceira, 0), (primeira, 1), (segunda, 2)], OrdemCanonicaDaConfiguracao.Fases, []);

        Enumerable.Range(0, 3).Select(posicao => origem.PosicaoDeOrigem("fases", posicao)).Should().Equal(1, 2, 0);
    }

    [Fact(DisplayName = "A parte recusada deixa a posição dela no documento sem item, e a origem dos que ficaram pula essa posição")]
    public void Origem_DaLista_ComParteRecusada_PulaAPosicaoDaParte()
    {
        FaseCronograma segunda = new ModeloDeFase("B", 2).Adotada();
        FaseCronograma primeira = new ModeloDeFase("A", 1).Adotada();
        ParteRecusada recusada = Recusada("fases", 1);

        OrigemNoDocumento origem = OrigemNoDocumento.DaLista("fases", [(segunda, 0), (primeira, 2)], OrdemCanonicaDaConfiguracao.Fases, [recusada]);

        Enumerable.Range(0, 2).Select(posicao => origem.PosicaoDeOrigem("fases", posicao)).Should().Equal(2, 0);
        origem.PartesRecusadas.Should().Equal(recusada);
    }

    [Fact(DisplayName = "A origem de um item com parte recusada dá o caminho de cada parte relativo ao item, seja lista ou campo")]
    public void Origem_PartesRecusadasDe_DaOsCaminhosDasPartesDoItem()
    {
        OrigemNoDocumento origem = ComPartesRecusadas("fases[0].produtos", "fases[0].bancasRequeridas", "fases[0].unidadeResponsavel", "fases[1].produtos", "etapas[0].bancas");

        origem.PartesRecusadasDe("fases[0]").Should().BeEquivalentTo(FaseDeclarada.ListaDosProdutos, FaseDeclarada.ListaDasBancasRequeridas, FaseDeclarada.CampoDaUnidadeResponsavel);
        origem.PartesRecusadasDe("fases[1]").Should().BeEquivalentTo(FaseDeclarada.ListaDosProdutos);
        origem.PartesRecusadasDe("fases[2]").Should().BeEmpty();
        origem.TemParteRecusadaEm("etapas[0].bancas").Should().BeTrue();
        origem.TemParteRecusadaEm("etapas[0]").Should().BeFalse();
    }

    [Fact(DisplayName = "A origem do documento reúne as listas e as partes recusadas de cada bloco, na ordem dos blocos")]
    public void Origem_Reunir_JuntaAsListasEAsPartesDosBlocos()
    {
        EtapaProcesso etapa = new ModeloDeEtapa("Prova") { Ordem = 1 }.Adotada();
        FaseCronograma fase = new ModeloDeFase("A", 1).Adotada();
        ParteRecusada daFase = Recusada("fases[0].produtos", 4);
        ParteRecusada daEtapa = Recusada("etapas[0].bancas", 2);

        OrigemNoDocumento origem = OrigemNoDocumento.Reunir(
        [
            OrigemNoDocumento.DaLista("fases", [(fase, 3)], OrdemCanonicaDaConfiguracao.Fases, [daFase]),
            OrigemNoDocumento.DaLista("etapas", [(etapa, 6)], OrdemCanonicaDaConfiguracao.Etapas, [daEtapa]),
        ]);

        (origem.PosicaoDeOrigem("fases", 0), origem.PosicaoDeOrigem("etapas", 0)).Should().Be((3, 6));
        origem.PartesRecusadas.Should().Equal(daFase, daEtapa);
    }

    [Fact(DisplayName = "O ponteiro do campo de uma fase sai na posição de origem que a leitura montou")]
    public void Ponteiro_ComAOrigemMontada_ApontaAPosicaoDeOrigem()
    {
        FaseCronograma primeira = new ModeloDeFase("A", 1).Adotada();
        FaseCronograma segunda = new ModeloDeFase("B", 2).Adotada();
        OrigemNoDocumento origem = OrigemNoDocumento.DaLista("fases", [(segunda, 0), (primeira, 1)], OrdemCanonicaDaConfiguracao.Fases, []);

        PonteiroNoDocumento.Converter("fases[0].produtos", origem).Should().Be("/cronograma/fases/1/publicacoesPrevistas");
        PonteiroNoDocumento.Converter("fases[1].produtos", origem).Should().Be("/cronograma/fases/0/publicacoesPrevistas");
    }
}
