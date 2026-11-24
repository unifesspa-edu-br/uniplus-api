namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static ModeloDeEtapa;

public sealed class EtapaDeclaradaTests
{
    private static (string? Campo, string Codigo)[] Recusas(EtapaDeclarada etapa) =>
        [.. etapa.Recusas.Select(static r => (r.Field, r.Error.Code))];

    private static ModeloDeEtapa ModeloComRecusaEmCadaPublicacao() =>
        new("Prova")
        {
            Peso = -1m,
            Produtos = [Preliminar("GABARITO"), Preliminar("GABARITO")],
            Inicio = new DateTimeOffset(2026, 3, 5, 0, 0, 0, TimeSpan.Zero),
            Fim = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            TiposDeDecisao = ["TIPO_INEXISTENTE"],
        };

    private static EtapaDeclarada EtapaComRecusaEmTodoDado() =>
        new ModeloDeEtapa(" ")
        {
            Carater = (CaraterEtapa)99,
            Peso = -1m,
            NotaMinima = -1m,
            NotaMaxima = -1m,
            Passo = -1m,
            Ordem = 0,
            Itens = ItensInvalidos,
            Escala = EscalaInvalida,
        }.Declarar();

    private static EtapaDeclarada EtapaDoEnemComRecusaEmTodoDado()
    {
        ReferenciaRegra regra = ReferenciaRegra.De(RegraDeApuracaoDaAvaliacao.AvaliacaoUnica.Codigo(), "v1", new string('a', 64));

        return (new ModeloDeEtapa("Prova").DoEnem() with
        {
            Forma = FormaDeAvaliacao.Objetiva,
            RegraConsolidacao = regra,
            Itens = ItensInvalidos,
            Escala = EscalaInvalida,
            Conceito = NotaDeZeroADez(),
        }).Declarar();
    }

    private static readonly ItemDeAvaliacaoDeclarado[] ItensInvalidos = [new(null, " ", -1m)];

    private static readonly NivelDeAtendimento[] EscalaInvalida = [new(" ", 2m)];

    private static EtapaDeclarada EtapaQueEmiteParecerEPublicaPreliminares()
    {
        ProdutoDaEtapa preliminar = Preliminar("GABARITO");

        return new ModeloDeEtapa("Prova")
        {
            EmiteParecerIndividual = true,
            Produtos = [preliminar, Definitivo("RESULTADO"), Preliminar("RESULTADO")],
            Recursos = [RecursoModelo.AoAto(preliminar.Id)],
            TiposDeDecisao = ["INDEFERIMENTO", "DEFERIMENTO"],
        }.Declarar();
    }

    [Fact(DisplayName = "Etapa conforme é declarada sem recusas e com os dados normalizados")]
    public void Declarar_EtapaConforme_SemRecusas()
    {
        EtapaDeclarada etapa = new ModeloDeEtapa("  Prova objetiva  ") { Ordem = 1, FaseCodigo = " AVALIACAO " }.Declarar();

        etapa.Recusas.Should().BeEmpty();
        etapa.Nome.Should().Be("Prova objetiva");
        etapa.FaseCodigo.Should().Be("AVALIACAO");
        etapa.ComponeNota.Should().BeTrue();
        etapa.NotaVemDoEnem.Should().BeFalse();
    }

    [Fact(DisplayName = "As recusas se acumulam e a etapa continua existindo com o que foi declarado")]
    public void Declarar_VariasRecusas_AcumulaEMantemOsDados()
    {
        EtapaDeclarada etapa = new ModeloDeEtapa("   ") { Peso = -1m, Ordem = 0 }.Declarar();

        Recusas(etapa).Should().Equal(
            ("nome", "EtapaProcesso.NomeObrigatorio"),
            ("peso", "EtapaProcesso.PesoInvalido"),
            ("ordem", "EtapaProcesso.OrdemInvalida"));
        etapa.Peso.Should().Be(-1m);
        etapa.Ordem.Should().Be(0);
    }

    [Fact(DisplayName = "As recusas da publicação saem depois das da avaliação, na ordem da gravação")]
    public void Declarar_RecusasDasPublicacoes_SaemNaOrdemDaGravacao()
    {
        ModeloDeEtapa modelo = ModeloComRecusaEmCadaPublicacao();

        Recusas(modelo.Declarar()).Select(static r => r.Codigo).Should().Equal(
            "EtapaProcesso.PesoInvalido",
            "EtapaProcesso.AtoDuplicadoNaEtapa",
            "EtapaProcesso.JanelaInvertida",
            "MotivoDeDecisao.TipoInvalido");
    }

