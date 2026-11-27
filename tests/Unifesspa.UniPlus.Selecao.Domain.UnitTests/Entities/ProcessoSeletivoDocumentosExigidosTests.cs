namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

/// <summary>
/// Cobertura de <see cref="ProcessoSeletivo.DefinirDocumentosExigidos"/>:
/// substituição integral e pertencimento da fase ao cronograma do MESMO
/// processo.
/// </summary>
public sealed class ProcessoSeletivoDocumentosExigidosTests
{
    [Theory(DisplayName = "Na fase que tem a inscrição e o pedido de isenção, o documento pertence a qualquer dos dois formulários")]
    [InlineData(PapelDoFormularioDeTeste.Inscricao)]
    [InlineData(PapelDoFormularioDeTeste.IsencaoTaxa)]
    public void DefinirDocumentosExigidos_FaseCompartilhada_AceitaOsDoisFormularios(PapelDoFormularioDeTeste papel)
    {
        FaseCronograma fase = FaseQueResponde(1, "INSCRICAO", inscricao: true, isencao: true);
        ProcessoSeletivo processo = ProcessoComFases(fase);
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Inscricao);
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.IsencaoTaxa);

        Result resultado = DefinirRegrasDeDocumento(processo, Exigencia(fase.Id, papel: papel));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Fase que não pertence ao cronograma do processo é recusada")]
    public void DefinirDocumentosExigidos_FaseDeOutroProcesso_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid faseDoPedido = Guid.Parse("f1a5e000-0000-7000-8000-00000000fa5e");

        Result resultado = DefinirRegrasDeDocumento(processo, Exigencia(faseDoPedido));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("DocumentoExigido.FaseNaoPertenceAoProcesso");
        resultado.Error.Message.Should().NotContainEquivalentOf(faseDoPedido.ToString()).And.NotContainEquivalentOf("f1a5e000");
    }

    [Fact(DisplayName = "Fase viva do cronograma do próprio processo é aceita")]
    public void DefinirDocumentosExigidos_FaseDoProprioProcesso_Aceita()
    {
        FaseCronograma fase = Fase(1, "INSCRICAO");
        ProcessoSeletivo processo = ProcessoComFases(fase);

        DocumentoExigido exigencia = Exigencia(fase.Id);
        Result resultado = DefinirRegrasDeDocumentoNosFormulariosDaFase(processo, exigencia);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().ContainSingle(d => d.Id == exigencia.Id);
        exigencia.ProcessoSeletivoId.Should().Be(processo.Id);
    }

    [Fact(DisplayName = "Coleção vazia é um estado válido — nenhuma exigência configurada")]
    public void DefinirDocumentosExigidos_ListaVazia_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirDocumentosExigidos([]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "PUT idempotente: reenviar o mesmo payload substitui a coleção por inteiro, sem duplicar")]
    public void DefinirDocumentosExigidos_ReenviarMesmoPayload_NaoDuplica()
    {
        FaseCronograma fase = Fase(1, "INSCRICAO");
        ProcessoSeletivo processo = ProcessoComFases(fase);
        ExigirDocumentos(processo, Exigencia(fase.Id));

        DocumentoExigido segundaChamada = Exigencia(fase.Id);
        Result resultado = DefinirRegrasDeDocumento(processo, segundaChamada);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().HaveCount(1);
        processo.DocumentosExigidos.Should().ContainSingle(d => d.Id == segundaChamada.Id);
    }
}
