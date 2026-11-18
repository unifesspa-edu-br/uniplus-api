namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
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
/// dos documentos da finalidade do modelo, a regra padrão dos derivados que citam e a multiplicação pelas
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
            "INSCRICAO", "LAUDO_MEDICO", true,
            [[new CondicaoPrecondicaoInput("SOLICITA_ATENDIMENTO_ESPECIALIZADO", "IGUAL", JsonSerializer.SerializeToElement(true))]], "REMOVE_VANTAGEM", [],
            new ExigenciaPorOpcaoInput("CONDICAO_ATENDIMENTO", ["LACTANTE"]));

    private static string MotivosDaRecusa(Result<AplicacaoDeModeloDto> resultado) =>
        string.Join("; ", resultado.Errors.Select(static e => $"{e.Field}: {e.Error.Code}"));

    private FaseCronograma DeclararCronogramaComDocumentoDaHabilitacao()
    {
        FaseCronograma fase = _cenario.FaseDoCronograma("INSCRICAO", "RESULTADO_FINAL");
        _cenario.DeclararAtendimentoEspecializado();
        DocumentoExigido daHabilitacao = DocumentoExigido.Criar(
            _cenario.Processo.CronogramaFases.Single(static f => f.Codigo == "RESULTADO_FINAL").Id, Guid.CreateVersion7(), "HISTORICO_ENSINO_MEDIO", "Histórico", "ESCOLARIDADE", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: true, null, [], [], null, FormatosPermitidos.Criar(true, null).Value!, null).Value!;
        _cenario.Processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(daHabilitacao, 0).Value!]).IsSuccess.Should().BeTrue();
        return fase;
    }

    private void DeveTerSoOsDocumentosDaHabilitacaoEOsDaInscricao() =>
        _cenario.Processo.DocumentosExigidos.Select(static d => (d.TipoDocumentoCodigo, d.Finalidade)).Should().BeEquivalentTo(
        [
            ("HISTORICO_ENSINO_MEDIO", (FinalidadeFormulario?)null),
            ("LAUDO_MEDICO", FinalidadeFormulario.Inscricao),
            ("LAUDO_MEDICO", FinalidadeFormulario.Inscricao),
            ("CERTIDAO_NASCIMENTO", FinalidadeFormulario.Inscricao),
        ]);

    private void LaudoCopiadoDeveValerNaFaseDaInscricao(FaseCronograma fase)
    {
        DocumentoExigido laudo = _cenario.Processo.DocumentosExigidos.First(static d => d.TipoDocumentoCodigo == "LAUDO_MEDICO");
        laudo.ExigidoNaFaseId.Should().Be(fase.Id);
        laudo.ConsequenciaIndeferimento.Should().Be("REMOVE_VANTAGEM");
        laudo.Condicoes.Select(static c => (c.Fato, c.Clausula)).Should().BeEquivalentTo(
            [("SOLICITA_ATENDIMENTO_ESPECIALIZADO", 0), ("CONDICAO_ATENDIMENTO", 0)]);
    }

    [Fact(DisplayName = "O modelo copia o conjunto de documentos exigidos para o processo, na fase de inscrição, e a nova aplicação troca só os da finalidade do modelo")]
    public async Task Handle_ModeloComDocumentos_CopiaETrocaOsDaFinalidade()
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

    [Fact(DisplayName = "O derivado que só a condição de um documento do modelo cita recebe a regra padrão do catálogo")]
    public async Task Handle_DerivadoCitadoSoPeloDocumento_CopiaARegraPadrao()
    {
        _cenario.FaseDoCronograma("INSCRICAO");
        _cenario.DeclararAtendimentoEspecializado();

        DocumentoExigidoDoModeloInput documento = new(
            "INSCRICAO", "LAUDO_MEDICO", false,
            [[new CondicaoPrecondicaoInput("FLAG", "IGUAL", JsonSerializer.SerializeToElement(true))]], "REMOVE_VANTAGEM", []);

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(
            FinalidadeFormulario.Inscricao,
            [.. ConjuntoBasicoDaInscricao.Itens, Item("SOLICITA_ATENDIMENTO_ESPECIALIZADO", 100), Item("QUILOMBOLA", 101)],
            documentos: [documento]));

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

        copias.Should().OnlyContain(static c => c.PorOpcao == null && c.Obrigatorio);
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
        _cenario.FaseDoCronograma("RESULTADO_FINAL");
        _cenario.DeclararAtendimentoEspecializado();

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(ModeloComDocumentos(DocumentoDoModelo("LAUDO_MEDICO", "PCD")));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "modelo.documentosExigidos[0].faseCodigo",
            Error = new { Code = AplicacaoDeModeloErrorCodes.FaseDoDocumentoInexistente },
        });
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        _cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }
}