    [Fact(DisplayName = "Na leitura do documento o ato repetido recusa a lista dos produtos inteira, uma vez só, sem posição de produto")]
    public void Declarar_AtoRepetido_RecusaAListaInteiraUmaVez()
    {
        ModeloDeEtapa modelo = new("Prova") { Produtos = [Preliminar("ATO_A"), Preliminar("ATO_B"), Preliminar("ATO_B"), Preliminar("ATO_A")] };

        Recusas(modelo.Declarar()).Should().Equal(("produtos", "EtapaProcesso.AtoDuplicadoNaEtapa"));
    }

    [Fact(DisplayName = "A recusa da âncora das janelas recursais acompanha o campo dos recursos")]
    public void Declarar_AncoraForaDosPreliminares_RecusaNosRecursos()
    {
        ProdutoDaEtapa definitivo = Definitivo("GABARITO");
        ModeloDeEtapa modelo = new ModeloDeEtapa("Prova") { Produtos = [definitivo], Recursos = [RecursoModelo.AoAto(definitivo.Id)] };

        Recusas(modelo.Declarar()).Should().Equal(("recursos", "EtapaProcesso.AncoraNaoEhProdutoPreliminarDaEtapa"));
    }

    [Fact(DisplayName = "Cada dado de avaliação recusado é reconhecido pelo campo raiz da recusa")]
    public void Recusado_ReconhecePeloCampoRaiz()
    {
        EtapaDeclarada etapa = new ModeloDeEtapa("Prova")
        {
            Peso = -1m,
            Itens = [new ItemDeAvaliacaoDeclarado(null, " ", 5m)],
        }.Declarar();

        etapa.Recusado(DadoDaEtapa.Peso).Should().BeTrue();
        etapa.Recusado(DadoDaEtapa.ItensDeAvaliacao).Should().BeTrue();
        etapa.Recusado(DadoDaEtapa.Carater).Should().BeFalse();
        etapa.Recusado(DadoDaEtapa.NotaMaxima).Should().BeFalse();
    }

    [Fact(DisplayName = "Recusa de outro campo não marca dado de avaliação")]
    public void Recusado_RecusaDeOutroCampo_NaoMarcaDado()
    {
        EtapaDeclarada etapa = new ModeloDeEtapa("Prova")
        {
            Inicio = new DateTimeOffset(2026, 3, 5, 0, 0, 0, TimeSpan.Zero),
            Fim = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
        }.Declarar();

        etapa.Recusas.Should().ContainSingle();
        Enum.GetValues<DadoDaEtapa>().Should().OnlyContain(dado => !etapa.Recusado(dado));
    }

    [Fact(DisplayName = "Todo campo emitido pelas validações da avaliação tem o dado da etapa que ele marca")]
    public void Recusado_TodoCampoDaAvaliacaoTemDado()
    {
        EtapaDeclarada comum = EtapaComRecusaEmTodoDado();
        EtapaDeclarada doEnem = EtapaDoEnemComRecusaEmTodoDado();

        foreach (EtapaDeclarada etapa in new[] { comum, doEnem })
        {
            etapa.Recusas.Select(static r => r.Field!.Split('[', '.')[0]).Distinct().Should().OnlyContain(campo => CamposDosDados.Contains(campo));
        }

        Enum.GetValues<DadoDaEtapa>().Should().OnlyContain(dado => comum.Recusado(dado) || doEnem.Recusado(dado));
    }

    private static readonly string[] CamposDosDados =
    [
        "nome", "carater", "peso", "notaMinima", "notaMaxima", "passoDoLancamento", "ordem", "itensDeAvaliacao",
        "escalaDeAtendimento", "formaDeAvaliacao", "regraConsolidacao", "conceito",
    ];

    [Fact(DisplayName = "Os atos da etapa dizem o que ela publica, se promete parecer e se há recurso contra ato")]
    public void Atos_DizemOQueAEtapaPublica()
    {
        EtapaDeclarada etapa = EtapaQueEmiteParecerEPublicaPreliminares();

        etapa.Atos.EmiteParecerIndividual.Should().BeTrue();
        etapa.Atos.TemProdutos.Should().BeTrue();
        etapa.Atos.TemRecursos.Should().BeTrue();
        etapa.Atos.TemRecursoEmAto.Should().BeTrue();
        etapa.Atos.TiposComCicloAberto().Should().Equal("GABARITO");
        etapa.Atos.Produtos.Select(static p => p.Posicao).Should().Equal(0, 1, 2);
    }

