namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Contratos;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>
/// A origem liga a posição canônica de cada item da declaração à posição dele no documento recebido e guarda as partes que a
/// leitura recusou; a declaração que vem da raiz tem a identidade, e a família das declarações entrega as partes recusadas.
/// </summary>
public sealed class OrigemNoDocumentoTests
{
    private static readonly string[] ListasDaConferencia =
    [
        "fases",
        "fases[1].produtos",
        "fases[1].bancasRequeridas",
        "etapas",
        "etapas[2].produtos",
        "etapas[2].bancas",
        "etapas[0].recursos",
        "formularios",
        "formularios[1].itens",
        "formularios[1].grupos",
        "formularios[1].grupos[0].subitens",
        "distribuicaoVagas",
        "criterios",
        "criterios[0].areas",
        "regrasEliminacao",
        "exigencias",
        "termos",
        "motivos",
    ];

    public static TheoryData<string> ListasQueAConferenciaApontaPorPosicao => [.. ListasDaConferencia];

    [Theory(DisplayName = "A declaração que vem da raiz tem a identidade: a posição de origem é a canônica em cada lista")]
    [MemberData(nameof(ListasQueAConferenciaApontaPorPosicao))]
    public void Declaracao_DaRaiz_TemAIdentidadeEmCadaLista(string lista)
    {
        OrigemNoDocumento origem = ConfiguracaoDeclaradaDeTeste.Vazia().Origem;

        origem.Should().BeSameAs(OrigemNoDocumento.Identidade);
        origem.PosicaoDeOrigem(lista, 0).Should().Be(0);
        origem.PosicaoDeOrigem(lista, 7).Should().Be(7);
    }

    [Fact(DisplayName = "A declaração que vem da raiz não tem parte recusada")]
    public void Declaracao_DaRaiz_NaoTemParteRecusada() =>
        ConfiguracaoDeclaradaDeTeste.Vazia().Origem.PartesRecusadas.Should().BeEmpty();

    [Fact(DisplayName = "A lista guardada na origem devolve a posição de origem de cada posição canônica")]
    public void PosicaoDeOrigem_ListaGuardada_DevolveAPosicaoDoDocumento()
    {
        OrigemNoDocumento origem = new(new Dictionary<string, IReadOnlyList<int>> { ["etapas"] = [2, 0, 1] }, []);

        Enumerable.Range(0, 3).Select(posicao => origem.PosicaoDeOrigem("etapas", posicao)).Should().Equal(2, 0, 1);
    }

    [Fact(DisplayName = "A posição canônica que a lista guardada não tem é recusada")]
    public void PosicaoDeOrigem_PosicaoForaDaLista_Lanca()
    {
        OrigemNoDocumento origem = new(new Dictionary<string, IReadOnlyList<int>> { ["etapas"] = [1, 0] }, []);

        Assert.Throws<ArgumentOutOfRangeException>(() => origem.PosicaoDeOrigem("etapas", 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => origem.PosicaoDeOrigem("etapas", -1));
    }

    [Fact(DisplayName = "A lista sem nome não tem posição de origem")]
    public void PosicaoDeOrigem_ListaSemNome_Lanca() =>
        Assert.Throws<ArgumentException>(() => OrigemNoDocumento.Identidade.PosicaoDeOrigem(string.Empty, 0));

    [Fact(DisplayName = "A parte recusada guarda a lista, a chave, a posição de origem e a causa que a leitura deu")]
    public void ParteRecusada_GuardaOQueALeituraSabe()
    {
        FieldError causa = new("/cronograma/fases/3/bancasRequeridas/1", new DomainError("A.Leitura", "recusada"));

        ParteRecusada parte = new("fases[0].bancasRequeridas", "banca-a", 1, causa);

        (parte.Colecao, parte.Chave, parte.PosicaoDeOrigem, parte.Causa).Should().Be(("fases[0].bancasRequeridas", "banca-a", 1, causa));
    }

    [Fact(DisplayName = "A parte recusada sem chave guarda a chave ausente, e a posição de origem não é negativa")]
    public void ParteRecusada_SemChave_EPosicaoNaoNegativa()
    {
        FieldError causa = new("/etapas/0", new DomainError("A.Leitura", "recusada"));

        new ParteRecusada("etapas", null, 0, causa).Chave.Should().BeNull();
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParteRecusada("etapas", null, -1, causa));
    }

