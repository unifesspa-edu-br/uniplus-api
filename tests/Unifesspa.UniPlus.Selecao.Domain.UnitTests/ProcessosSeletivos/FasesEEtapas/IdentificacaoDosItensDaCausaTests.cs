namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioFaseCronograma;

/// <summary>
/// A causa que aponta fase, etapa, recurso ou banca pela posição canônica leva também o identificador estável do item, para o cliente
/// achar o item certo depois de outra gravação mudar as posições; o item que não existe não aponta outro.
/// </summary>
public sealed class IdentificacaoDosItensDaCausaTests
{
    private const TipoDeBanca AnaliseDocumentalDoTipo = TipoDeBanca.AnaliseDocumental;
    private const TipoDeBanca HeteroidentificacaoDoTipo = TipoDeBanca.Heteroidentificacao;

    private static readonly DomainError Erro = new("Teste.Recusa", "Mensagem.");

    private static (ConfiguracaoDeclarada Configuracao, EtapaProcesso Etapa) ProcessoComDoisRecursos()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        EtapaProcesso etapa = processo.Etapas.Single();
        etapa.DefinirJanelaEParecer(null, null, emiteParecerIndividual: true).IsSuccess.Should().BeTrue();
        etapa.DefinirProdutos([ProdutoDaEtapa.Criar("GABARITO", PapelProdutoFase.Preliminar)]).IsSuccess.Should().BeTrue();
        etapa.DefinirRecursos([RecursoModelo.NaCiencia(), RecursoModelo.AoAto(etapa.Produtos.Single().Id)]).IsSuccess.Should().BeTrue();

