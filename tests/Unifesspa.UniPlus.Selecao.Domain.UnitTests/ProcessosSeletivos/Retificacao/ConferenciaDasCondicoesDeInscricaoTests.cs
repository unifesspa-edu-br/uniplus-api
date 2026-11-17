namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Depois de inscrição enviada, a oferta e as modalidades concorridas não saem, a taxa não muda e, havendo pedido de
/// isenção, os critérios de isenção ficam (UNI-REQ-0212, UNI-REQ-0144).
/// </summary>
public sealed class ConferenciaDasCondicoesDeInscricaoTests
{
    private static readonly Guid Oferta = Guid.Parse("00000000-0000-7000-8000-000000000001");
    private static readonly Guid OutraOferta = Guid.Parse("00000000-0000-7000-8000-000000000002");
    private static readonly Guid Ampla = Guid.Parse("00000000-0000-7000-8000-0000000000a1");
    private static readonly Guid Cota = Guid.Parse("00000000-0000-7000-8000-0000000000a2");

    private static ModalidadeSelecionada Modalidade(Guid origem) => ModalidadeSelecionada.Criar(
        origem, "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
        null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021",
        quantidadeDeclarada: 40).Value!;

    private static DistribuicaoDeVagasDaOferta Distribuicao(Guid oferta, params Guid[] modalidades) =>
        new(oferta, [.. modalidades.Select(Modalidade)]);

    private static ConfiguracaoTaxaInscricao Taxa(bool cobra, decimal? valor, params string[] fundamentos) =>
        ConfiguracaoTaxaInscricao.Criar(cobra, valor, fundamentos).Value!;

    private static FatosDaExecucao Fatos(
        Dictionary<Guid, IReadOnlySet<Guid>>? concorridas = null, bool inscricao = false, bool pedidoDeIsencao = false) => new()
        {
            Instante = FaseDaRetificacaoDeTeste.Instante,
            FasesIniciadas = FasesIniciadas.Nenhuma,
            ModalidadesConcorridasPorOferta = concorridas ?? [],
            HaInscricaoEnviada = inscricao,
            HaPedidoDeIsencao = pedidoDeIsencao,
        };

    private static Dictionary<Guid, IReadOnlySet<Guid>> Concorridas(Guid oferta, params Guid[] modalidades) =>
        new() { [oferta] = new HashSet<Guid>(modalidades) };

    private static IEnumerable<(string? Campo, string Codigo)> Recusas(IReadOnlyList<FieldError> recusas) =>
        recusas.Select(static r => (r.Field, r.Error.Code));

    [Fact(DisplayName = "A oferta em que há inscrição enviada não sai do documento")]
    public void OfertaComInscricao_Retirada_Recusa()
    {
        Recusas(ConferenciaDasCondicoesDeInscricao.Conferir([Distribuicao(OutraOferta, Ampla)], null, null, Fatos(Concorridas(Oferta, Ampla)))).Should().Equal(
            ("distribuicaoVagas", RetificacaoErrorCodes.OfertaComInscricaoNaoSai));
    }

    [Fact(DisplayName = "Cada modalidade concorrida que sai da oferta é recusada, ampla e cota juntas, em ordem de origem")]
    public void ModalidadesConcorridas_Retiradas_Acumulam()
    {
        Recusas(ConferenciaDasCondicoesDeInscricao.Conferir([Distribuicao(Oferta)], null, null, Fatos(Concorridas(Oferta, Cota, Ampla)))).Should().Equal(
            ("distribuicaoVagas[0].modalidades", RetificacaoErrorCodes.ModalidadeConcorridaNaoSai),
            ("distribuicaoVagas[0].modalidades", RetificacaoErrorCodes.ModalidadeConcorridaNaoSai));
    }

    [Fact(DisplayName = "A modalidade sem inscrição pode sair, e a concorrida mantida passa; acrescentar modalidade e oferta passa")]
    public void ModalidadeSemInscricao_PodeSair()
    {
        ConferenciaDasCondicoesDeInscricao.Conferir([Distribuicao(Oferta, Ampla), Distribuicao(OutraOferta, Cota)], null, null, Fatos(Concorridas(Oferta, Ampla))).Should().BeEmpty();
        ConferenciaDasCondicoesDeInscricao.Conferir([Distribuicao(Oferta, Ampla)], null, null, Fatos(Concorridas(Oferta, Ampla))).Should().BeEmpty();
    }

    [Fact(DisplayName = "Acrescentar modalidade à oferta concorrida passa")]
    public void ModalidadeAcrescentada_Passa()
    {
        ConferenciaDasCondicoesDeInscricao.Conferir([Distribuicao(Oferta, Ampla, Cota)], null, null, Fatos(Concorridas(Oferta, Ampla))).Should().BeEmpty();
    }

    [Fact(DisplayName = "A mesma modalidade em ofertas distintas é conferida oferta por oferta")]
    public void MesmaModalidade_EmOfertasDistintas_ConferidaPorOferta()
    {
        Dictionary<Guid, IReadOnlySet<Guid>> concorridas = new() { [Oferta] = new HashSet<Guid> { Ampla }, [OutraOferta] = new HashSet<Guid> { Ampla } };

        Recusas(ConferenciaDasCondicoesDeInscricao.Conferir([Distribuicao(Oferta, Ampla), Distribuicao(OutraOferta, Cota)], null, null, Fatos(concorridas))).Should().Equal(
            ("distribuicaoVagas[1].modalidades", RetificacaoErrorCodes.ModalidadeConcorridaNaoSai));
    }

