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
/// A validação da folha de exigência antes de qualquer leitura: base legal, aplicabilidade e finalidade inválidas em folha profunda e a âncora da idade máxima de emissão.
/// </summary>
public sealed class DefinirDocumentosExigidosValidacaoDaFolhaCommandHandlerTests
{
    [Fact(DisplayName = "ADR-0125: base legal inválida numa fase profunda da árvore recusa ANTES de consultar TipoDocumento/vocabulário de fatos")]
    public async Task Handle_BaseLegalInvalidaEmFolhaProfunda_RecusaSemConsultarReaders()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFaseSemLeitoresConfigurados();
        ItemDocumentoExigidoInput folha = cenario.ItemGeral().ComBasesLegais(new BaseLegalInput("", "FEDERAL", "RESOLVIDO", null)).Construir();

        Result resultado = await HandleAsync(cenario.Mocks, GrupoOuComUmaFolha(cenario, folha));

        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(["DocumentoExigidoBaseLegal.ReferenciaObrigatoria"]);
        await ConferirQueNenhumLeitorFoiConsultado(cenario.Mocks);
    }

    [Fact(DisplayName = "ADR-0125: aplicabilidade Nenhuma numa fase profunda da árvore recusa ANTES de consultar TipoDocumento/vocabulário de fatos")]
    public async Task Handle_AplicabilidadeInvalidaEmFolhaProfunda_RecusaSemConsultarReaders()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFaseSemLeitoresConfigurados();
        ItemDocumentoExigidoInput folha = cenario.ItemGeral().ComAplicabilidade("INVALIDA").Construir();

        Result resultado = await HandleAsync(cenario.Mocks, GrupoOuComUmaFolha(cenario, folha));

        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(["DocumentoExigido.AplicabilidadeObrigatoria"]);
        await ConferirQueNenhumLeitorFoiConsultado(cenario.Mocks);
    }

    [Fact(DisplayName = "O formulário do documento fora do vocabulário das finalidades é recusado no campo dele, antes de consultar TipoDocumento")]
    public async Task Handle_FinalidadeForaDoVocabulario_RecusaNoCampo()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFaseSemLeitoresConfigurados();

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral().NaFinalidade("MATRICULA"));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo([("momento.finalidade", EstruturaFormularioErrorCodes.FinalidadeInvalida)]);
        await cenario.Mocks.TipoDocumentoReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
    }

    [Fact(DisplayName = "ADR-0125: duas bases legais inválidas na mesma folha acumulam, com o índice prefixado ao field")]
    public async Task Handle_DuasBasesLegaisInvalidasNaMesmaFolha_AcumulaComIndicePrefixado()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        BaseLegalInput baseSemReferencia = new("", "FEDERAL", "RESOLVIDO", null);
        BaseLegalInput baseSemAbrangencia = new("Lei X", "", "RESOLVIDO", null);

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral().ComBasesLegais(baseSemReferencia, baseSemAbrangencia));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Field).Should().BeEquivalentTo(
            ["indeferimento.basesLegais[0].referencia", "indeferimento.basesLegais[1].abrangencia"]);
    }

    [Fact(DisplayName = "CA-08: âncora de fase de IdadeMaximaEmissao que não pertence ao processo é recusada")]
    public async Task Handle_IdadeMaximaEmissaoComFaseDeOutroProcesso_RetornaErroNomeado()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        IdadeMaximaEmissaoInput idade = new(90, "DIAS", "FIM_FASE", null, Guid.CreateVersion7());

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral().ComIdadeMaxima(idade));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("IdadeMaximaEmissao.FaseNaoPertenceAoProcesso");
    }

    /// <summary>A fase do cenário não tem início nem fim definidos: FIM_FASE não tem extremo a apontar.</summary>
    [Fact(DisplayName = "CA-08: âncora de fase de IdadeMaximaEmissao sem o extremo definido é recusada")]
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
            [("aplicabilidade.tipo", "DocumentoExigido.AplicabilidadeObrigatoria")]);
        await ConferirQueNenhumLeitorFoiConsultado(cenario.Mocks);
    }

    [Fact(DisplayName = "A consequência inválida e o tamanho não positivo acumulam, cada um no seu campo do pedido")]
    public async Task Handle_ConsequenciaETamanhoInvalidos_AcumulamNosCamposDoPedido()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFaseSemLeitoresConfigurados();

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral().ComConsequencia("TALVEZ").ComTamanhoMaximo(0));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("indeferimento.consequencia", "DocumentoExigido.ConsequenciaIndeferimentoInvalida"),
            ("requisitos.tamanhoMaximoBytes", "DocumentoExigido.TamanhoMaximoBytesInvalido"),
        ]);
    }

    [Fact(DisplayName = "A exigência geral com condição é recusada no campo aplicabilidade.condicoes")]
    public async Task Handle_GeralComCondicao_RecusaNoCampoDasCondicoes()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFaseSemLeitoresConfigurados();
        ItemDocumentoExigidoDeTeste item = cenario.ItemCondicional(new(0, "SEXO", "IGUAL", "\"MASCULINO\"")).ComAplicabilidade("GERAL");

        Result resultado = await cenario.DefinirAsync(item);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
            [("aplicabilidade.condicoes", "DocumentoExigido.GeralComCondicao")]);
    }

    [Fact(DisplayName = "Sem o objeto dos requisitos, os formatos permitidos ausentes são recusados com código")]
    public async Task Handle_SemRequisitos_RecusaFormatosPermitidosObrigatorio()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral().SemRequisitos());

        resultado.Error!.Code.Should().Be("FormatosPermitidos.Obrigatorio");
    }

    /// <summary>O grupo (nó raiz) é visitado antes da folha, mas não tem bases legais próprias: a travessia recursa até a violação na folha.</summary>
    private static DefinirDocumentosExigidosCommand GrupoOuComUmaFolha(CenarioDaExigencia cenario, ItemDocumentoExigidoInput folha) =>
        new(cenario.Processo.Id, [NoExigenciaDeTeste.Grupo("OU", NoExigenciaDeTeste.Folha(folha).Construir()).ComQuantidadeMinima(1).ComIndeferimento("ELIMINA").Construir()]);

    private static async Task ConferirQueNenhumLeitorFoiConsultado(Mocks mocks)
    {
        await mocks.TipoDocumentoReader.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await mocks.FatoCandidatoReader.DidNotReceiveWithAnyArgs().ListarAsync(default);
    }
}
