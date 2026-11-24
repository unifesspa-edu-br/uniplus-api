namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

/// <summary>
/// Cobertura de <c>PermiteComplementacao</c> da fase quando há exigência com consequência
/// <c>PENDENCIA_REENVIO</c>: a fase não pode deixar de permitir a complementação que o reenvio exige.
/// </summary>
public sealed class ProcessoSeletivoComplementacaoDaFaseComPendenciaDeReenvioTests
{
    private static FaseCronograma FaseComComplementacao(
        int ordem, string codigo, bool permiteComplementacao, Guid? faseCanonicaOrigemId = null) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            faseCanonicaOrigemId ?? Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: permiteComplementacao, ColetaInscricao: false,
            ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static ProcessoSeletivo ProcessoComExigencia(FaseCronograma fase, DocumentoExigido exigencia)
    {
        ProcessoSeletivo processo = ProcessoComFases(fase);
        ExigirDocumentos(processo, exigencia);
        return processo;
    }

    private static Result RedefinirComplementacao(ProcessoSeletivo processo, FaseCronograma fase, bool permiteComplementacao) =>
        processo.DefinirCronogramaFases(
            [FaseComComplementacao(1, "ENVIO_DOCUMENTOS", permiteComplementacao, fase.FaseCanonicaOrigemId)],
            []);

    private static DocumentoExigido ExigenciaComPendenciaDeReenvio(Guid faseId) =>
        Exigencia(faseId, Aplicabilidade.Geral, consequenciaIndeferimento: "PENDENCIA_REENVIO");

    [Fact(DisplayName = "CA-04: retirar PermiteComplementacao de fase referenciada por exigência PENDENCIA_REENVIO é recusado")]
    public void DefinirCronogramaFases_RetiraComplementacaoComPendenciaReenvio_Recusa()
    {
        FaseCronograma fase = FaseComComplementacao(1, "ENVIO_DOCUMENTOS", permiteComplementacao: true);
        ProcessoSeletivo processo = ProcessoComExigencia(fase, ExigenciaComPendenciaDeReenvio(fase.Id));

        Result resultado = RedefinirComplementacao(processo, fase, permiteComplementacao: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.PendenciaReenvioExigeComplementacao");
        resultado.Error.Message.Should().Be($"A fase '{fase.Codigo}' (ordem {fase.Ordem}) precisa permitir complementação, pois uma regra de comprovação dessa fase prevê reenvio.");
    }

    [Fact(DisplayName = "Retirar PermiteComplementacao sem exigência PENDENCIA_REENVIO é aceito (contraprova)")]
    public void DefinirCronogramaFases_RetiraComplementacaoSemPendenciaReenvio_Aceita()
    {
        FaseCronograma fase = FaseComComplementacao(1, "ENVIO_DOCUMENTOS", permiteComplementacao: true);
        ProcessoSeletivo processo = ProcessoComExigencia(fase, Exigencia(fase.Id));

        Result resultado = RedefinirComplementacao(processo, fase, permiteComplementacao: false);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Manter PermiteComplementacao verdadeiro com exigência PENDENCIA_REENVIO é aceito (contraprova)")]
    public void DefinirCronogramaFases_MantemComplementacaoComPendenciaReenvio_Aceita()
    {
        FaseCronograma fase = FaseComComplementacao(1, "ENVIO_DOCUMENTOS", permiteComplementacao: true);
        ProcessoSeletivo processo = ProcessoComExigencia(fase, ExigenciaComPendenciaDeReenvio(fase.Id));

        Result resultado = RedefinirComplementacao(processo, fase, permiteComplementacao: true);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }
}
