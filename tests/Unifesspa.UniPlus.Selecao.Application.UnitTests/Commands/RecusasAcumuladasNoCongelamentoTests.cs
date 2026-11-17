namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDeRecusaNoCongelamento;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>
/// O que a conferência da configuração acumula numa resposta só e o que ela deixa de avaliar: as recusas da configuração,
/// do edital e dos motivos saem juntas, na ordem das famílias; o anexo recusado impede avaliar o período e a conformidade
/// legal; o período recusado impede avaliar a conformidade legal; o fato que o catálogo não deixa coletar impede avaliar o
/// que leria o catálogo sobre ele; o fuso ausente é defeito do ambiente e recusa sozinho.
/// </summary>
public sealed class RecusasAcumuladasNoCongelamentoTests
{
    private const string PeriodoNaoInformavel = "ProcessoSeletivo.PeriodoInscricaoNaoInformavel";
    private static readonly DateTimeOffset InicioInformado = new(2026, 2, 1, 0, 0, 0, TimeSpan.FromHours(-3));
    private static readonly DateTimeOffset FimInformado = new(2026, 2, 28, 23, 59, 59, TimeSpan.FromHours(-3));

    [Fact(DisplayName = "A configuração, o edital e os motivos recusam numa resposta só, na ordem das famílias")]
    public async Task ConfiguracaoEditalEMotivos_SaemJuntosNaOrdemDasFamilias()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.Publicar)
            .Com(DefeitoDoCongelamento.Motivos)
            .Com(DefeitoDoCongelamento.ConformidadeLegal)
            .Com(DefeitoDoCongelamento.Cronograma);

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        resposta.Errors.Select(static e => e.Error.Code).Should().ContainInOrder(
            CodigoDa(DefeitoDoCongelamento.Cronograma),
            CodigoDa(DefeitoDoCongelamento.ConformidadeLegal),
            CodigoDa(DefeitoDoCongelamento.Motivos));
        eventos.Should().BeEmpty();
        await cenario.Servicos.NadaFoiGravadoAsync();
        cenario.AfirmarQueNaoTransitou();
    }

    [Fact(DisplayName = "O anexo recusado impede avaliar o período de inscrição e a conformidade legal, que só leem o edital montado")]
    public async Task AnexoRecusado_ImpedeAvaliarOPeriodoEAConformidadeLegal()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.Publicar);
        Result controle = await PublicarComPeriodoAsync(cenario, anexos: null);
        controle.Errors.Select(static e => e.Error.Code).Should().Contain(PeriodoNaoInformavel);

        CenarioDeRecusaNoCongelamento comAnexo = Para(OperacaoQueCongela.Publicar).Com(DefeitoDoCongelamento.ConformidadeLegal);
        Result resposta = await PublicarComPeriodoAsync(comAnexo, anexos: [new AnexoSolicitadoDoEdital(Guid.CreateVersion7(), null)]);

        resposta.Errors.Select(static e => e.Error.Code).Should().Contain(CodigoDa(DefeitoDoCongelamento.Anexos))
            .And.NotContain(PeriodoNaoInformavel)
            .And.NotContain(CodigoDa(DefeitoDoCongelamento.ConformidadeLegal));
        ConformidadeLegalNaoFoiLida(comAnexo);
    }

    [Fact(DisplayName = "O período recusado impede avaliar a conformidade legal, que lê o dia de referência do edital")]
    public async Task PeriodoRecusado_ImpedeAvaliarAConformidadeLegal()
    {
        CenarioDeRecusaNoCongelamento cenario = Para(OperacaoQueCongela.Publicar).Com(DefeitoDoCongelamento.ConformidadeLegal);

        Result resposta = await PublicarComPeriodoAsync(cenario, anexos: null);

        resposta.Errors.Select(static e => e.Error.Code).Should().Contain(PeriodoNaoInformavel)
            .And.NotContain(CodigoDa(DefeitoDoCongelamento.ConformidadeLegal));
        ConformidadeLegalNaoFoiLida(cenario);
    }

    [Fact(DisplayName = "O fato coletado que o catálogo reclassificou recusa uma vez só, sem os valores, os eixos e a definição que leriam o catálogo sobre ele")]
    public async Task FatoNaoMaisDeclarado_RecusaSemAsConferenciasQueLeriamOCatalogo()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Fato reclassificado");
        processo.DefinirItens([ItemDaModalidade()]).IsSuccess.Should().BeTrue();
        ServicosDoCongelamentoDeTeste servicos = ServicosDoCongelamentoDeTeste.Para(processo)
            .ComCatalogoDeFatos(CatalogoComAModalidadeDerivada());

        (Result resposta, IEnumerable<object> eventos) = await servicos.PublicarAsync(processo.Id);

        resposta.Errors.Select(static e => e.Error.Code).Should().Equal("ProcessoSeletivo.FatoColetadoNaoMaisDeclarado");
        eventos.Should().BeEmpty();
        await servicos.NadaFoiGravadoAsync();
    }

    [Theory(DisplayName = "A proteção pendente não impede conferir a configuração: a causa da aprovação sai na frente da do identificador ausente, nas três operações")]
    [InlineData(OperacaoQueCongela.Publicar)]
    [InlineData(OperacaoQueCongela.Retificar)]
    public async Task ProtecaoPendenteEIdentificadorAusente_AcumulamNaOrdem(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao).Com(DefeitoDoCongelamento.ProtecaoPendente);
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.IdentificadorLegivel))!.SetValue(cenario.Processo, null);

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        resposta.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            (null, CodigoDa(DefeitoDoCongelamento.ProtecaoPendente)),
            ("/identificacao/identificadorLegivel", "ProcessoSeletivo.IdentificadorLegivelAusente"));
        eventos.Should().BeEmpty();
        await cenario.Servicos.NadaFoiGravadoAsync();
    }

    [Theory(DisplayName = "O fuso institucional ausente recusa com erro único do ambiente, ainda que o catálogo também recuse, sem canonicalizar nem gravar")]
    [InlineData(OperacaoQueCongela.Publicar)]
    [InlineData(OperacaoQueCongela.Retificar)]
    public async Task FusoAusente_RecusaSozinho(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao).Com(DefeitoDoCongelamento.ProtecaoPendente);
        cenario.Servicos.ComFusoDaConferencia(new ResolvedorFusoIndisponivelDeTeste());

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        resposta.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.FusoInstitucionalNaoReconhecido");
        eventos.Should().BeEmpty();
        cenario.Servicos.Canonicalizer.ReceivedCalls().Should().BeEmpty();
        await cenario.Servicos.NadaFoiGravadoAsync();
    }

    private static async Task<Result> PublicarComPeriodoAsync(CenarioDeRecusaNoCongelamento cenario, IReadOnlyList<AnexoSolicitadoDoEdital>? anexos)
    {
        (Result resposta, _) = await cenario.Servicos.PublicarAsync(new PublicarProcessoSeletivoCommand(
            cenario.Processo.Id, "001/2026", InicioInformado, FimInformado, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto(), Anexos: anexos));

        return resposta;
    }

    private static void ConformidadeLegalNaoFoiLida(CenarioDeRecusaNoCongelamento cenario) =>
        cenario.Servicos.ObrigatoriedadesLegais.ReceivedCalls().Should().BeEmpty();

    private static IFatoCandidatoReader CatalogoComAModalidadeDerivada()
    {
        FatoCandidatoView modalidade = new(
            Id: Guid.CreateVersion7(), Codigo: "MODALIDADE", Nome: "Modalidade de concorrência", Descricao: "Descrição do fato.",
            Dominio: "CATEGORICO", Origem: "DERIVADO", Cardinalidade: "MULTIVALORADO", ValoresDominio: null, PontoResolucao: "INSCRICAO",
            Binding: "REGRA_DERIVACAO:MODALIDADE", ValoresDominioDeclarados: null, FonteValores: "MODALIDADE", Ativo: true,
            ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal,
            FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);
        IFatoCandidatoReader catalogo = Substitute.For<IFatoCandidatoReader>();
        catalogo.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[modalidade]));

        return catalogo;
    }

    private static FatoColetado ItemDaModalidade() =>
        FatoColetado.Criar(
            "MODALIDADE", 0, "Modalidade", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL",
            hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade,
            descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
}
