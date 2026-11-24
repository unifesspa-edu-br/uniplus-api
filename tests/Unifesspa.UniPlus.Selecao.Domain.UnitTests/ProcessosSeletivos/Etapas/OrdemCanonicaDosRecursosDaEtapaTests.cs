namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.OrdemCanonica;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os recursos da etapa não guardam a ordem em que foram enviados: a declaração, o registro publicado e a leitura os trazem na
/// ordem canônica, de modo que a posição que uma recusa aponta é a mesma em qualquer lugar.
/// </summary>
public sealed class OrdemCanonicaDosRecursosDaEtapaTests
{
    private static EtapaProcesso EtapaComDoisPreliminares(out ProdutoDaEtapa gabarito, out ProdutoDaEtapa resultado)
    {
        TipoEtapaSnapshot tipo = TipoEtapaSnapshot.Criar(
            Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;
        EtapaProcesso etapa = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa("Prova Objetiva", CaraterEtapa.Ambas, tipo, Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(
                Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null,
                RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        gabarito = ProdutoDaEtapa.Criar("GABARITO", PapelDaPublicacaoPrevista.Preliminar);
        resultado = ProdutoDaEtapa.Criar("RESULTADO_DA_PROVA", PapelDaPublicacaoPrevista.Preliminar);
        etapa.DefinirProdutos([resultado, gabarito]).IsSuccess.Should().BeTrue();

        return etapa;
    }

    private static RecursoDaEtapa AoAto(Guid produtoId, decimal horas) => RecursoDaEtapa.Criar(
        EventoInicialDoPrazo.PublicacaoDoAto,
        ReferenciaRegra.De(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), "v1", new string('a', 64)),
        ParametrosDoRecursoDeTeste.Declarados(horas, UnidadePrazo.Horas),
        produtoId).Value!;

    [Fact(DisplayName = "Os recursos saem na mesma ordem qualquer que seja a ordem em que foram declarados")]
    public void Recursos_DeclaradosEmOrdensDiferentes_SaemNaMesmaOrdem()
    {
        EtapaProcesso etapa = EtapaComDoisPreliminares(out ProdutoDaEtapa gabarito, out ProdutoDaEtapa resultado);
        RecursoDaEtapa doGabarito = AoAto(gabarito.Id, 24m);
        RecursoDaEtapa doResultado = AoAto(resultado.Id, 48m);

        etapa.DefinirRecursos([doResultado, doGabarito]).IsSuccess.Should().BeTrue();
        Guid[] primeiraOrdem = [.. etapa.Recursos.Select(static r => r.ProdutoAncoraId)];
        etapa.DefinirRecursos([AoAto(gabarito.Id, 24m), AoAto(resultado.Id, 48m)]).IsSuccess.Should().BeTrue();
        Guid[] segundaOrdem = [.. etapa.Recursos.Select(static r => r.ProdutoAncoraId)];

        primeiraOrdem.Should().Equal(segundaOrdem);
        primeiraOrdem.Should().Equal(gabarito.Id, resultado.Id);
    }

    [Fact(DisplayName = "A configuração declarada leva os recursos da etapa na ordem canônica, a mesma do índice que a recusa aponta")]
    public void ConfiguracaoDeclarada_Recursos_NaOrdemCanonica()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        EtapaProcesso etapa = processo.Etapas.Single();
        ProdutoDaEtapa resultado = ProdutoDaEtapa.Criar("RESULTADO_DA_PROVA", PapelDaPublicacaoPrevista.Preliminar);
        ProdutoDaEtapa gabarito = ProdutoDaEtapa.Criar("GABARITO", PapelDaPublicacaoPrevista.Preliminar);
        etapa.DefinirProdutos([resultado, gabarito]).IsSuccess.Should().BeTrue();
        etapa.DefinirRecursos([AoAto(resultado.Id, 48m), AoAto(gabarito.Id, 24m)]).IsSuccess.Should().BeTrue();

        processo.Declaracao().Avaliacao.Etapas.Single().Recursos.Select(static r => r.ProdutoAncoraId).Should().Equal(gabarito.Id, resultado.Id);
    }

    [Fact(DisplayName = "A ordem canônica separa os recursos pelo evento que inicia o prazo antes de separar pelo produto")]
    public void RecursosDaEtapa_EventoInicialDoPrazoPrecedeOProduto()
    {
        EtapaProcesso etapa = EtapaComDoisPreliminares(out ProdutoDaEtapa gabarito, out _);
        RecursoDaEtapa aoAto = AoAto(gabarito.Id, 24m);
        RecursoDaEtapa naCiencia = RecursoDaEtapa.Criar(
            EventoInicialDoPrazo.CienciaIndividual,
            ReferenciaRegra.De(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), "v1", new string('a', 64)),
            ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas),
            Guid.Empty).Value!;

        RecursoDaEtapa[] ordenados = [.. OrdemCanonicaDaConfiguracao.RecursosDaEtapa([naCiencia, aoAto], etapa.Produtos)];
        RecursoDaEtapa[] esperados = [.. new[] { naCiencia, aoAto }.OrderBy(static r => r.Ancora)];

        ordenados.Should().Equal(esperados);
    }

    [Fact(DisplayName = "O recurso idêntico a outro em tudo menos na identidade se ordena pela identidade, sempre do mesmo jeito")]
    public void RecursosDaEtapa_EmpateEmTudo_DesempataPelaIdentidade()
    {
        EtapaProcesso etapa = EtapaComDoisPreliminares(out ProdutoDaEtapa gabarito, out _);
        RecursoDaEtapa primeiro = AoAto(gabarito.Id, 24m);
        RecursoDaEtapa segundo = AoAto(gabarito.Id, 24m);

        Guid[] direta = [.. OrdemCanonicaDaConfiguracao.RecursosDaEtapa([primeiro, segundo], etapa.Produtos).Select(static r => r.Id)];
        Guid[] invertida = [.. OrdemCanonicaDaConfiguracao.RecursosDaEtapa([segundo, primeiro], etapa.Produtos).Select(static r => r.Id)];

        direta.Should().Equal(invertida);
        direta.Should().BeInAscendingOrder();
    }

    [Fact(DisplayName = "O recurso que se baseia em produto que a etapa não tem é ordenado sem lançar")]
    public void RecursosDaEtapa_ProdutoQueNaoExiste_OrdenaSemLancar()
    {
        EtapaProcesso etapa = EtapaComDoisPreliminares(out ProdutoDaEtapa gabarito, out _);

        OrdemCanonicaDaConfiguracao.RecursosDaEtapa([AoAto(Guid.CreateVersion7(), 24m), AoAto(gabarito.Id, 24m)], etapa.Produtos)
            .Should().HaveCount(2);
    }

    [Fact(DisplayName = "Os recursos e os produtos são obrigatórios")]
    public void RecursosDaEtapa_ArgumentoNulo_Lanca()
    {
        FluentActions.Invoking(() => OrdemCanonicaDaConfiguracao.RecursosDaEtapa(null!, [])).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => OrdemCanonicaDaConfiguracao.RecursosDaEtapa([], null!)).Should().Throw<ArgumentNullException>();
    }
}
