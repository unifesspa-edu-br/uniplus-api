namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDosDocumentosExigidos;

/// <summary>
/// A validação das regras e de suas referências a cadastros.
/// </summary>
public sealed class DefinirDocumentosExigidosValidacaoDoDocumentoCommandHandlerTests
{
    [Fact(DisplayName = "ADR-0125: base legal inválida em regra dentro de grupo é recusada e consulta os cadastros")]
    public async Task Handle_BaseLegalInvalidaEmRegraDeDocumentoDentroDeGrupo_RecusaEConsultaReaders()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        ItemDocumentoExigidoInput documentoInformado = cenario.ItemGeral().ComBasesLegais(new BaseLegalInput("", "FEDERAL", "RESOLVIDO", null)).Construir();

        Result resultado = await HandleAsync(cenario.Mocks, GrupoComUmaRegraDeDocumento(cenario, documentoInformado));

        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(["DocumentoExigidoBaseLegal.ReferenciaObrigatoria"]);
        await cenario.Mocks.TipoDocumentoReader.Received().ObterPorIdAsync(cenario.TipoDocumentoId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ADR-0125: aplicabilidade inválida em regra dentro de grupo é recusada e consulta os cadastros")]
    public async Task Handle_AplicabilidadeInvalidaEmRegraDeDocumentoDentroDeGrupo_RecusaEConsultaReaders()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        ItemDocumentoExigidoInput documentoInformado = cenario.ItemGeral().ComAplicabilidade("INVALIDA").Construir();

        Result resultado = await HandleAsync(cenario.Mocks, GrupoComUmaRegraDeDocumento(cenario, documentoInformado));

        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(["DocumentoExigido.AplicabilidadeObrigatoria"]);
        await cenario.Mocks.TipoDocumentoReader.Received().ObterPorIdAsync(cenario.TipoDocumentoId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O formulário do documento fora do vocabulário das finalidades é recusado no campo dele, e consulta TipoDocumento")]
    public async Task Handle_FinalidadeForaDoVocabulario_RecusaNoCampo()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral().NaFinalidade("MATRICULA"));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo([("regrasDeComprovacao[0].documento.momento.formularioCodigo", EstruturaFormularioErrorCodes.FinalidadeInvalida)]);
        await cenario.Mocks.TipoDocumentoReader.Received().ObterPorIdAsync(cenario.TipoDocumentoId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ADR-0125: duas bases legais inválidas na mesma regra acumulam com seus índices")]
    public async Task Handle_DuasBasesLegaisInvalidasNaMesmaRegra_AcumulaComIndicePrefixado()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        BaseLegalInput baseSemReferencia = new("", "FEDERAL", "RESOLVIDO", null);
        BaseLegalInput baseSemAbrangencia = new("Lei X", "", "RESOLVIDO", null);

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral().ComBasesLegais(baseSemReferencia, baseSemAbrangencia));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Field).Should().BeEquivalentTo(
            ["regrasDeComprovacao[0].documento.indeferimento.basesLegais[0].referencia", "regrasDeComprovacao[0].documento.indeferimento.basesLegais[1].abrangencia"]);
    }

    [Fact]
    public async Task Handle_DeclaracoesInvalidasEmNosDiferentes_AcumulaComCaminhos()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        RegraDeComprovacaoInformada semDocumento = new("DOCUMENTO");
        RegraDeComprovacaoInformada desconhecido = new("XOR");
        RegraDeComprovacaoInformada grupo = new("TODOS_OS_ITENS", Itens: [desconhecido]);
        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [semDocumento, grupo]));
        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("regrasDeComprovacao[0].documento", "RegraDeComprovacao.DocumentoObrigatorio"),
            ("regrasDeComprovacao[1].itens[0].forma", "RegraDeComprovacao.FormaInvalida"));
        await ConferirQueNenhumLeitorFoiConsultado(cenario.Mocks);
    }

    [Fact]
    public async Task Handle_DistincaoEOutroNoInvalidos_AcumulaAntesDasRecusasDosCadastros()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        RegraDeComprovacaoInformada documentoInformado = RegraDeComprovacaoDeTeste.DeDocumento(cenario.ItemGeral().Construir()).ComDistincao("COMPETENCIA_MENSAL").Construir();
        RegraDeComprovacaoInformada semDocumento = new("DOCUMENTO", null, null, null, null, null);

        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [documentoInformado, semDocumento]));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("regrasDeComprovacao[0].cardinalidade.distincao.dataReferencia", "RegraDeComprovacao.DataReferenciaObrigatoriaParaCalendario"),
            ("regrasDeComprovacao[1].documento", "RegraDeComprovacao.DocumentoObrigatorio"));
        await cenario.Mocks.TipoDocumentoReader.Received().ObterPorIdAsync(cenario.TipoDocumentoId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RepeticaoEOutroNoInvalidos_AcumulaSemRepetirCodigo()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        RegraDeComprovacaoInformada repetida = new("DOCUMENTO", cenario.ItemGeral().Construir(), GrupoDoFormularioParaRepeticao: "INEXISTENTE");
        RegraDeComprovacaoInformada semDocumento = new("DOCUMENTO", null, null, null, null, null);

        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [repetida, semDocumento]));

        resultado.Errors.Select(e => e.Field).Should().Equal("regrasDeComprovacao[0].grupoDoFormularioParaRepeticao", "regrasDeComprovacao[1].documento");
        resultado.Errors.Should().Contain(e => e.Error.Code == "RegraDeComprovacao.GrupoParaRepeticaoNaoDeclarado" && !e.Error.Message.Contains("INEXISTENTE", StringComparison.Ordinal));
        await cenario.Mocks.TipoDocumentoReader.Received().ObterPorIdAsync(cenario.TipoDocumentoId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GruposComFasesEFinalidadesDiferentes_AcumulaComPosicao()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        ItemDocumentoExigidoInput primeira = cenario.ItemGeral().NaFinalidade("INSCRICAO").Construir();
        ItemDocumentoExigidoInput outra = primeira with { Momento = primeira.Momento! with { FaseId = Guid.CreateVersion7(), FormularioCodigo = "ISENCAO_TAXA" } };
        RegraDeComprovacaoInformada grupo = GrupoCom(primeira, outra);

        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [grupo, grupo]));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("regrasDeComprovacao[0].itens", "RegraDeComprovacao.GrupoComFasesDiferentes"), ("regrasDeComprovacao[0].itens", "RegraDeComprovacao.GrupoComFormulariosDiferentes"),
            ("regrasDeComprovacao[1].itens", "RegraDeComprovacao.GrupoComFasesDiferentes"), ("regrasDeComprovacao[1].itens", "RegraDeComprovacao.GrupoComFormulariosDiferentes"));
    }

    private static RegraDeComprovacaoInformada GrupoCom(ItemDocumentoExigidoInput primeira, ItemDocumentoExigidoInput outra) =>
        new("TODOS_OS_ITENS", Itens: [new("DOCUMENTO", primeira), new("DOCUMENTO", outra)]);

    [Fact]
    public async Task Handle_DeclaracaoRecusadaEOutroGrupoIncoerente_AcumulaCausasIndependentes()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        ItemDocumentoExigidoInput primeira = cenario.ItemGeral().NaFinalidade("INSCRICAO").Construir();
        ItemDocumentoExigidoInput outra = primeira with { Momento = primeira.Momento! with { FaseId = Guid.CreateVersion7(), FormularioCodigo = "ISENCAO_TAXA" } };
        RegraDeComprovacaoInformada semDocumento = new("DOCUMENTO", null, null, null, null, null);

        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [semDocumento, GrupoCom(primeira, outra)]));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("regrasDeComprovacao[0].documento", "RegraDeComprovacao.DocumentoObrigatorio"),
            ("regrasDeComprovacao[1].itens", "RegraDeComprovacao.GrupoComFasesDiferentes"), ("regrasDeComprovacao[1].itens", "RegraDeComprovacao.GrupoComFormulariosDiferentes"));
    }

    [Fact]
    public async Task Handle_RepeticaoRecusadaNaoAvaliaGatilhoDependente_NaoConsultaFatos()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        ItemDocumentoExigidoInput item = cenario.ItemCondicional(new CondicaoGatilhoInput(0, "FATO_FICTICIO", "IGUAL", "SIM")).Construir();
        RegraDeComprovacaoInformada documentoInformado = new("DOCUMENTO", item, null, null, null, null);
        RegraDeComprovacaoInformada grupo = new("TODOS_OS_ITENS", Itens: [documentoInformado], GrupoDoFormularioParaRepeticao: "GRUPO_INEXISTENTE");

        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [grupo]));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("RegraDeComprovacao.GrupoParaRepeticaoNaoDeclarado");
        await cenario.Mocks.TipoDocumentoReader.Received().ObterPorIdAsync(cenario.TipoDocumentoId, Arg.Any<CancellationToken>());
        await cenario.Mocks.FatoCandidatoReader.DidNotReceiveWithAnyArgs().ListarAsync(default);
    }

    [Fact(DisplayName = "A aplicabilidade ausente é recusada com código no campo aplicabilidade.tipo, e consulta TipoDocumento")]
    public async Task Handle_AplicabilidadeAusente_RecusaNoCampoDoTipo()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral().ComAplicabilidade(null));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
            [("regrasDeComprovacao[0].documento.aplicabilidade.tipo", "DocumentoExigido.AplicabilidadeObrigatoria")]);
        await cenario.Mocks.TipoDocumentoReader.Received().ObterPorIdAsync(cenario.TipoDocumentoId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A consequência inválida e o tamanho não positivo acumulam, cada um no seu campo do pedido")]
    public async Task Handle_ConsequenciaETamanhoInvalidos_AcumulamNosCamposDoPedido()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral().ComConsequencia("TALVEZ").ComTamanhoMaximo(0));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("regrasDeComprovacao[0].documento.indeferimento.consequenciaDoDescumprimento", "DocumentoExigido.ConsequenciaDoDescumprimentoInvalida"),
            ("regrasDeComprovacao[0].documento.requisitos.tamanhoMaximoBytes", "DocumentoExigido.TamanhoMaximoBytesInvalido"),
        ]);
    }

    [Fact(DisplayName = "A exigência geral com condição é recusada no campo aplicabilidade.condicoes")]
    public async Task Handle_GeralComCondicao_RecusaNoCampoDasCondicoes()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        ItemDocumentoExigidoDeTeste item = cenario.ItemCondicional(new(0, "SEXO", "IGUAL", "\"MASCULINO\"")).ComAplicabilidade("GERAL");

        Result resultado = await cenario.DefinirAsync(item);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
            [("regrasDeComprovacao[0].documento.aplicabilidade.condicoes", "DocumentoExigido.GeralComCondicao"), ("regrasDeComprovacao[0].documento", "PredicadoDnf.FatoDesconhecido")]);
    }

    [Fact(DisplayName = "Sem o objeto dos requisitos, os formatos permitidos ausentes são recusados com código")]
    public async Task Handle_SemRequisitos_RecusaFormatosPermitidosObrigatorio()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral().SemRequisitos());

        resultado.Error!.Code.Should().Be("FormatosPermitidos.Obrigatorio");
    }

    [Fact]
    public async Task Handle_DoisDocumentosSemFormatos_IdentificaCadaRegra()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        ItemDocumentoExigidoInput documento = cenario.ItemGeral().SemRequisitos().Construir();
        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [new("DOCUMENTO", documento), new("DOCUMENTO", documento)]));

        resultado.Errors.Select(e => e.Field).Should().Equal("regrasDeComprovacao[0].documento.requisitos.formatosPermitidos", "regrasDeComprovacao[1].documento.requisitos.formatosPermitidos");
    }

    [Fact]
    public async Task Handle_ModeloNaoEncontrado_RecusaNoCampoDoModelo()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        ItemDocumentoExigidoInput documento = cenario.ItemGeral().Construir();
        documento = documento with { Requisitos = documento.Requisitos! with { Modelo = new(Guid.Parse("11111111-1111-4111-8111-111111111111")) } };

        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [new("DOCUMENTO", documento)]));

        resultado.Errors.Should().ContainSingle(e => e.Error.Code == "DocumentoExigido.ModeloNaoEncontrado")
            .Which.Field.Should().Be("regrasDeComprovacao[0].documento.requisitos.modelo.modeloId");
    }

    [Fact]
    public async Task Handle_RestricoesInvalidas_AcumulaNaOrdem()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        PrazoDeEmissaoInput prazoSemUnidade = new("COM_PRAZO", 1, null, null, null);
        ValidadeDoDocumentoInput validadeSemReferencia = new("EXIGE_DOCUMENTO_VALIDO", null, null);
        RequisitosDoDocumentoInput requisitos = new(null, null, prazoSemUnidade, validadeSemReferencia, new(Guid.CreateVersion7()));
        ItemDocumentoExigidoInput documento = cenario.ItemGeral().Construir() with { Requisitos = requisitos };

        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [new("DOCUMENTO", documento)]));

        resultado.Errors.Select(e => e.Error.Code).Should().Equal(
            PrazoDeEmissaoErrorCodes.UnidadeInvalida, DataDeReferenciaDoDocumentoErrorCodes.Ausente, "FormatosPermitidos.Obrigatorio", "DocumentoExigido.ModeloNaoEncontrado");
    }

    [Fact]
    public async Task Handle_QuantidadeInvalidaEFormatosAusentes_AcumulaUmaVez()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        RegraDeComprovacaoInformada regra = RegraDeComprovacaoDeTeste.DeDocumento(cenario.ItemGeral().SemRequisitos()).ComQuantidadeMinima(0).Construir();
        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [regra]));
        resultado.Errors.Select(e => e.Field).Should().Equal("regrasDeComprovacao[0].cardinalidade.quantidadeMinima", "regrasDeComprovacao[0].documento.requisitos.formatosPermitidos");
    }

    [Fact]
    public async Task Handle_TipoModeloECondicaoInvalidos_AcumulaReferencias()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFaseSemLeitoresConfigurados();
        ItemDocumentoExigidoInput item = cenario.ItemCondicional(new(0, "FATO_FICTICIO", "IGUAL", "SIM")).Construir();
        item = item with { Requisitos = item.Requisitos! with { Modelo = new(Guid.CreateVersion7()) } };
        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [new("DOCUMENTO", item)]));
        resultado.Errors.Select(e => e.Error.Code).Should().Equal("DocumentoExigido.TipoDocumentoNaoEncontrado", "PredicadoDnf.FatoDesconhecido", "DocumentoExigido.ModeloNaoEncontrado");
    }

    [Fact]
    public async Task Handle_RepeticaoRecusada_ConfereRestricoesSemLerFatos()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        ItemDocumentoExigidoInput item = cenario.ItemCondicional(new(0, "FATO_FICTICIO", "IGUAL", "SIM")).Construir();
        item = item with { Requisitos = new(null, null, null, null, new(Guid.CreateVersion7())) };
        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [new("DOCUMENTO", item, GrupoDoFormularioParaRepeticao: "INEXISTENTE")]));
        resultado.Errors.Select(e => e.Error.Code).Should().Equal("RegraDeComprovacao.GrupoParaRepeticaoNaoDeclarado", "FormatosPermitidos.Obrigatorio", "DocumentoExigido.ModeloNaoEncontrado");
        await cenario.Mocks.FatoCandidatoReader.DidNotReceiveWithAnyArgs().ListarAsync(default);
    }

    [Fact]
    public async Task Handle_TipoNoPrimeiroEQuantidadeNoSegundo_DeclaracaoVemPrimeiro()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFaseSemLeitoresConfigurados();
        ItemDocumentoExigidoInput item = cenario.ItemGeral().Construir();
        RegraDeComprovacaoInformada segunda = RegraDeComprovacaoDeTeste.DeDocumento(item).ComQuantidadeMinima(0).Construir();
        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [new("DOCUMENTO", item), segunda]));
        resultado.Errors.Select(e => e.Field).Should().Equal("regrasDeComprovacao[1].cardinalidade.quantidadeMinima", "regrasDeComprovacao[0].documento", "regrasDeComprovacao[1].documento");
        resultado.Error!.Code.Should().Be("RegraDeComprovacao.QuantidadeMinimaDeApresentacoesInvalida");
    }

    [Fact]
    public async Task Handle_ReenvioDeclaradoNaRegraDeDocumento_RecusaEAcumulaComAsDemais()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFaseSemLeitoresConfigurados();
        RegraDeComprovacaoInformada regra = RegraDeComprovacaoDeTeste.DeDocumento(cenario.ItemGeral().Construir()).ComQuantidadeMinima(0).ComReenvio().Construir();
        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [regra]));
        resultado.Errors.Select(e => e.Error.Code).Should().Contain(["RegraDeComprovacao.ReenvioProibidoEmRegraDeDocumento", "RegraDeComprovacao.QuantidadeMinimaDeApresentacoesInvalida"]);
        resultado.Errors.Single(e => e.Error.Code == "RegraDeComprovacao.ReenvioProibidoEmRegraDeDocumento").Field.Should().Be("regrasDeComprovacao[0].indeferimento.abrePrazoParaReenvio");
    }

    [Fact]
    public async Task Handle_ReferenciaEComposicaoInvalidas_ReferenciaVemPrimeiro()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        ItemDocumentoExigidoInput primeira = cenario.ItemGeral().NaFinalidade("INSCRICAO").Construir();
        ItemDocumentoExigidoInput outra = primeira with { Momento = primeira.Momento! with { FaseId = Guid.CreateVersion7() } };
        ItemDocumentoExigidoInput semFormato = cenario.ItemGeral().SemRequisitos().Construir();
        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [GrupoCom(primeira, outra), new("DOCUMENTO", semFormato)]));
        resultado.Errors.Select(e => e.Error.Code).Should().Equal("FormatosPermitidos.Obrigatorio", "RegraDeComprovacao.GrupoComFasesDiferentes");
        resultado.Error!.Code.Should().Be("FormatosPermitidos.Obrigatorio");
    }

    [Fact]
    public async Task Handle_FinalidadeInvalidaComCondicaoValida_NaoAcrescentaRecusaDeFase()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase([FatoTipoDeficiencia()]);
        ColetarFato(cenario.Processo, "TIPO_DEFICIENCIA");
        Result resultado = await cenario.DefinirAsync(cenario.ItemCondicional(new(0, "TIPO_DEFICIENCIA", "IGUAL", "\"TEA\"")).NaFinalidade("INVALIDA"));
        resultado.Errors.Select(e => e.Error.Code).Should().Equal(EstruturaFormularioErrorCodes.FinalidadeInvalida);
    }

    private static DefinirDocumentosExigidosCommand GrupoComUmaRegraDeDocumento(CenarioDaExigencia cenario, ItemDocumentoExigidoInput documentoInformado) =>
        new(cenario.Processo.Id, [RegraDeComprovacaoDeTeste.Grupo("QUANTIDADE_MINIMA_DE_ITENS", RegraDeComprovacaoDeTeste.DeDocumento(documentoInformado).Construir()).ComQuantidadeMinima(1).ComIndeferimento("ELIMINA_DO_PROCESSO").Construir()]);

    private static async Task ConferirQueNenhumLeitorFoiConsultado(Mocks mocks)
    {
        await mocks.TipoDocumentoReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await mocks.FatoCandidatoReader.DidNotReceiveWithAnyArgs().ListarAsync(default);
    }
}
