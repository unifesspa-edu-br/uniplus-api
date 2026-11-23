namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDosDocumentosExigidos;

/// <summary>
/// O prazo de emissão e a validade no PUT das exigências: os três estados de cada escolha, a recusa dos códigos
/// e das combinações incoerentes, e a fase de referência conferida contra o cronograma.
/// </summary>
public sealed class DefinirDocumentosExigidosPrazoEValidadeCommandHandlerTests
{
    private const string CaminhoDoPrazo = "regrasDeComprovacao[0].documento.requisitos.prazoDeEmissao";
    private const string CaminhoDaValidade = "regrasDeComprovacao[0].documento.requisitos.validade";

    private static readonly DataDeReferenciaDoDocumentoInput NoFimDasInscricoes = new("FIM_DAS_INSCRICOES", null, null);

    private static async Task<DocumentoExigido> GravarAsync(CenarioDaExigencia cenario, ItemDocumentoExigidoDeTeste item)
    {
        Result resultado = await cenario.DefinirAsync(item);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        return cenario.Processo.DocumentosExigidos.Single();
    }

    [Fact(DisplayName = "Sem prazo de emissão nem validade no pedido, a escolha de cada um fica por fazer")]
    public async Task Handle_SemPrazoNemValidade_GravaSemEscolha()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();

        DocumentoExigido documento = await GravarAsync(cenario, cenario.ItemGeral());

