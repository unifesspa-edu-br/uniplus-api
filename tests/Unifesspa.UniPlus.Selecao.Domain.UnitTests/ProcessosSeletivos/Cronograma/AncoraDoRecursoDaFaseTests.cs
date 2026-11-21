namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// A âncora do prazo de recurso da fase: a conferência de que ela aponta para um produto preliminar da
/// própria fase e o reapontamento dela quando os produtos são recriados ou reconciliados.
/// </summary>
public sealed class AncoraDoRecursoDaFaseTests
{
    private const string Fase = "RESULTADO_PRELIMINAR";
    private const string Gabarito = "GABARITO";
    private const string Resultado = "RESULTADO_CLASSIFICACAO";

    private static RegraRecursoFase Recurso(Guid produtoAncoraId) => RegraRecursoFase.Criar(
        ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)),
        ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas),
        produtoAncoraId).Value!;

    private static ProdutoDaFase Preliminar(string ato) => ProdutoDaFase.Criar(ato, PapelProdutoFase.Preliminar);

    private static ProdutoDaFase Definitivo(string ato) => ProdutoDaFase.Criar(ato, PapelProdutoFase.Definitivo);

    [Fact(DisplayName = "Fase sem regra de recurso não tem âncora a conferir")]
    public void Violacao_SemRegraDeRecurso_NaoViola()
    {
        AncoraDoRecursoDaFase.Violacao(Fase, [Definitivo(Resultado)], regraRecurso: null).Should().BeNull();
    }

    [Fact(DisplayName = "Fase que admite recurso e não publica produto preliminar é recusada no campo dos produtos")]
    public void Violacao_SemProdutoPreliminar_RecusaNosProdutos()
    {
        FieldError? violacao = AncoraDoRecursoDaFase.Violacao(
            Fase, [Definitivo(Resultado)], Recurso(Guid.CreateVersion7()));

        violacao.Should().NotBeNull();
        violacao!.Field.Should().Be("produtos");
        violacao.Error.Code.Should().Be("RegraRecursoFase.FaseSemProdutoPreliminar");
        violacao.Error.Message.Should().Contain($"'{Fase}'");
    }

    [Fact(DisplayName = "Âncora em produto definitivo é recusada e a mensagem lista os preliminares em ordem alfabética")]
    public void Violacao_AncoraEmDefinitivo_ListaOsPreliminaresOrdenados()
    {
        ProdutoDaFase definitivo = Definitivo(Resultado);

        FieldError? violacao = AncoraDoRecursoDaFase.Violacao(
            Fase, [Preliminar("ZETA"), definitivo, Preliminar("ALFA")], Recurso(definitivo.Id));

        violacao.Should().NotBeNull();
        violacao!.Field.Should().Be("regraRecurso.atoAncoraCodigo");
        violacao.Error.Code.Should().Be("RegraRecursoFase.AncoraNaoEhProdutoPreliminarDaFase");
        violacao.Error.Message.Should().Contain("(ALFA, ZETA)");
    }

    [Fact(DisplayName = "Âncora em produto de outra fase é recusada")]
    public void Violacao_AncoraEmProdutoDeOutraFase_Recusa()
    {
        FieldError? violacao = AncoraDoRecursoDaFase.Violacao(
            Fase, [Preliminar(Resultado)], Recurso(Preliminar(Resultado).Id));

        violacao!.Error.Code.Should().Be("RegraRecursoFase.AncoraNaoEhProdutoPreliminarDaFase");
    }

    [Fact(DisplayName = "Âncora no produto preliminar da própria fase é aceita")]
    public void Violacao_AncoraNoPreliminarDaFase_NaoViola()
    {
        ProdutoDaFase preliminar = Preliminar(Resultado);

        AncoraDoRecursoDaFase.Violacao(Fase, [preliminar, Definitivo("OUTRO")], Recurso(preliminar.Id)).Should().BeNull();
    }

    [Fact(DisplayName = "Preliminar e definitivo da mesma matéria não se confundem: só a âncora no preliminar vale")]
    public void Violacao_PreliminarEDefinitivoDaMesmaMateria_DistingueOPapel()
    {
        ProdutoDaFase definitivo = Definitivo(Gabarito);
        ProdutoDaFase preliminar = Preliminar(Gabarito);
        ProdutoDaFase[] produtos = [definitivo, preliminar];

        AncoraDoRecursoDaFase.Violacao(Fase, produtos, Recurso(preliminar.Id)).Should().BeNull();
        AncoraDoRecursoDaFase.Violacao(Fase, produtos, Recurso(definitivo.Id))!
            .Error.Code.Should().Be("RegraRecursoFase.AncoraNaoEhProdutoPreliminarDaFase");
    }

    [Fact(DisplayName = "Reancorar leva a âncora ao produto de destino que tem o mesmo par ato e papel")]
    public void Reancorar_PorPar_ApontaParaOProdutoEquivalenteDoDestino()
    {
        ProdutoDaFase preliminarDeOrigem = Preliminar(Gabarito);
        ProdutoDaFase[] origem = [preliminarDeOrigem, Definitivo(Gabarito)];
        ProdutoDaFase definitivoDeDestino = Definitivo(Gabarito);
        ProdutoDaFase preliminarDeDestino = Preliminar(Gabarito);
        RegraRecursoFase regra = Recurso(preliminarDeOrigem.Id);

        AncoraDoRecursoDaFase.Reancorar(regra, origem, [definitivoDeDestino, preliminarDeDestino]);

        regra.ProdutoAncoraId.Should().Be(preliminarDeDestino.Id);
    }

    [Fact(DisplayName = "Reancorar deixa a âncora como está quando ela não está entre os produtos de origem")]
    public void Reancorar_AncoraAusenteNaOrigem_FicaComoEstava()
    {
        Guid ancoraDeOutraFase = Guid.CreateVersion7();
        RegraRecursoFase regra = Recurso(ancoraDeOutraFase);

        AncoraDoRecursoDaFase.Reancorar(regra, [Preliminar(Gabarito)], [Preliminar(Gabarito)]);

        regra.ProdutoAncoraId.Should().Be(ancoraDeOutraFase);
    }

    [Fact(DisplayName = "Reancorar deixa a âncora como está quando o destino não tem o par")]
    public void Reancorar_DestinoSemOPar_FicaComoEstava()
    {
        ProdutoDaFase preliminarDeOrigem = Preliminar(Gabarito);
        RegraRecursoFase regra = Recurso(preliminarDeOrigem.Id);

        AncoraDoRecursoDaFase.Reancorar(regra, [preliminarDeOrigem], [Definitivo(Gabarito), Preliminar(Resultado)]);

        regra.ProdutoAncoraId.Should().Be(preliminarDeOrigem.Id);
    }

    [Fact(DisplayName = "Reancorar cobre os dois remapeamentos da restauração: do congelado ao recriado e do recriado ao rastreado")]
    public void Reancorar_DoisRemapeamentosSucessivos_SeguemAteOProdutoRastreado()
    {
        ProdutoDaFase preliminarCongelado = Preliminar(Gabarito);
        ProdutoDaFase[] congelados = [Definitivo(Gabarito), preliminarCongelado];
        ProdutoDaFase[] recriados = [.. congelados.Select(static p => ProdutoDaFase.Criar(p.AtoCodigo, p.Papel))];
        ProdutoDaFase[] rastreados = [Preliminar(Gabarito), Definitivo(Gabarito)];
        RegraRecursoFase regra = Recurso(preliminarCongelado.Id);

        AncoraDoRecursoDaFase.Reancorar(regra, congelados, recriados);
        regra.ProdutoAncoraId.Should().Be(recriados.Single(static p => p.Papel == PapelProdutoFase.Preliminar).Id);

        AncoraDoRecursoDaFase.Reancorar(regra, recriados, rastreados);
        regra.ProdutoAncoraId.Should().Be(rastreados.Single(static p => p.Papel == PapelProdutoFase.Preliminar).Id);
    }
}