    [Fact(DisplayName = "Todas as ofertas retiradas vêm antes das modalidades retiradas, ainda que a oferta com modalidade retirada tenha origem menor")]
    public void OfertasAntesDasModalidades()
    {
        Dictionary<Guid, IReadOnlySet<Guid>> concorridas = new() { [Oferta] = new HashSet<Guid> { Ampla }, [OutraOferta] = new HashSet<Guid> { Ampla } };

        Recusas(ConferenciaDasCondicoesDeInscricao.Conferir([Distribuicao(Oferta, Cota)], null, null, Fatos(concorridas))).Should().Equal(
            ("distribuicaoVagas", RetificacaoErrorCodes.OfertaComInscricaoNaoSai),
            ("distribuicaoVagas[0].modalidades", RetificacaoErrorCodes.ModalidadeConcorridaNaoSai));
    }

    [Fact(DisplayName = "Com inscrição enviada, a taxa não declarada nos dois lados passa")]
    public void Taxa_NaoDeclaradaNosDoisLados_Passa()
    {
        ConferenciaDasCondicoesDeInscricao.Conferir([], null, null, Fatos(inscricao: true, pedidoDeIsencao: true)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem inscrição enviada, oferta, modalidade e taxa mudam livremente")]
    public void SemInscricao_MudaLivremente()
    {
        ConferenciaDasCondicoesDeInscricao.Conferir([], Taxa(true, 100m, FundamentoIsencaoCodigo.CadastroUnico), Taxa(false, null), Fatos()).Should().BeEmpty();
    }

    [Theory(DisplayName = "Com inscrição enviada, mudar o valor da taxa ou o cobrar é recusado, para mais ou para menos")]
    [InlineData(true, 100, true, 120)]
    [InlineData(true, 100, true, 80)]
    [InlineData(true, 100, false, null)]
    [InlineData(false, null, true, 100)]
    public void Taxa_ComInscricao_Muda_Recusa(bool cobraAntes, int? valorAntes, bool cobraDepois, int? valorDepois)
    {
        ConfiguracaoTaxaInscricao antes = Taxa(cobraAntes, valorAntes, cobraAntes ? [FundamentoIsencaoCodigo.CadastroUnico] : []);
        ConfiguracaoTaxaInscricao depois = Taxa(cobraDepois, valorDepois, cobraDepois ? [FundamentoIsencaoCodigo.CadastroUnico] : []);

        Recusas(ConferenciaDasCondicoesDeInscricao.Conferir([], antes, depois, Fatos(inscricao: true))).Should().Equal(
            ("taxaDeInscricao", RetificacaoErrorCodes.TaxaComInscricaoNaoMuda));
    }

    [Fact(DisplayName = "Com inscrição enviada, a taxa igual passa, e os critérios de isenção mudam sem pedido de isenção")]
    public void Taxa_ComInscricao_Igual_Passa()
    {
        ConferenciaDasCondicoesDeInscricao.Conferir([], Taxa(true, 100m, FundamentoIsencaoCodigo.CadastroUnico), Taxa(true, 100m, FundamentoIsencaoCodigo.CadastroUnico), Fatos(inscricao: true)).Should().BeEmpty();
        ConferenciaDasCondicoesDeInscricao.Conferir([], Taxa(true, 100m, FundamentoIsencaoCodigo.CadastroUnico), Taxa(true, 100m, FundamentoIsencaoCodigo.DoacaoMedulaOssea), Fatos(inscricao: true)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Com pedido de isenção feito, mudar os critérios de isenção é recusado, e a mesma lista em outra ordem passa")]
    public void CriteriosDeIsencao_ComPedido()
    {
        Recusas(ConferenciaDasCondicoesDeInscricao.Conferir([], Taxa(true, 100m, FundamentoIsencaoCodigo.CadastroUnico), Taxa(true, 100m, FundamentoIsencaoCodigo.DoacaoMedulaOssea), Fatos(inscricao: true, pedidoDeIsencao: true))).Should().Equal(
            ("taxaDeInscricao.fundamentos", RetificacaoErrorCodes.CriteriosDeIsencaoComPedidoNaoMudam));
        ConferenciaDasCondicoesDeInscricao.Conferir([], Taxa(true, 100m, FundamentoIsencaoCodigo.CadastroUnico, FundamentoIsencaoCodigo.DoacaoMedulaOssea), Taxa(true, 100m, FundamentoIsencaoCodigo.DoacaoMedulaOssea, FundamentoIsencaoCodigo.CadastroUnico), Fatos(inscricao: true, pedidoDeIsencao: true)).Should().BeEmpty();
    }

    [Fact(DisplayName = "As recusas de oferta, taxa e critérios acumulam na ordem fixada")]
    public void Recusas_Acumulam()
    {
        Recusas(ConferenciaDasCondicoesDeInscricao.Conferir([], Taxa(true, 100m, FundamentoIsencaoCodigo.CadastroUnico), Taxa(true, 90m, FundamentoIsencaoCodigo.DoacaoMedulaOssea), Fatos(Concorridas(Oferta, Ampla), inscricao: true, pedidoDeIsencao: true))).Should().Equal(
            ("distribuicaoVagas", RetificacaoErrorCodes.OfertaComInscricaoNaoSai),
            ("taxaDeInscricao", RetificacaoErrorCodes.TaxaComInscricaoNaoMuda),
            ("taxaDeInscricao.fundamentos", RetificacaoErrorCodes.CriteriosDeIsencaoComPedidoNaoMudam));
    }
}