        return (processo.Declaracao(), etapa);
    }

    // As bancas são declaradas fora da ordem canônica (o código do tipo decide: a de análise documental vem antes da de heteroidentificação)
    // e há duas do mesmo tipo, distinguidas pelos candidatos atendidos; na etapa, as mesmas três, na mesma ordem de chegada.
    private static (ConfiguracaoDeclarada Configuracao, FaseCronograma Fase, EtapaProcesso Etapa) ProcessoComBancasForaDaOrdemCanonica()
    {
        FaseCronograma fase = ProcessoConformeFactory.FaseConforme(bancas:
            [Banca(HeteroidentificacaoDoTipo), Banca(AnaliseDocumentalDoTipo, "RENDA"), Banca(AnaliseDocumentalDoTipo, "PCD")]);
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: fase);
        EtapaProcesso etapa = processo.Etapas.Single();
        etapa.DefinirBancas(
        [
            BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.De(HeteroidentificacaoDoTipo)),
            BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.De(AnaliseDocumentalDoTipo)),
            BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.De(AnaliseDocumentalDoTipo)),
        ]);

        return (processo.Declaracao(), fase, etapa);
    }

    private static string[] IdentificadoresDe(IEnumerable<BancaRequerida> bancas) => [.. bancas.Select(static banca => banca.Id.ToString("D"))];

    private static FieldError[] Identificar(ConfiguracaoDeclarada configuracao, params string?[] campos) =>
        [.. IdentificacaoDosItensDaCausa.Identificar([.. campos.Select(static campo => new FieldError(campo, Erro))], configuracao)];

    [Fact(DisplayName = "A causa em uma fase leva o código do tipo de fase dela")]
    public void Identificar_CausaNaFase_LevaOCodigoDaFase()
    {
        (ConfiguracaoDeclarada configuracao, _) = ProcessoComDoisRecursos();

        FieldError causa = Identificar(configuracao, "fases[0].fim").Single();

        causa.Identificadores.Should().Equal(new IdentificadorDoItem("fases", configuracao.Cronograma.CronogramaFases[0].Codigo));
        causa.Field.Should().Be("fases[0].fim", "o campo não muda: quem o converte em ponteiro é outra etapa");
    }

    [Fact(DisplayName = "A causa em uma etapa leva o identificador da etapa")]
    public void Identificar_CausaNaEtapa_LevaOIdentificadorDaEtapa()
    {
        (ConfiguracaoDeclarada configuracao, EtapaProcesso etapa) = ProcessoComDoisRecursos();

        Identificar(configuracao, "etapas[0].produtos[0].papel").Single().Identificadores.Should().Equal(
            new IdentificadorDoItem("etapas", etapa.Id.ToString("D")));
    }

    [Fact(DisplayName = "A causa em um recurso da etapa leva o identificador da etapa e o do recurso, na ordem do caminho e na ordem canônica dos recursos")]
    public void Identificar_CausaNoRecurso_LevaEtapaERecursoNaOrdemCanonica()
    {
        (ConfiguracaoDeclarada configuracao, EtapaProcesso etapa) = ProcessoComDoisRecursos();
        RecursoDaEtapa segundoNaOrdemCanonica = etapa.Recursos.ElementAt(1);

        FieldError causa = Identificar(configuracao, "etapas[0].recursos[1].regraCodigo").Single();

        causa.Identificadores.Should().Equal(
            new IdentificadorDoItem("etapas", etapa.Id.ToString("D")),
            new IdentificadorDoItem("recursos", segundoNaOrdemCanonica.Id.ToString("D")));
        etapa.Recursos.ElementAt(0).Id.Should().NotBe(segundoNaOrdemCanonica.Id);
    }

    [Fact(DisplayName = "A causa em uma banca da fase leva o código da fase e o identificador da banca na posição canônica, e não na ordem em que as bancas chegaram")]
    public void Identificar_CausaNaBancaDaFase_LevaFaseEBancaNaOrdemCanonica()
    {
        (ConfiguracaoDeclarada configuracao, FaseCronograma fase, _) = ProcessoComBancasForaDaOrdemCanonica();
        string[] naOrdemCanonica = IdentificadoresDe(fase.BancasRequeridas);

        FieldError[] causas = Identificar(
            configuracao, "fases[0].bancasRequeridas[0]", "fases[0].bancasRequeridas[1].regraDeDeliberacao.quorum", "fases[0].bancasRequeridas[2]");

        fase.BancasRequeridas.Select(static banca => (banca.Tipo.Codigo, banca.PosicaoNoTipo))
            .Should().Equal([(AnaliseDocumentalDoTipo, 1), (AnaliseDocumentalDoTipo, 2), (HeteroidentificacaoDoTipo, 1)], "a banca de heteroidentificação chegou primeiro e é a última na ordem canônica");
        causas.Select(static causa => causa.Identificadores!.Select(static item => item.Lista).ToArray())
            .Should().AllSatisfy(listas => listas.Should().Equal("fases", "bancasRequeridas"));
        causas.Select(static causa => causa.Identificadores![1].Identificador).Should().Equal(naOrdemCanonica);
        causas[0].Identificadores![0].Identificador.Should().Be(configuracao.Cronograma.CronogramaFases[0].Codigo);
    }

    [Fact(DisplayName = "Duas bancas do mesmo tipo na fase têm identificadores distintos")]
    public void Identificar_DuasBancasDoMesmoTipo_TemIdentificadoresDistintos()
    {
        (ConfiguracaoDeclarada configuracao, FaseCronograma fase, _) = ProcessoComBancasForaDaOrdemCanonica();

        FieldError[] causas = Identificar(configuracao, "fases[0].bancasRequeridas[0]", "fases[0].bancasRequeridas[1]");

        fase.BancasRequeridas.Take(2).Select(static banca => banca.Tipo.Codigo).Should().OnlyContain(codigo => codigo == AnaliseDocumentalDoTipo);
        causas.Select(static causa => causa.Identificadores![1].Identificador).Distinct().Should().HaveCount(2);
    }

    [Fact(DisplayName = "A causa em uma banca da etapa leva o identificador da etapa e o da banca na posição canônica, e não na ordem em que as bancas chegaram")]
    public void Identificar_CausaNaBancaDaEtapa_LevaEtapaEBancaNaOrdemCanonica()
    {
        (ConfiguracaoDeclarada configuracao, _, EtapaProcesso etapa) = ProcessoComBancasForaDaOrdemCanonica();
        BancaDaEtapa[] naOrdemCanonica = [.. etapa.Bancas];

        FieldError[] causas = Identificar(configuracao, "etapas[0].bancas[0].regraDeDeliberacao", "etapas[0].bancas[1]", "etapas[0].bancas[2]");

        naOrdemCanonica.Select(static banca => banca.Tipo.Codigo).Should().Equal([AnaliseDocumentalDoTipo, AnaliseDocumentalDoTipo, HeteroidentificacaoDoTipo]);
        causas.SelectMany(static causa => causa.Identificadores!).Should().Equal(naOrdemCanonica.SelectMany(banca =>
            new[] { new IdentificadorDoItem("etapas", etapa.Id.ToString("D")), new IdentificadorDoItem("bancas", banca.Id.ToString("D")) }));
        naOrdemCanonica.Select(static banca => banca.Id).Distinct().Should().HaveCount(3, "as duas bancas do mesmo tipo têm identificadores distintos");
    }

    [Fact(DisplayName = "A causa que se refere à fase ou à etapa inteira, sem apontar uma banca, leva só a fase ou a etapa")]
    public void Identificar_CausaNaListaDasBancasSemPosicao_LevaSoAFaseOuAEtapa()
    {
        (ConfiguracaoDeclarada configuracao, _, EtapaProcesso etapa) = ProcessoComBancasForaDaOrdemCanonica();

        FieldError[] causas = Identificar(configuracao, "fases[0].bancasRequeridas", "etapas[0].bancas", "fases[0].regraRecurso.admiteInstanciaSuperior");

        causas[0].Identificadores.Should().Equal(new IdentificadorDoItem("fases", configuracao.Cronograma.CronogramaFases[0].Codigo));
        causas[1].Identificadores.Should().Equal(new IdentificadorDoItem("etapas", etapa.Id.ToString("D")));
        causas[2].Identificadores.Should().Equal(new IdentificadorDoItem("fases", configuracao.Cronograma.CronogramaFases[0].Codigo));
    }

    [Fact(DisplayName = "A categoria dos candidatos atendidos da banca não ganha identificador próprio: a causa leva a fase e a banca")]
    public void Identificar_CausaNaCategoriaDaBanca_LevaSoAFaseEABanca()
    {
        (ConfiguracaoDeclarada configuracao, FaseCronograma fase, _) = ProcessoComBancasForaDaOrdemCanonica();

        FieldError causa = Identificar(configuracao, "fases[0].bancasRequeridas[1].categoriasDocumentoIds[0]").Single();

        causa.Identificadores.Should().Equal(
            new IdentificadorDoItem("fases", configuracao.Cronograma.CronogramaFases[0].Codigo),
            new IdentificadorDoItem("bancasRequeridas", fase.BancasRequeridas.ElementAt(1).Id.ToString("D")));
    }

    [Fact(DisplayName = "A banca que não existe na leitura não aponta outra: a causa fica só com a fase ou com a etapa")]
    public void Identificar_BancaInexistente_NaoApontaOutraBanca()
    {
        (ConfiguracaoDeclarada configuracao, _, EtapaProcesso etapa) = ProcessoComBancasForaDaOrdemCanonica();

        FieldError[] causas = Identificar(configuracao, "fases[0].bancasRequeridas[3]", "etapas[0].bancas[3]");

        causas[0].Identificadores.Should().Equal(new IdentificadorDoItem("fases", configuracao.Cronograma.CronogramaFases[0].Codigo));
        causas[1].Identificadores.Should().Equal(new IdentificadorDoItem("etapas", etapa.Id.ToString("D")));
    }

    [Theory(DisplayName = "A lista de bancas de outro lugar, a fase ou a etapa que não existe e o campo sem índice de fase não levam identificador de banca")]
    [InlineData("fases[0].bancas[0]", 1)]
    [InlineData("etapas[0].bancasRequeridas[0]", 1)]
    [InlineData("fases[5].bancasRequeridas[0]", 0)]
    [InlineData("etapas[5].bancas[0]", 0)]
    [InlineData("fases", 0)]
    public void Identificar_BancaForaDoLugarOuDoIndice_NaoLevaIdentificadorDeBanca(string campo, int itensEsperados)
    {
        (ConfiguracaoDeclarada configuracao, _, _) = ProcessoComBancasForaDaOrdemCanonica();

        FieldError causa = Identificar(configuracao, campo).Single();

        string[] listas = [.. (causa.Identificadores ?? []).Select(static item => item.Lista)];

        listas.Should().HaveCount(itensEsperados).And.NotContain("bancasRequeridas").And.NotContain("bancas");
    }

    [Fact(DisplayName = "O índice de recurso depois da última posição deixa só a etapa, e o de fase ou de etapa inexistente não leva identificador")]
    public void Identificar_ItemInexistente_NaoApontaOutroItem()
    {
        (ConfiguracaoDeclarada configuracao, EtapaProcesso etapa) = ProcessoComDoisRecursos();

        FieldError[] causas = Identificar(configuracao, "etapas[0].recursos[2].regraCodigo", "fases[3].fim", "etapas[5].nome");

        causas[0].Identificadores.Should().Equal(new IdentificadorDoItem("etapas", etapa.Id.ToString("D")));
        causas[1].Identificadores.Should().BeNull();
        causas[2].Identificadores.Should().BeNull();
    }

    [Theory(DisplayName = "A causa sem campo, de outro bloco, que já é ponteiro ou com índice que não é posição fica como está")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("identificacao.nome")]
    [InlineData("fases")]
    [InlineData("fases[x].fim")]
    [InlineData("fases[].fim")]
    [InlineData("fases[0")]
    [InlineData("/cronograma/fases/0/fim")]
    public void Identificar_CausaQueNaoApontaItemDeLista_FicaComoEstava(string? campo)
    {
        (ConfiguracaoDeclarada configuracao, _) = ProcessoComDoisRecursos();

        FieldError causa = Identificar(configuracao, campo).Single();

        causa.Should().Be(new FieldError(campo, Erro));
    }

    [Fact(DisplayName = "Cada causa recebe o identificador do próprio item, na mesma ordem, e as demais informações ficam como estavam")]
    public void Identificar_VariasCausas_PreservaAOrdemEOsDemaisMembros()
    {
        (ConfiguracaoDeclarada configuracao, EtapaProcesso etapa) = ProcessoComDoisRecursos();
        FieldError[] entrada = [new("etapas[0].nome", Erro, "origem-a"), new("fases[0].fim", Erro, "origem-b"), new(null, Erro)];

        IReadOnlyList<FieldError> saida = IdentificacaoDosItensDaCausa.Identificar(entrada, configuracao);

        saida.Select(static c => (c.Field, c.Origem)).Should().Equal(("etapas[0].nome", "origem-a"), ("fases[0].fim", "origem-b"), (null, null));
        saida[0].Identificadores!.Single().Identificador.Should().Be(etapa.Id.ToString("D"));
        saida[2].Identificadores.Should().BeNull();
    }

    [Fact(DisplayName = "As causas e a configuração são obrigatórias")]
    public void Identificar_ArgumentoNulo_Lanca()
    {
        (ConfiguracaoDeclarada configuracao, _) = ProcessoComDoisRecursos();

        FluentActions.Invoking(() => IdentificacaoDosItensDaCausa.Identificar(null!, configuracao)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => IdentificacaoDosItensDaCausa.Identificar([], null!)).Should().Throw<ArgumentNullException>();
    }
}
