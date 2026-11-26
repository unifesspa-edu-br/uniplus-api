namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaAplicacaoDoModeloDeFormulario;

/// <summary>
/// Os documentos exigidos que o modelo de formulário copia para o processo: a fase onde entram, a troca só
/// dos documentos do formulário do modelo, a regra padrão dos derivados que citam e a multiplicação pelas
/// opções ofertadas.
/// </summary>
public sealed class AplicarModeloFormularioDocumentosExigidosTests
{
    private readonly CenarioDaAplicacaoDoModeloDeFormulario _cenario = new();

    private static Dictionary<string, DominioDeValores> CondicoesDeAtendimentoOfertadas(params string[] opcoes) =>
        new Dictionary<string, DominioDeValores>(StringComparer.Ordinal)
        {
            ["CONDICAO_ATENDIMENTO"] = DominioDeValores.OfertadoPeloProcesso(opcoes),
        };

    private static DocumentoExigidoDoModeloInput LaudoPorOpcaoDeAtendimentoExcetoLactante() =>
        new(
            "INSCRICAO", "LAUDO_MEDICO",
            [[new CondicaoPrecondicaoInput("SOLICITA_ATENDIMENTO_ESPECIALIZADO", "IGUAL", JsonSerializer.SerializeToElement(true))]],
            new IndeferimentoDoDocumentoDoModeloInput(true, "RETIRA_O_ACRESCIMO", []),
            PorOpcao: new ExigenciaPorOpcaoInput("CONDICAO_ATENDIMENTO", ["LACTANTE"]));

    private static DocumentoExigidoDoModeloInput LaudoQueDependeDeUmFatoDerivado() =>
        new(
            "INSCRICAO", "LAUDO_MEDICO",
            [[new CondicaoPrecondicaoInput("FLAG", "IGUAL", JsonSerializer.SerializeToElement(true))]],
            new IndeferimentoDoDocumentoDoModeloInput(false, "RETIRA_O_ACRESCIMO", []));

    private static string MotivosDaRecusa(Result<AplicacaoDeModeloDto> resultado) =>
        string.Join("; ", resultado.Errors.Select(static e => $"{e.Field}: {e.Error.Code}"));

