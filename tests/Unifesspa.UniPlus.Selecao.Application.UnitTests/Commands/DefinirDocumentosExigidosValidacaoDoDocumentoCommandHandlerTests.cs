namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
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
/// A validação da regra de documento antes de consultar outros cadastros.
/// </summary>
public sealed class DefinirDocumentosExigidosValidacaoDoDocumentoCommandHandlerTests
{
    [Fact(DisplayName = "ADR-0125: base legal inválida em regra aninhada é recusada antes de consultar cadastros")]
    public async Task Handle_BaseLegalInvalidaEmRegraDeDocumentoAninhada_RecusaSemConsultarReaders()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFaseSemLeitoresConfigurados();
        ItemDocumentoExigidoInput documentoInformado = cenario.ItemGeral().ComBasesLegais(new BaseLegalInput("", "FEDERAL", "RESOLVIDO", null)).Construir();

        Result resultado = await HandleAsync(cenario.Mocks, GrupoComUmaRegraDeDocumento(cenario, documentoInformado));

        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(["DocumentoExigidoBaseLegal.ReferenciaObrigatoria"]);
        await ConferirQueNenhumLeitorFoiConsultado(cenario.Mocks);
    }

    [Fact(DisplayName = "ADR-0125: aplicabilidade inválida em regra aninhada é recusada antes de consultar cadastros")]
    public async Task Handle_AplicabilidadeInvalidaEmRegraDeDocumentoAninhada_RecusaSemConsultarReaders()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFaseSemLeitoresConfigurados();
        ItemDocumentoExigidoInput documentoInformado = cenario.ItemGeral().ComAplicabilidade("INVALIDA").Construir();

        Result resultado = await HandleAsync(cenario.Mocks, GrupoComUmaRegraDeDocumento(cenario, documentoInformado));

        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(["DocumentoExigido.AplicabilidadeObrigatoria"]);
        await ConferirQueNenhumLeitorFoiConsultado(cenario.Mocks);
    }

    [Fact(DisplayName = "O formulário do documento fora do vocabulário das finalidades é recusado no campo dele, antes de consultar TipoDocumento")]
    public async Task Handle_FinalidadeForaDoVocabulario_RecusaNoCampo()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFaseSemLeitoresConfigurados();

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral().NaFinalidade("MATRICULA"));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo([("regrasDeComprovacao[0].documento.momento.finalidade", EstruturaFormularioErrorCodes.FinalidadeInvalida)]);
        await cenario.Mocks.TipoDocumentoReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
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
        CenarioDaExigencia cenario = ProcessoComUmaFaseSemLeitoresConfigurados();
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
    public async Task Handle_DistincaoEOutroNoInvalidos_AcumulaAntesDosCadastros()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFaseSemLeitoresConfigurados();
        RegraDeComprovacaoInformada documentoInformado = RegraDeComprovacaoDeTeste.DeDocumento(cenario.ItemGeral().Construir()).ComDistincao("COMPETENCIA_MENSAL").Construir();
        RegraDeComprovacaoInformada semDocumento = new("DOCUMENTO", null, null, null, null, null);

        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [documentoInformado, semDocumento]));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("regrasDeComprovacao[0].cardinalidade.distincao.dataReferencia", "RegraDeComprovacao.DataReferenciaObrigatoriaParaCalendario"),
            ("regrasDeComprovacao[1].documento", "RegraDeComprovacao.DocumentoObrigatorio"));
        await ConferirQueNenhumLeitorFoiConsultado(cenario.Mocks);
    }

    [Fact]
    public async Task Handle_RepeticaoEOutroNoInvalidos_AcumulaSemRepetirCodigo()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFaseSemLeitoresConfigurados();
        RegraDeComprovacaoInformada repetida = new("DOCUMENTO", cenario.ItemGeral().Construir(), GrupoDoFormularioParaRepeticao: "INEXISTENTE");
        RegraDeComprovacaoInformada semDocumento = new("DOCUMENTO", null, null, null, null, null);

        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [repetida, semDocumento]));

        resultado.Errors.Select(e => e.Field).Should().Equal("regrasDeComprovacao[0].grupoDoFormularioParaRepeticao", "regrasDeComprovacao[1].documento");
        resultado.Errors.Should().Contain(e => e.Error.Code == "RegraDeComprovacao.GrupoParaRepeticaoNaoDeclarado" && !e.Error.Message.Contains("INEXISTENTE", StringComparison.Ordinal));
        await ConferirQueNenhumLeitorFoiConsultado(cenario.Mocks);
    }

    [Fact]
    public async Task Handle_GruposComFasesEFinalidadesDiferentes_AcumulaComPosicao()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        ItemDocumentoExigidoInput primeira = cenario.ItemGeral().NaFinalidade("INSCRICAO").Construir();
        ItemDocumentoExigidoInput outra = primeira with { Momento = primeira.Momento! with { FaseId = Guid.CreateVersion7(), Finalidade = "ISENCAO_TAXA" } };
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
        ItemDocumentoExigidoInput outra = primeira with { Momento = primeira.Momento! with { FaseId = Guid.CreateVersion7(), Finalidade = "ISENCAO_TAXA" } };
        RegraDeComprovacaoInformada semDocumento = new("DOCUMENTO", null, null, null, null, null);

        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [semDocumento, GrupoCom(primeira, outra)]));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("regrasDeComprovacao[0].documento", "RegraDeComprovacao.DocumentoObrigatorio"),
            ("regrasDeComprovacao[1].itens", "RegraDeComprovacao.GrupoComFasesDiferentes"), ("regrasDeComprovacao[1].itens", "RegraDeComprovacao.GrupoComFormulariosDiferentes"));
    }

    [Fact]
    public async Task Handle_RepeticaoRecusadaNaoAvaliaGatilhoDependente_NaoConsultaCadastros()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFaseSemLeitoresConfigurados();
        ItemDocumentoExigidoInput item = cenario.ItemCondicional(new CondicaoGatilhoInput(0, "FATO_FICTICIO", "IGUAL", "SIM")).Construir();
        RegraDeComprovacaoInformada documentoInformado = new("DOCUMENTO", item, null, null, null, null);
        RegraDeComprovacaoInformada grupo = new("TODOS_OS_ITENS", Itens: [documentoInformado], GrupoDoFormularioParaRepeticao: "GRUPO_INEXISTENTE");

        Result resultado = await HandleAsync(cenario.Mocks, new(cenario.Processo.Id, [grupo]));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("RegraDeComprovacao.GrupoParaRepeticaoNaoDeclarado");
        await ConferirQueNenhumLeitorFoiConsultado(cenario.Mocks);
    }

    [Fact(DisplayName = "CA-08: fase de referência da idade máxima de emissão que não pertence ao processo é recusada")]
    public async Task Handle_IdadeMaximaEmissaoComFaseDeOutroProcesso_RetornaErroNomeado()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        IdadeMaximaEmissaoInput idade = new(90, "DIAS", "FIM_FASE", null, Guid.CreateVersion7());

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral().ComIdadeMaxima(idade));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("IdadeMaximaEmissao.FaseNaoPertenceAoProcesso");
    }

    /// <summary>A fase do cenário não tem início nem fim definidos: FIM_FASE não tem extremo a apontar.</summary>
    [Fact(DisplayName = "CA-08: fase de referência da idade máxima de emissão sem o extremo definido é recusada")]
    public async Task Handle_IdadeMaximaEmissaoComFaseSemExtremo_RetornaErroNomeado()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        IdadeMaximaEmissaoInput idade = new(90, "DIAS", "FIM_FASE", null, cenario.Fase.Id);

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral().ComIdadeMaxima(idade));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("IdadeMaximaEmissao.FaseExtremoAusente");
    }

    [Fact(DisplayName = "A aplicabilidade ausente é recusada com código no campo aplicabilidade.tipo, antes de consultar TipoDocumento")]
    public async Task Handle_AplicabilidadeAusente_RecusaNoCampoDoTipo()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFaseSemLeitoresConfigurados();

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral().ComAplicabilidade(null));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
            [("regrasDeComprovacao[0].documento.aplicabilidade.tipo", "DocumentoExigido.AplicabilidadeObrigatoria")]);
        await ConferirQueNenhumLeitorFoiConsultado(cenario.Mocks);
    }

    [Fact(DisplayName = "A consequência inválida e o tamanho não positivo acumulam, cada um no seu campo do pedido")]
    public async Task Handle_ConsequenciaETamanhoInvalidos_AcumulamNosCamposDoPedido()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFaseSemLeitoresConfigurados();

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral().ComConsequencia("TALVEZ").ComTamanhoMaximo(0));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("regrasDeComprovacao[0].documento.indeferimento.consequencia", "DocumentoExigido.ConsequenciaIndeferimentoInvalida"),
            ("regrasDeComprovacao[0].documento.requisitos.tamanhoMaximoBytes", "DocumentoExigido.TamanhoMaximoBytesInvalido"),
        ]);
    }

    [Fact(DisplayName = "A exigência geral com condição é recusada no campo aplicabilidade.condicoes")]
    public async Task Handle_GeralComCondicao_RecusaNoCampoDasCondicoes()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFaseSemLeitoresConfigurados();
        ItemDocumentoExigidoDeTeste item = cenario.ItemCondicional(new(0, "SEXO", "IGUAL", "\"MASCULINO\"")).ComAplicabilidade("GERAL");

        Result resultado = await cenario.DefinirAsync(item);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
            [("regrasDeComprovacao[0].documento.aplicabilidade.condicoes", "DocumentoExigido.GeralComCondicao")]);
    }

    [Fact(DisplayName = "Sem o objeto dos requisitos, os formatos permitidos ausentes são recusados com código")]
    public async Task Handle_SemRequisitos_RecusaFormatosPermitidosObrigatorio()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral().SemRequisitos());

        resultado.Error!.Code.Should().Be("FormatosPermitidos.Obrigatorio");
    }

    private static DefinirDocumentosExigidosCommand GrupoComUmaRegraDeDocumento(CenarioDaExigencia cenario, ItemDocumentoExigidoInput documentoInformado) =>
        new(cenario.Processo.Id, [RegraDeComprovacaoDeTeste.Grupo("QUANTIDADE_MINIMA_DE_ITENS", RegraDeComprovacaoDeTeste.DeDocumento(documentoInformado).Construir()).ComQuantidadeMinima(1).ComIndeferimento("ELIMINA").Construir()]);

    private static async Task ConferirQueNenhumLeitorFoiConsultado(Mocks mocks)
    {
        await mocks.TipoDocumentoReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await mocks.FatoCandidatoReader.DidNotReceiveWithAnyArgs().ListarAsync(default);
    }
}
