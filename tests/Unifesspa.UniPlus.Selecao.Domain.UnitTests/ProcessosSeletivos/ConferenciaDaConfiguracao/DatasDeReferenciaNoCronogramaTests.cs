namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConferenciaDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DatasDeReferenciaNoCronogramaTests
{
    [Fact]
    public void ConferirCompletude_EscolhasAusentes_RecusaNaOrdemDosCampos()
    {
        DocumentoExigido documento = new DocumentoExigidoBuilder().Construir();

        IReadOnlyList<FieldError> causas = DatasDeReferenciaNoCronograma.ConferirCompletude([], [documento]);

        causas.Select(c => (c.Field, c.Error.Code)).Should().Equal(
            ("exigencias[0].prazoDeEmissao", "PrazoDeEmissao.EscolhaNaoDeclarada"),
            ("exigencias[0].validade", "ValidadeDoDocumento.EscolhaNaoDeclarada"));
        causas.Select(c => c.Error.Message).Should().Equal(
            $"Faça a escolha do prazo de emissão do documento '{documento.TipoDocumentoNome}'.",
            $"Faça a escolha da validade do documento '{documento.TipoDocumentoNome}'.");
    }

    [Fact]
    public void ConferirCompletude_FasesSemDatas_RecusaCadaCampo()
    {
        FaseCronograma fase = FaseParaReferenciaBuilder.Criar(false, null, null);
        DocumentoExigido documento = DocumentoComReferencias(fase.Id);

        IReadOnlyList<FieldError> causas = DatasDeReferenciaNoCronograma.ConferirCompletude([fase], [documento]);

        causas.Select(c => (c.Field, c.Error.Code)).Should().Equal(
            ("exigencias[0].prazoDeEmissao.referencia", "DataDeReferenciaDoDocumento.DataDaFaseIndefinida"),
            ("exigencias[0].validade.referencia", "DataDeReferenciaDoDocumento.DataDaFaseIndefinida"));
        causas.Select(c => c.Error.Message).Should().Equal(
            $"Defina a data da fase de referência do prazo de emissão do documento '{documento.TipoDocumentoNome}'.",
            $"Defina a data da fase de referência da validade do documento '{documento.TipoDocumentoNome}'.");
    }

    [Fact]
    public void ConferirCompletude_SemFimDasInscricoes_RecusaCadaReferencia()
    {
        DocumentoExigido documento = new DocumentoExigidoBuilder().ComPrazoDeEmissao(new PrazoDeEmissaoBuilder().Construir())
            .ComValidade(new ValidadeDoDocumentoBuilder().Construir()).Construir();

        IReadOnlyList<FieldError> causas = DatasDeReferenciaNoCronograma.ConferirCompletude([], [documento]);

        causas.Select(c => (c.Field, c.Error.Code)).Should().Equal(
            ("exigencias[0].prazoDeEmissao.referencia", "DataDeReferenciaDoDocumento.FimDasInscricoesIndefinido"),
            ("exigencias[0].validade.referencia", "DataDeReferenciaDoDocumento.FimDasInscricoesIndefinido"));
        causas.Select(c => c.Error.Message).Should().Equal(
            $"Defina o término de uma fase que coleta inscrição do prazo de emissão do documento '{documento.TipoDocumentoNome}'.",
            $"Defina o término de uma fase que coleta inscrição da validade do documento '{documento.TipoDocumentoNome}'.");
    }

    [Fact]
    public void ConferirCompletude_UmaFaseDeColetaComFim_Aceita()
    {
        FaseCronograma semFim = FaseParaReferenciaBuilder.Criar(true, null, null);
        FaseCronograma comFim = FaseParaReferenciaBuilder.Criar(true, null, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        DocumentoExigido documento = new DocumentoExigidoBuilder().ComPrazoDeEmissao(new PrazoDeEmissaoBuilder().Construir())
            .ComValidade(new ValidadeDoDocumentoBuilder().Construir()).Construir();

        DatasDeReferenciaNoCronograma.ConferirCompletude([semFim, comFim], [documento]).Should().BeEmpty();
    }

    [Fact]
    public void ConferirConjunto_FaseRecusada_ApontaSomenteODocumentoQueAUsa()
    {
        FaseCronograma aceita = FaseParaReferenciaBuilder.Criar(false, null, null);
        DocumentoExigido pendente = DocumentoComReferencias(Guid.CreateVersion7());
        DocumentoExigido resolvido = new DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "OUTRO", "Outro documento", "CAT", Unifesspa.UniPlus.Testes.Compartilhado.ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComPrazoDeEmissao(new PrazoDeEmissaoBuilder().Referenciado(new DataDeReferenciaBuilder().NoInicioDaFase(aceita.Id).Construir()).Construir())
            .ComValidade(new ValidadeDoDocumentoBuilder().NaoExige().Construir()).Construir();

        IReadOnlyList<FieldError> causas = DatasDeReferenciaNoCronograma.ConferirConjunto([aceita], [resolvido, pendente]);

        causas.Select(c => (c.Field, c.Error.Message)).Should().Equal(
            ("exigencias[1].prazoDeEmissao.referencia", $"A fase de referência do prazo de emissão do documento '{pendente.TipoDocumentoNome}' não pertence ao cronograma deste processo."),
            ("exigencias[1].validade.referencia", $"A fase de referência da validade do documento '{pendente.TipoDocumentoNome}' não pertence ao cronograma deste processo."));
    }

    [Fact]
    public void ConferirConjunto_ComCaminhosDoPedido_ApontaORequisitoDaExigenciaNoCorpo()
    {
        FaseCronograma aceita = FaseParaReferenciaBuilder.Criar(false, null, null);
        DocumentoExigido resolvido = new DocumentoExigidoBuilder()
            .ComPrazoDeEmissao(new PrazoDeEmissaoBuilder().Referenciado(new DataDeReferenciaBuilder().NoInicioDaFase(aceita.Id).Construir()).Construir())
            .ComValidade(new ValidadeDoDocumentoBuilder().NaoExige().Construir()).Construir();
        DocumentoExigido pendente = DocumentoComReferencias(Guid.CreateVersion7());

        IReadOnlyList<FieldError> causas = DatasDeReferenciaNoCronograma.ConferirConjunto(
            [aceita], [resolvido, pendente], ["regrasDeComprovacao[0].documento.requisitos", "regrasDeComprovacao[1].itens[0].documento.requisitos"]);

        causas.Select(static c => c.Field).Should().Equal(
            "regrasDeComprovacao[1].itens[0].documento.requisitos.prazoDeEmissao.referencia",
            "regrasDeComprovacao[1].itens[0].documento.requisitos.validade.referencia");
    }

    [Fact]
    public void DataNoCronograma_VariasFasesDeInscricao_UsaOMaiorTermino()
    {
        FaseCronograma primeira = FaseParaReferenciaBuilder.Criar(true, null, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        FaseCronograma ultima = FaseParaReferenciaBuilder.Criar(true, null, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        DataDeReferenciaDoDocumento referencia = new DataDeReferenciaBuilder().Construir();

        referencia.DataNoCronograma([primeira, ultima], TimeZoneInfo.Utc).Should().Be(new DateOnly(2026, 3, 1));
    }

    private static DocumentoExigido DocumentoComReferencias(Guid faseId)
    {
        DataDeReferenciaDoDocumento inicio = new DataDeReferenciaBuilder().NoInicioDaFase(faseId).Construir();
        DataDeReferenciaDoDocumento fim = new DataDeReferenciaBuilder().NoFimDaFase(faseId).Construir();
        return new DocumentoExigidoBuilder().ComPrazoDeEmissao(new PrazoDeEmissaoBuilder().Referenciado(inicio).Construir())
            .ComValidade(new ValidadeDoDocumentoBuilder().Referenciada(fim).Construir()).Construir();
    }
}