    [Fact(DisplayName = "As bancas da etapa dizem se há banca, onde cada uma está e com que papel julgam o recurso")]
    public void Bancas_DizemSeHaBancaEOndeCadaUmaEsta()
    {
        EtapaDeclarada semBanca = new ModeloDeEtapa("Prova").Declarar();
        EtapaDeclarada comRecursos = new ModeloDeEtapa("Prova") { Bancas = [BancasDeTeste.DeRecursosDaEtapa()] }.Declarar();

        semBanca.Bancas.TemBancas.Should().BeFalse();
        comRecursos.Bancas.TemBancas.Should().BeTrue();
        comRecursos.Bancas.Declaradas.Should().ContainSingle().Which.Should().Match<BancaDeclarada>(
            static banca => banca.Posicao == 0 && banca.Tipo.JulgaAPrimeiraInstancia && banca.Regra != null);
        comRecursos.Bancas.BancasDaSegundaInstancia.Should().BeEmpty();
    }

    [Fact(DisplayName = "As bancas da etapa separam, pelo papel copiado e na posição declarada, as que julgam em 2ª instância")]
    public void Bancas_SeparamAsDaSegundaInstanciaPeloPapel()
    {
        EtapaDeclarada etapa = new ModeloDeEtapa("Prova") { Bancas = [BancasDeTeste.DeRecursosDaEtapa(), BancasDeTeste.DaEtapa(TipoDeBanca.AnaliseDeRecursosDeSegundaInstancia)] }.Declarar();

        etapa.Bancas.BancasDaSegundaInstancia.Should().ContainSingle().Which.Should().Match<BancaDeclarada>(
            static banca => banca.Posicao == 1 && banca.Tipo.JulgaASegundaInstancia && !banca.Tipo.JulgaAPrimeiraInstancia && banca.Tipo.JulgaRecurso);
    }

    [Fact(DisplayName = "Declaração nula ou sem identificador é erro de programação")]
    public void Declarar_EntradaInvalida_Lanca()
    {
        ModeloDeEtapa modelo = new("Prova");
        Action semId = () => EtapaDeclarada.Declarar(
            Guid.Empty, new IdentificacaoDaEtapa("Prova", CaraterEtapa.Classificatoria, TipoDeProva(), null, null),
            new AvaliacaoDeclaradaDaEtapa(new DeclaracaoDaAvaliacaoDaEtapa(1m, null, 10m, null, null, null, null), [], null),
            new JanelaDeTempo(null, null), new PublicacoesDaEtapa([], false, []), []);
        Action semAvaliacao = () => EtapaDeclarada.Declarar(
            modelo.Id, new IdentificacaoDaEtapa("Prova", CaraterEtapa.Classificatoria, TipoDeProva(), null, null),
            null!, new JanelaDeTempo(null, null), new PublicacoesDaEtapa([], false, []), []);

        semId.Should().Throw<ArgumentException>();
        semAvaliacao.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "O tipo de etapa não resolvido é erro de programação, e a exceção nomeia o parâmetro que o traz")]
    public void TipoDeEtapaNaoResolvido_LancaNomeandoOParametro()
    {
        ModeloDeEtapa modelo = new("Prova");
        IdentificacaoDaEtapa semTipo = new("Prova", CaraterEtapa.Classificatoria, null!, null, null);
        DeclaracaoDaAvaliacaoDaEtapa declaracao = new(1m, null, 10m, null, null, null, null);
        Action declarar = () => EtapaDeclarada.Declarar(
            modelo.Id, semTipo, new AvaliacaoDeclaradaDaEtapa(declaracao, [], null), new JanelaDeTempo(null, null), new PublicacoesDaEtapa([], false, []), []);
        Action reidratar = () => EtapaProcesso.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!, modelo.Id, semTipo, declaracao, null);

        declarar.Should().Throw<ArgumentNullException>().WithParameterName("identificacao");
        reidratar.Should().Throw<ArgumentNullException>().WithParameterName("tipoEtapa");
    }

    private static TipoEtapaSnapshot TipoDeProva() =>
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA", "Prova", true, true, false).Value!;
}
