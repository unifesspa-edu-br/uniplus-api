namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;

using AwesomeAssertions;

using Microsoft.Extensions.Time.Testing;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal sealed class PublicacaoDeDocumentoBuilder
{
    private readonly ProcessoSeletivo _processo = ProcessoConformeFactory.Criar();
    private readonly List<DocumentoExigido> _documentos = [];

    internal PublicacaoDeDocumentoBuilder SemDatas()
    {
        FaseCronograma fase = _processo.CronogramaFases.Single();
        FaseCronograma semDatas = FaseCronograma.Reidratar(fase.Chave, fase.Id,
            new(fase.FaseCanonicaOrigemId, fase.Codigo, fase.OrigemData, fase.AgrupaEtapas, fase.PermiteComplementacao, fase.ColetaInscricao, fase.ColetaSolicitacaoIsencao),
            new(fase.Ordem, null, null, fase.UnidadeResponsavel),
            new([.. fase.Produtos], fase.EmiteParecerIndividual, fase.RegraRecurso, null, fase.RegraDeHomologacao), [.. fase.BancasRequeridas]);
        _processo.DefinirCronogramaFases([semDatas], []).IsSuccess.Should().BeTrue();
        return this;
    }

    internal PublicacaoDeDocumentoBuilder ComDocumento(string caso)
    {
        Guid faseId = _processo.CronogramaFases.Single().Id;
        DocumentoExigidoBuilder documento = new DocumentoExigidoBuilder().NaFase(faseId, PapelDoFormularioDeTeste.Inscricao);
        if (caso != "escolhas")
        {
            DataDeReferenciaDoDocumento referencia = caso == "fase" ? new DataDeReferenciaBuilder().NoInicioDaFase(faseId).Construir() : new DataDeReferenciaBuilder().Construir();
            documento.ComPrazoDeEmissao(new PrazoDeEmissaoBuilder().Referenciado(referencia).Construir());
            documento.ComValidade(new ValidadeDoDocumentoBuilder().Referenciada(referencia).Construir());
        }
        _documentos.Add(documento.Construir());
        return this;
    }

    /// <summary>Uma exigência de outro tipo com a escolha feita e sem usar data alguma do cronograma.</summary>
    internal PublicacaoDeDocumentoBuilder ComDocumentoCompletoSemReferencia()
    {
        _documentos.Add(new DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "OUTRO", "Outro documento", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal))
            .NaFase(_processo.CronogramaFases.Single().Id, PapelDoFormularioDeTeste.Inscricao)
            .ComPrazoDeEmissao(new PrazoDeEmissaoBuilder().SemPrazo().Construir())
            .ComValidade(new ValidadeDoDocumentoBuilder().NaoExige().Construir())
            .Construir());
        return this;
    }

    internal Result<VersaoConfiguracao> Congelar()
    {
        ApoioDosDocumentosExigidosDoProcessoSeletivo.ExigirDocumentos(_processo, [.. _documentos]);
        return _processo.Publicar(ApoioDaPublicacaoDoProcessoSeletivo.CenarioDoEdital() with
        {
            Relogio = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)),
        });
    }
}