    private FaseCronograma DeclararCronogramaComDocumentoDaHabilitacao()
    {
        FaseCronograma fase = _cenario.FaseDoCronograma("INSCRICAO", "RESULTADO_FINAL");
        _cenario.DeclararAtendimentoEspecializado();
        Guid faseFinal = _cenario.Processo.CronogramaFases.Single(static f => f.Codigo == "RESULTADO_FINAL").Id;
        _cenario.Processo.DefinirFormulario(PapelDoFormularioDeTeste.Habilitacao, faseFinal, null, FormularioDeTeste.Paginas(PapelDoFormularioDeTeste.Habilitacao)).IsSuccess.Should().BeTrue();
        DocumentoExigido daHabilitacao = DocumentoExigidoDeTeste.Novo()
            .NaFase(faseFinal)
            .NoFormulario(PapelDoFormularioDeTeste.Habilitacao)
            .ComTipo("HISTORICO_ENSINO_MEDIO", "Histórico", "ESCOLARIDADE")
            .Criar().Value!;
        _cenario.Processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(daHabilitacao, 0).Value!]).IsSuccess.Should().BeTrue();
        return fase;
    }

    private void DeveTerSoOsDocumentosDaHabilitacaoEOsDaInscricao() =>
        _cenario.Processo.DocumentosExigidos.Select(static d => (d.TipoDocumentoCodigo, d.FormularioCodigo.Valor)).Should().BeEquivalentTo(
        [
            ("HISTORICO_ENSINO_MEDIO", "HABILITACAO"),
            ("LAUDO_MEDICO", "INSCRICAO"),
            ("LAUDO_MEDICO", "INSCRICAO"),
            ("CERTIDAO_NASCIMENTO", "INSCRICAO"),
        ]);

    private void LaudoCopiadoDeveValerNaFaseDaInscricao(FaseCronograma fase)
    {
        DocumentoExigido laudo = _cenario.Processo.DocumentosExigidos.First(static d => d.TipoDocumentoCodigo == "LAUDO_MEDICO");
        laudo.ExigidoNaFaseId.Should().Be(fase.Id);
        laudo.ConsequenciaDoDescumprimento.Should().Be(Unifesspa.UniPlus.Regras.Enums.ConsequenciaDoDescumprimento.RetiraOAcrescimo);
        laudo.Condicoes.Select(static c => (c.Fato, c.Clausula)).Should().BeEquivalentTo(
            [("SOLICITA_ATENDIMENTO_ESPECIALIZADO", 0), ("CONDICAO_ATENDIMENTO", 0)]);
    }

    [Fact(DisplayName = "O modelo copia o conjunto de documentos exigidos para o processo, na fase de inscrição, e a nova aplicação troca só os do formulário do modelo")]
    public async Task Handle_ModeloComDocumentos_CopiaETrocaOsDoFormulario()
    {
        FaseCronograma fase = DeclararCronogramaComDocumentoDaHabilitacao();
        ModeloFormularioView modelo = ModeloComDocumentos(
            DocumentoDoModelo("LAUDO_MEDICO", "PCD"), DocumentoDoModelo("LAUDO_MEDICO", "LACTANTE"), DocumentoDoModelo("CERTIDAO_NASCIMENTO", "LACTANTE"));

        Result<AplicacaoDeModeloDto> primeira = await _cenario.AplicarAsync(modelo);
        Result<AplicacaoDeModeloDto> segunda = await _cenario.AplicarAsync(modelo);

        primeira.IsSuccess.Should().BeTrue(MotivosDaRecusa(primeira));
        segunda.IsSuccess.Should().BeTrue();
        DeveTerSoOsDocumentosDaHabilitacaoEOsDaInscricao();
        LaudoCopiadoDeveValerNaFaseDaInscricao(fase);
    }

    [Fact]
    public async Task Handle_ExigenciaMantidaDoMesmoTipo_PreservaChaveEFormaOutra()
    {
        _cenario.FaseDoCronograma("INSCRICAO", "RESULTADO_FINAL");
        _cenario.DeclararAtendimentoEspecializado();
        DocumentoExigido mantida = ExigenciaMantida();
        _cenario.Processo.DefinirDocumentosExigidosNosFormulariosMinimos([RegraDeComprovacao.CriarRegraDeDocumento(mantida, 0).Value!]).IsSuccess.Should().BeTrue();

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(ModeloComDocumentos(DocumentoDoModelo("LAUDO_MEDICO", "PCD")));

        resultado.IsSuccess.Should().BeTrue(MotivosDaRecusa(resultado));
        _cenario.Processo.DocumentosExigidos.Select(static documento => documento.Chave.Valor)
            .Should().BeEquivalentTo("exigencia_laudo_medico", "exigencia_laudo_medico_2");
        _cenario.Processo.DocumentosExigidos.Should().Contain(documento => documento.Id == mantida.Id && documento.Chave == mantida.Chave);
    }

    private DocumentoExigido ExigenciaMantida() => new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
        .ComChave(ChaveLocal.Criar("exigencia_laudo_medico").Value)
        .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "LAUDO_MEDICO", "Laudo médico", "SAUDE", ProtecaoDoDocumentoDeTeste.Pessoal))
        .NaFase(_cenario.Processo.CronogramaFases.Single(static fase => fase.Codigo == "RESULTADO_FINAL").Id, PapelDoFormularioDeTeste.Habilitacao)
        .Construir();

    [Fact(DisplayName = "A situação pendente da base legal é copiada para o processo")]
    public async Task Handle_BaseLegalPendente_CopiaSituacao()
    {
        _cenario.FaseDoCronograma("INSCRICAO");
        _cenario.DeclararAtendimentoEspecializado();
        DocumentoExigidoDoModeloInput documento = DocumentoDoModelo("LAUDO_MEDICO", "PCD") with
        {
            Indeferimento = new IndeferimentoDoDocumentoDoModeloInput(false, "RETIRA_O_ACRESCIMO", [new BaseLegalDoModeloInput("Lei de referência", "FEDERAL", "PENDENTE", null)]),
        };

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(ModeloComDocumentos(documento));

        resultado.IsSuccess.Should().BeTrue(MotivosDaRecusa(resultado));
        _cenario.Processo.DocumentosExigidos.Single().BasesLegais.Single().Situacao.Should().Be(StatusBaseLegal.Pendente);
    }

    [Fact(DisplayName = "O derivado que só a condição de um documento do modelo cita recebe a regra padrão do catálogo")]
    public async Task Handle_DerivadoCitadoSoPeloDocumento_CopiaARegraPadrao()
    {
        _cenario.FaseDoCronograma("INSCRICAO");
        _cenario.DeclararAtendimentoEspecializado();

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(
            PapelDoFormularioDeTeste.Inscricao,
            [.. ConjuntoBasicoDaInscricao.Itens, Item("SOLICITA_ATENDIMENTO_ESPECIALIZADO", 100), Item("QUILOMBOLA", 101)],
            documentos: ConjuntoDeDocumentosDoModeloBuilder.SoDocumentos(LaudoQueDependeDeUmFatoDerivado())));

        resultado.IsSuccess.Should().BeTrue(MotivosDaRecusa(resultado));
        resultado.Value!.DerivacoesCopiadas.Should().Contain("FLAG");
    }

    [Theory(DisplayName = "O filtro de oferta poda só a cláusula que afirma valor não ofertado; a de outro operador segue para o validador")]
    [InlineData("IGUAL", 0)]
    [InlineData("DIFERENTE", 1)]
    public void ClausulasOfertadas_PodaSoAAfirmacaoDeValorNaoOfertado(string operador, int esperadas)
    {
        IReadOnlyDictionary<string, DominioDeValores> ofertados = CondicoesDeAtendimentoOfertadas("PCD");
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> quando =
            [[new CondicaoPrecondicaoInput("CONDICAO_ATENDIMENTO", operador, JsonSerializer.SerializeToElement("LACTANTE"))]];

        SelecaoDosDocumentosDoModelo.ClausulasOfertadas(quando, ofertados)!.Should().HaveCount(esperadas);
    }

    [Fact(DisplayName = "O documento multiplicado pelas opções do fato entra uma vez por opção ofertada, menos as excetuadas, e cada cópia vale só para quem declarou a opção")]
    public void PorOpcaoOfertada_UmaCopiaPorOpcaoOfertadaNaoExcetuada()
    {
        IReadOnlyDictionary<string, DominioDeValores> ofertados = CondicoesDeAtendimentoOfertadas("VISAO_MONOCULAR", "LACTANTE", "PCD");
        DocumentoExigidoDoModeloInput laudo = LaudoPorOpcaoDeAtendimentoExcetoLactante();

        List<DocumentoExigidoDoModeloInput> copias = [.. SelecaoDosDocumentosDoModelo.PorOpcaoOfertada(laudo, ofertados)];

        copias.Should().OnlyContain(static c => c.PorOpcao == null && c.Indeferimento!.Obrigatorio);
        copias.Select(static c => string.Join(" e ", c.Quando!.Single().Select(static k => $"{k.Fato}={k.Valor.GetRawText()}"))).Should().BeEquivalentTo(
        [
            "SOLICITA_ATENDIMENTO_ESPECIALIZADO=true e CONDICAO_ATENDIMENTO=\"PCD\"",
            "SOLICITA_ATENDIMENTO_ESPECIALIZADO=true e CONDICAO_ATENDIMENTO=\"VISAO_MONOCULAR\"",
        ]);
        SelecaoDosDocumentosDoModelo.PorOpcaoOfertada(laudo, new Dictionary<string, DominioDeValores>(StringComparer.Ordinal))
            .Should().BeEmpty("sem opção ofertada não há o que comprovar");
    }

    [Fact(DisplayName = "O documento do modelo numa fase que o cronograma não tem é recusado, e nada é gravado")]
    public async Task Handle_DocumentoEmFaseInexistente_RecusaSemGravar()
    {
        _cenario.FaseDoCronograma("INSCRICAO");
        _cenario.DeclararAtendimentoEspecializado();
        DocumentoExigidoDoModeloInput documentoEmFaseSemCronograma = DocumentoDoModelo("LAUDO_MEDICO", "PCD") with { FaseCodigo = "RESULTADO_FINAL" };

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(ModeloComDocumentos(documentoEmFaseSemCronograma));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "modelo.conjuntoDeDocumentos.documentosExigidos[0].faseCodigo",
            Error = new { Code = AplicacaoDeModeloErrorCodes.FaseDoDocumentoInexistente },
        });
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        _cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }
}
