namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDosDocumentosExigidosDoProcessoSeletivo;

/// <summary>
/// Cobertura da idade máxima de emissão referenciada no fim da inscrição (<c>FIM_INSCRICAO</c>): ela não
/// usa <c>ReferenciaFaseId</c>, a referência é implícita (a fase com <c>ColetaInscricao</c>) e exige
/// que alguma fase de coleta tenha o Fim definido.
/// </summary>
public sealed class ProcessoSeletivoIdadeMaximaComReferenciaNoFimDaInscricaoTests
{
    private static readonly DateTimeOffset FimDeJaneiro = new(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);

    private static FaseCronograma FaseColetaInscricao(
        int ordem, string codigo, DateTimeOffset? fim, Guid? faseCanonicaOrigemId = null) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            faseCanonicaOrigemId ?? Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: fim, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static DocumentoExigido ExigenciaComIdadeFimInscricao(Guid exigidoNaFaseId, FinalidadeFormulario? finalidade = null) =>
        ExigenciaComIdade(
            exigidoNaFaseId,
            IdadeMaximaEmissao.Criar(90, UnidadeIdade.Dias, ReferenciaTipoIdadeEmissao.FimInscricao, null, null).Value!,
            finalidade);

    private static ProcessoSeletivo ProcessoComExigenciaDeIdadeNoFimDaInscricao(FaseCronograma fase)
    {
        ProcessoSeletivo processo = ProcessoComFases(fase);
        ExigirDocumentos(processo, ExigenciaComIdadeFimInscricao(fase.Id, FinalidadeFormulario.Inscricao));
        return processo;
    }

    [Fact(DisplayName = "FIM_INSCRICAO sem NENHUMA fase que coleta inscrição no processo é recusado")]
    public void DefinirDocumentosExigidos_FimInscricaoSemFaseDeColeta_Recusa()
    {
        FaseCronograma fase = Fase(1, "ANALISE"); // ColetaInscricao = false
        ProcessoSeletivo processo = ProcessoComFases(fase);

        Result resultado = DefinirRegrasDeDocumento(processo, ExigenciaComIdadeFimInscricao(fase.Id));

        resultado.IsFailure.Should().BeTrue("FIM_INSCRICAO não pode ser resolvido sem uma fase de coleta — não há fallback silencioso");
        resultado.Error!.Code.Should().Be("IdadeMaximaEmissao.FaseNaoPertenceAoProcesso");
    }

    [Fact(DisplayName = "FIM_INSCRICAO com fase de coleta SEM Fim definido é recusado")]
    public void DefinirDocumentosExigidos_FimInscricaoComFaseDeColetaSemFim_Recusa()
    {
        FaseCronograma fase = FaseColetaInscricao(1, "INSCRICAO", fim: null);
        ProcessoSeletivo processo = ProcessoComFases(fase);

        Result resultado = DefinirRegrasDeDocumento(processo, ExigenciaComIdadeFimInscricao(fase.Id, FinalidadeFormulario.Inscricao));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("IdadeMaximaEmissao.FaseExtremoAusente");
    }

    [Fact(DisplayName = "Contraprova: FIM_INSCRICAO com fase de coleta com Fim definido é aceito")]
    public void DefinirDocumentosExigidos_FimInscricaoComFaseDeColetaComFim_Aceita()
    {
        FaseCronograma fase = FaseColetaInscricao(1, "INSCRICAO", FimDeJaneiro);
        ProcessoSeletivo processo = ProcessoComFases(fase);

        Result resultado = DefinirRegrasDeDocumento(processo, ExigenciaComIdadeFimInscricao(fase.Id, FinalidadeFormulario.Inscricao));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Com DUAS fases de coleta, a primeira sem Fim e a segunda com Fim, FIM_INSCRICAO é aceito — a pergunta é existencial (Any), não posicional (FirstOrDefault)")]
    public void DefinirDocumentosExigidos_FimInscricaoComPrimeiraFaseDeColetaSemFimEDemaisComFim_Aceita()
    {
        DateTimeOffset fim = new(2026, 2, 28, 0, 0, 0, TimeSpan.Zero);
        FaseCronograma primeiraColeta = FaseColetaInscricao(1, "INSCRICAO_PRIMEIRA_FASE", fim: null);
        FaseCronograma segundaColeta = FaseColetaInscricao(2, "INSCRICAO_SEGUNDA_FASE", fim);
        ProcessoSeletivo processo = ProcessoComFases(primeiraColeta, segundaColeta);

        Result resultado = DefinirRegrasDeDocumento(processo, ExigenciaComIdadeFimInscricao(primeiraColeta.Id, FinalidadeFormulario.Inscricao));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message, "a segunda fase de coleta resolve FIM_INSCRICAO — escolher a primeira encontrada (sem Fim) seria um 422 falso");
    }

    [Fact(DisplayName = "Redefinir o cronograma tirando o Fim da fase de coleta com exigência FIM_INSCRICAO viva é recusado")]
    public void DefinirCronogramaFases_FaseDeColetaPerdeFimComExigenciaFimInscricaoViva_Recusa()
    {
        FaseCronograma fase = FaseColetaInscricao(1, "INSCRICAO", FimDeJaneiro);
        ProcessoSeletivo processo = ProcessoComExigenciaDeIdadeNoFimDaInscricao(fase);

        // Mesma Ordem e mesma FaseCanonicaOrigemId — a fase sobrevive via reconciliação, mas perde o Fim.
        Result resultado = processo.DefinirCronogramaFases(
            [FaseColetaInscricao(1, "INSCRICAO", fim: null, fase.FaseCanonicaOrigemId)], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("IdadeMaximaEmissao.FaseExtremoAusente");
    }

    [Fact(DisplayName = "Contraprova: redefinir o cronograma preservando o Fim da fase de coleta com exigência FIM_INSCRICAO viva é aceito")]
    public void DefinirCronogramaFases_FaseDeColetaPreservaFimComExigenciaFimInscricaoViva_Aceita()
    {
        FaseCronograma fase = FaseColetaInscricao(1, "INSCRICAO", FimDeJaneiro);
        ProcessoSeletivo processo = ProcessoComExigenciaDeIdadeNoFimDaInscricao(fase);

        DateTimeOffset novoFim = new(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);
        Result resultado = processo.DefinirCronogramaFases(
            [FaseColetaInscricao(1, "INSCRICAO", novoFim, fase.FaseCanonicaOrigemId)], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }
}