    [Fact(DisplayName = "A família das declarações entrega as partes recusadas na ordem em que o documento as traz, com a causa como ponteiro")]
    public void FamiliaDasDeclaracoes_EntregaAsPartesRecusadasNaOrdemDeEntrada()
    {
        FieldError[] daLeitura =
        [
            new("/cronograma/fases/0/bancasRequeridas/1", new DomainError("A.Banca", "banca")),
            new("/etapas/2/produtos/0", new DomainError("A.Produto", "produto")),
            new("/etapas/2/bancas/4", new DomainError("A.Outra", "outra")),
        ];
        ContextoDaConferencia contexto = DeclaracoesComDefeitoParaOEsquema.ContextoComTodosOsDefeitos();

        IReadOnlyList<FieldError> causas = DeclaracoesDe(ComPartesRecusadas(contexto, daLeitura));

        causas.Take(3).Should().Equal(daLeitura);
        causas.Skip(3).Should().Equal([.. contexto.Cronograma.RecusasDasDeclaracoes(), .. contexto.Etapas.RecusasDasDeclaracoes()]);
    }

    private static ContextoDaConferencia ComPartesRecusadas(ContextoDaConferencia contexto, FieldError[] causas) =>
        contexto with
        {
            Configuracao = contexto.Configuracao.ComOrigem(
                new OrigemNoDocumento(new Dictionary<string, IReadOnlyList<int>>(), [.. causas.Select(static (causa, posicao) => new ParteRecusada("lista", null, posicao, causa))])),
        };

    private static IReadOnlyList<FieldError> DeclaracoesDe(ContextoDaConferencia contexto) =>
        [.. CausasQueImpedemCongelar.Familias.Single(static familia => familia.Nome == NomeDaFamilia.Declaracoes).Causas(contexto)];

    [Fact(DisplayName = "A família das declarações sem parte recusada entrega só as recusas das declarações que ficaram")]
    public void FamiliaDasDeclaracoes_SemParteRecusada_EntregaSoAsRecusasDasDeclaracoes()
    {
        ContextoDaConferencia contexto = DeclaracoesComDefeitoParaOEsquema.ContextoComTodosOsDefeitos();

        DeclaracoesDe(contexto).Should().Equal([.. contexto.Cronograma.RecusasDasDeclaracoes(), .. contexto.Etapas.RecusasDasDeclaracoes()]);
    }

    [Fact(DisplayName = "A causa de uma declaração que ficou sai com o ponteiro da posição de origem, e a da parte recusada, como a leitura a deu")]
    public void ConferenciaCompleta_ConverteASoDaDeclaracaoEMantemADaParteRecusada()
    {
        FieldError daParte = new("/etapas/9/produtos/0", new DomainError("A.Produto", "produto"));
        ContextoDaConferencia contexto = DeclaracoesComDefeitoParaOEsquema.ContextoComTodosOsDefeitos();
        OrigemNoDocumento origem = new(
            new Dictionary<string, IReadOnlyList<int>> { ["fases"] = [3, 2, 1, 0], ["etapas"] = [6, 5, 4, 3, 2, 1, 0] },
            [new("etapas[9].produtos", null, 0, daParte)]);
        ContextoDaConferencia lida = contexto with { Configuracao = contexto.Configuracao.ComOrigem(origem) };

        IReadOnlyList<FieldError> convertidas = PonteiroNoDocumento.Converter(CausasQueImpedemCongelar.De(lida), lida.Configuracao.Origem);

        convertidas.Select(static causa => causa.Field).Should()
            .Contain(["/etapas/9/produtos/0", "/cronograma/fases/2/faseCanonicaCodigo", "/etapas/5/chave"]);
        convertidas.Count(static causa => causa.Field == "/etapas/9/produtos/0").Should().Be(1);
    }
}
