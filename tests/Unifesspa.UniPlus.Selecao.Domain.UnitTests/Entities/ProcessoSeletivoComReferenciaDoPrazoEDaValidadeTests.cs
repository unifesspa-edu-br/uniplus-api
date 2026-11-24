namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

/// <summary>
/// A fase que o prazo de emissão ou a validade de uma exigência usa como referência: deve pertencer ao cronograma
/// ao gravar a exigência, e não pode ser removida enquanto outra exigência depende dela.
/// </summary>
public sealed class ProcessoSeletivoComReferenciaDoPrazoEDaValidadeTests
{
    private static FaseCronograma FaseComFim(int ordem, string codigo, DateTimeOffset? fim, Guid? faseCanonicaOrigemId = null) => FaseCronograma.Criar(
        ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            faseCanonicaOrigemId ?? Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
            ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: fim, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static PrazoDeEmissao PrazoNoFimDaFase(Guid faseId) =>
        new PrazoDeEmissaoBuilder().Referenciado(new DataDeReferenciaBuilder().NoFimDaFase(faseId).Construir()).Construir();

    private static ValidadeDoDocumento ValidadeNoInicioDaFase(Guid faseId) =>
        new ValidadeDoDocumentoBuilder().Referenciada(new DataDeReferenciaBuilder().NoInicioDaFase(faseId).Construir()).Construir();

    [Fact(DisplayName = "Gravar exigência cujo prazo de emissão referencia fase fora do cronograma é recusado")]
    public void DefinirDocumentosExigidos_PrazoComFaseForaDoCronograma_Recusa()
    {
        FaseCronograma fase = Fase(1, "INSCRICAO");
        ProcessoSeletivo processo = ProcessoComFases(fase);

        Result resultado = DefinirRegrasDeDocumento(processo, ExigenciaComPrazoEValidade(fase.Id, PrazoNoFimDaFase(Guid.CreateVersion7()), null));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(DataDeReferenciaDoDocumentoErrorCodes.FaseNaoPertenceAoProcesso);
        resultado.Error.Message.Should().Contain("do prazo de emissão");
    }

    [Fact(DisplayName = "Gravar exigência cuja validade referencia fase fora do cronograma é recusado")]
    public void DefinirDocumentosExigidos_ValidadeComFaseForaDoCronograma_Recusa()
    {
        FaseCronograma fase = Fase(1, "INSCRICAO");
        ProcessoSeletivo processo = ProcessoComFases(fase);

        Result resultado = DefinirRegrasDeDocumento(processo, ExigenciaComPrazoEValidade(fase.Id, null, ValidadeNoInicioDaFase(Guid.CreateVersion7())));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(DataDeReferenciaDoDocumentoErrorCodes.FaseNaoPertenceAoProcesso);
        resultado.Error.Message.Should().Contain("da validade");
    }

    [Fact(DisplayName = "Gravar exigência com prazo e validade fora do cronograma recusa os dois campos juntos")]
    public void DefinirDocumentosExigidos_PrazoEValidadeComFaseForaDoCronograma_AcumulaAsRecusas()
    {
        FaseCronograma fase = Fase(1, "INSCRICAO");
        ProcessoSeletivo processo = ProcessoComFases(fase);
        DocumentoExigido exigencia = ExigenciaComPrazoEValidade(
            fase.Id, PrazoNoFimDaFase(Guid.CreateVersion7()), ValidadeNoInicioDaFase(Guid.CreateVersion7()));

        Result resultado = DefinirRegrasDeDocumento(processo, exigencia);

        resultado.Errors.Select(static erro => erro.Error.Code).Should().Equal(
            DataDeReferenciaDoDocumentoErrorCodes.FaseNaoPertenceAoProcesso, DataDeReferenciaDoDocumentoErrorCodes.FaseNaoPertenceAoProcesso);
    }

    [Fact(DisplayName = "Gravar exigência cujo prazo e validade referenciam fases do cronograma é aceito (contraprova)")]
    public void DefinirDocumentosExigidos_ReferenciasEmFasesDoCronograma_Aceita()
    {
        FaseCronograma exigida = Fase(1, "INSCRICAO");
        FaseCronograma referenciada = Fase(2, "ANALISE");
        ProcessoSeletivo processo = ProcessoComFases(exigida, referenciada);

        Result resultado = DefinirRegrasDeDocumento(
            processo, ExigenciaComPrazoEValidade(exigida.Id, PrazoNoFimDaFase(referenciada.Id), ValidadeNoInicioDaFase(referenciada.Id)));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Gravar exigência com referência no fim das inscrições é aceito sem fase que colete inscrição; a falta é pendência de publicação")]
    public void DefinirDocumentosExigidos_FimDasInscricoesSemFaseDeColeta_Aceita()
    {
        FaseCronograma fase = Fase(1, "ANALISE");
        ProcessoSeletivo processo = ProcessoComFases(fase);

        Result resultado = DefinirRegrasDeDocumento(processo, ExigenciaComPrazoEValidade(fase.Id, new PrazoDeEmissaoBuilder().Construir(), null));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Remover fase usada como referência do prazo de emissão por exigência de outra fase é recusado")]
    public void DefinirCronogramaFases_RemoveFaseDeReferenciaDoPrazo_Recusa()
    {
        FaseCronograma exigida = Fase(1, "INSCRICAO");
        FaseCronograma referenciada = Fase(2, "ANALISE");
        ProcessoSeletivo processo = ProcessoComFases(exigida, referenciada);
        ExigirDocumentos(processo, ExigenciaComPrazoEValidade(exigida.Id, PrazoNoFimDaFase(referenciada.Id), null));

        Result resultado = processo.DefinirCronogramaFases([Fase(1, "INSCRICAO", exigida.FaseCanonicaOrigemId)], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.ReferenciadaPorExigenciaViva");
        resultado.Error.Message.Should().Contain("referência do prazo de emissão").And.Contain("Grave primeiro a troca da referência");
    }

    [Fact(DisplayName = "Remover fase usada como referência da validade por exigência de outra fase é recusado")]
    public void DefinirCronogramaFases_RemoveFaseDeReferenciaDaValidade_Recusa()
    {
        FaseCronograma exigida = Fase(1, "INSCRICAO");
        FaseCronograma referenciada = Fase(2, "ANALISE");
        ProcessoSeletivo processo = ProcessoComFases(exigida, referenciada);
        ExigirDocumentos(processo, ExigenciaComPrazoEValidade(exigida.Id, null, ValidadeNoInicioDaFase(referenciada.Id)));

        Result resultado = processo.DefinirCronogramaFases([Fase(1, "INSCRICAO", exigida.FaseCanonicaOrigemId)], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.ReferenciadaPorExigenciaViva");
        resultado.Error.Message.Should().Contain("referência da validade");
    }

    [Fact(DisplayName = "Remover a fase de referência e a fase da exigência juntas é aceito: a exigência sai com a fase")]
    public void DefinirCronogramaFases_RemoveAsDuasFases_Aceita()
    {
        FaseCronograma referenciada = Fase(1, "ANALISE");
        FaseCronograma exigida = Fase(2, "RECURSO");
        FaseCronograma queFica = Fase(3, "RESULTADO");
        ProcessoSeletivo processo = ProcessoComFases(referenciada, exigida, queFica);
        ExigirDocumentos(processo, ExigenciaComPrazoEValidade(exigida.Id, PrazoNoFimDaFase(referenciada.Id), null));

        Result resultado = processo.DefinirCronogramaFases([Fase(1, "RESULTADO", queFica.FaseCanonicaOrigemId)], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Redefinir o cronograma e a fase de referência perder a data é aceito: a falta é pendência de publicação")]
    public void DefinirCronogramaFases_FaseDeReferenciaPerdeAData_Aceita()
    {
        FaseCronograma fase = FaseComFim(1, "ANALISE", new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero));
        ProcessoSeletivo processo = ProcessoComFases(fase);
        ExigirDocumentos(processo, ExigenciaComPrazoEValidade(fase.Id, PrazoNoFimDaFase(fase.Id), null));

        Result resultado = processo.DefinirCronogramaFases([FaseComFim(1, "ANALISE", fim: null, fase.FaseCanonicaOrigemId)], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }
}
