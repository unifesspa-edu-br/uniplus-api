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

        causas.Select(c => c.Error.Code).Should().Equal("PrazoDeEmissao.EscolhaNaoDeclarada", "ValidadeDoDocumento.EscolhaNaoDeclarada");
    }

    [Fact]
    public void ConferirCompletude_FasesSemDatas_RecusaCadaCampo()
    {
        FaseCronograma fase = FaseParaReferenciaBuilder.Criar(false, null, null);
        DocumentoExigido documento = DocumentoComReferencias(fase.Id);

        IReadOnlyList<FieldError> causas = DatasDeReferenciaNoCronograma.ConferirCompletude([fase], [documento]);

        causas.Select(c => c.Error.Code).Should().Equal("DataDeReferenciaDoDocumento.DataDaFaseIndefinida", "DataDeReferenciaDoDocumento.DataDaFaseIndefinida");
    }

    [Fact]
    public void ConferirCompletude_SemFimDasInscricoes_RecusaCadaReferencia()
    {
        DocumentoExigido documento = new DocumentoExigidoBuilder().ComPrazoDeEmissao(new PrazoDeEmissaoBuilder().Construir())
            .ComValidade(new ValidadeDoDocumentoBuilder().Construir()).Construir();

        IReadOnlyList<FieldError> causas = DatasDeReferenciaNoCronograma.ConferirCompletude([], [documento]);

        causas.Select(c => c.Error.Code).Should().Equal("DataDeReferenciaDoDocumento.FimDasInscricoesIndefinido", "DataDeReferenciaDoDocumento.FimDasInscricoesIndefinido");
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

        IReadOnlyList<FieldError> causas = DatasDeReferenciaNoCronograma.ConferirConjunto([aceita], [pendente, resolvido]);

        causas.Should().HaveCount(2).And.OnlyContain(causa => causa.Error.Message.Contains(pendente.TipoDocumentoNome, StringComparison.Ordinal));
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