        documento.PrazoDeEmissao.Should().BeNull();
        documento.Validade.Should().BeNull();
    }

    [Fact(DisplayName = "Prazo de emissão sem prazo e validade que não exige documento válido são gravados sem detalhes")]
    public async Task Handle_SemPrazoENaoExigeValidade_GravaAsEscolhas()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        ItemDocumentoExigidoDeTeste item = cenario.ItemGeral()
            .ComPrazoDeEmissao(new("SEM_PRAZO", null, null, null, null)).ComValidade(new("NAO_EXIGE_DOCUMENTO_VALIDO", null, null));

        DocumentoExigido documento = await GravarAsync(cenario, item);

        documento.PrazoDeEmissao!.Escolha.Should().Be(EscolhaDoPrazoDeEmissao.SemPrazo);
        documento.PrazoDeEmissao.Valor.Should().BeNull();
        documento.Validade!.Escolha.Should().Be(EscolhaDaValidade.NaoExigeDocumentoValido);
        documento.Validade.Referencia.Should().BeNull();
    }

    [Fact(DisplayName = "Prazo de emissão com prazo e validade que exige documento válido são gravados com os detalhes")]
    public async Task Handle_ComPrazoEExigeValidade_GravaOsDetalhes()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        ItemDocumentoExigidoDeTeste item = cenario.ItemGeral()
            .ComPrazoDeEmissao(new("COM_PRAZO", 90, "DIAS", NoFimDasInscricoes, "RECUSAR_DOCUMENTO"))
            .ComValidade(new("EXIGE_DOCUMENTO_VALIDO", new("DATA_DA_APRESENTACAO", null, null), "ACEITAR_COM_AVISO"));

        DocumentoExigido documento = await GravarAsync(cenario, item);

        documento.PrazoDeEmissao!.Valor.Should().Be(90);
        documento.PrazoDeEmissao.Unidade.Should().Be(UnidadeDoPrazoDeEmissao.Dias);
        documento.PrazoDeEmissao.Referencia!.Tipo.Should().Be(TipoDaDataDeReferencia.FimDasInscricoes);
        documento.PrazoDeEmissao.EfeitoForaDoPrazo.Should().Be(EfeitoNaAnaliseDoDocumento.RecusarDocumento);
        documento.Validade!.Referencia!.Tipo.Should().Be(TipoDaDataDeReferencia.DataDaApresentacao);
        documento.Validade.EfeitoDoVencimento.Should().Be(EfeitoNaAnaliseDoDocumento.AceitarComAviso);
    }

    [Fact(DisplayName = "Omitir o prazo de emissão e a validade no PUT apaga a escolha feita antes")]
    public async Task Handle_OmitirNoPut_ApagaAEscolha()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        await GravarAsync(cenario, cenario.ItemGeral()
            .ComPrazoDeEmissao(new("COM_PRAZO", 3, "MESES", NoFimDasInscricoes, "RECUSAR_DOCUMENTO"))
            .ComValidade(new("EXIGE_DOCUMENTO_VALIDO", NoFimDasInscricoes, "RECUSAR_DOCUMENTO")));

        DocumentoExigido documento = await GravarAsync(cenario, cenario.ItemGeral());

        documento.PrazoDeEmissao.Should().BeNull();
        documento.Validade.Should().BeNull();
    }

    [Fact(DisplayName = "Códigos desconhecidos do prazo e da validade são recusados juntos, cada um no seu campo")]
    public async Task Handle_CodigosDesconhecidos_AcumulaNosCampos()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        ItemDocumentoExigidoDeTeste item = cenario.ItemGeral()
            .ComPrazoDeEmissao(new("quinzenal", 90, "SEMANAS", new("hoje", null, null), "ignorar"))
            .ComValidade(new("talvez", null, "ignorar"));

        Result resultado = await cenario.DefinirAsync(item);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ($"{CaminhoDoPrazo}.escolha", PrazoDeEmissaoErrorCodes.EscolhaInvalida),
            ($"{CaminhoDoPrazo}.unidade", PrazoDeEmissaoErrorCodes.UnidadeInvalida),
            ($"{CaminhoDoPrazo}.referencia.tipo", DataDeReferenciaDoDocumentoErrorCodes.TipoInvalido),
            ($"{CaminhoDoPrazo}.efeitoForaDoPrazo", EfeitoNaAnaliseDoDocumentoErrorCodes.Invalido),
            ($"{CaminhoDaValidade}.escolha", ValidadeDoDocumentoErrorCodes.EscolhaInvalida),
            ($"{CaminhoDaValidade}.efeitoDoVencimento", EfeitoNaAnaliseDoDocumentoErrorCodes.Invalido));
    }

    [Fact(DisplayName = "Com prazo, valor, unidade, referência e efeito são exigidos; a falta de cada um é recusada no campo dele")]
    public async Task Handle_ComPrazoIncompleto_RecusaNoCampoDoQueFalta()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();

        Result semValor = await cenario.DefinirAsync(cenario.ItemGeral().ComPrazoDeEmissao(new("COM_PRAZO", 0, "DIAS", NoFimDasInscricoes, "RECUSAR_DOCUMENTO")));
        Result semReferencia = await cenario.DefinirAsync(cenario.ItemGeral().ComPrazoDeEmissao(new("COM_PRAZO", 5, "DIAS", null, "RECUSAR_DOCUMENTO")));

        semValor.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(($"{CaminhoDoPrazo}.valor", PrazoDeEmissaoErrorCodes.ValorInvalido));
        semReferencia.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(($"{CaminhoDoPrazo}.referencia", DataDeReferenciaDoDocumentoErrorCodes.Ausente));
    }

    [Fact(DisplayName = "Sem prazo com detalhes e validade que não exige com detalhes são recusados")]
    public async Task Handle_DetalhesSemEscolhaQueOsUse_Recusa()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        ItemDocumentoExigidoDeTeste item = cenario.ItemGeral()
            .ComPrazoDeEmissao(new("SEM_PRAZO", 90, null, null, null)).ComValidade(new("NAO_EXIGE_DOCUMENTO_VALIDO", NoFimDasInscricoes, null));

        Result resultado = await cenario.DefinirAsync(item);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            (CaminhoDoPrazo, PrazoDeEmissaoErrorCodes.DetalhesSemPrazo), (CaminhoDaValidade, ValidadeDoDocumentoErrorCodes.DetalhesSemExigencia));
    }

    [Fact(DisplayName = "A data só é aceita na data específica, e a fase só no início ou no fim de uma fase; as duas recusas vão juntas")]
    public async Task Handle_ReferenciaComDataEFaseIncoerentes_AcumulaNosCampos()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        DataDeReferenciaDoDocumentoInput referencia = new("FIM_DAS_INSCRICOES", new DateOnly(2026, 3, 1), Guid.CreateVersion7());

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral().ComPrazoDeEmissao(new("COM_PRAZO", 5, "DIAS", referencia, "RECUSAR_DOCUMENTO")));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ($"{CaminhoDoPrazo}.referencia.data", DataDeReferenciaDoDocumentoErrorCodes.DataIncoerenteComTipo),
            ($"{CaminhoDoPrazo}.referencia.faseId", DataDeReferenciaDoDocumentoErrorCodes.FaseIncoerenteComTipo));
    }

    [Fact(DisplayName = "Fase de referência do prazo ou da validade que não pertence ao cronograma do processo é recusada")]
    public async Task Handle_FaseDeReferenciaForaDoCronograma_Recusa()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        DataDeReferenciaDoDocumentoInput deOutroProcesso = new("FIM_DA_FASE", null, Guid.CreateVersion7());

        Result noPrazo = await cenario.DefinirAsync(cenario.ItemGeral().ComPrazoDeEmissao(new("COM_PRAZO", 90, "DIAS", deOutroProcesso, "RECUSAR_DOCUMENTO")));
        Result naValidade = await cenario.DefinirAsync(cenario.ItemGeral().ComValidade(new("EXIGE_DOCUMENTO_VALIDO", deOutroProcesso, "RECUSAR_DOCUMENTO")));

        noPrazo.Error!.Code.Should().Be(DataDeReferenciaDoDocumentoErrorCodes.FaseNaoPertenceAoProcesso);
        naValidade.Error!.Code.Should().Be(DataDeReferenciaDoDocumentoErrorCodes.FaseNaoPertenceAoProcesso);
    }

    [Fact(DisplayName = "Fase de referência que pertence ao cronograma do processo é aceita (contraprova)")]
    public async Task Handle_FaseDeReferenciaNoCronograma_Aceita()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        DataDeReferenciaDoDocumentoInput daFase = new("INICIO_DA_FASE", null, cenario.Fase.Id);

        DocumentoExigido documento = await GravarAsync(cenario, cenario.ItemGeral().ComValidade(new("EXIGE_DOCUMENTO_VALIDO", daFase, "RECUSAR_DOCUMENTO")));

        documento.Validade!.Referencia!.FaseId.Should().Be(cenario.Fase.Id);
    }
}
