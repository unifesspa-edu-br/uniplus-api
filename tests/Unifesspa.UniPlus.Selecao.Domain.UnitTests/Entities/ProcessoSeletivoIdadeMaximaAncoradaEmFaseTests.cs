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
/// Cobertura da idade máxima de emissão ancorada na janela de outra fase
/// (<c>IdadeMaximaEmissao.ReferenciaFaseId</c>): a fase âncora não pode ser removida nem perder o
/// extremo usado enquanto um documento vivo depende dela, salvo quando o documento sai junto.
/// </summary>
public sealed class ProcessoSeletivoIdadeMaximaAncoradaEmFaseTests
{
    private static readonly DateTimeOffset FimDeJaneiro = new(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);

    private static FaseCronograma FaseNoEditalComFim(int ordem, string codigo, DateTimeOffset fim) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(ordem, Inicio: fim.AddDays(-10), Fim: fim, FaseConcluinteCodigo: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static FaseCronograma FaseNaExecucaoComFim(
        int ordem, string codigo, DateTimeOffset? fim, Guid? faseCanonicaOrigemId = null) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            faseCanonicaOrigemId ?? Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(ordem, Inicio: null, Fim: fim, FaseConcluinteCodigo: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private static DocumentoExigido ExigenciaAncoradaNoFimDaFase(
        Guid exigidoNaFaseId, Guid referenciaFaseId, FinalidadeFormulario? finalidade = null) =>
        ExigenciaComIdade(
            exigidoNaFaseId,
            IdadeMaximaEmissao.Criar(90, UnidadeIdade.Dias, ReferenciaTipoIdadeEmissao.FimFase, null, referenciaFaseId).Value!,
            finalidade);

    /// <summary>
    /// A recusa que permanece: quem referencia a fase removida é uma exigência de OUTRA fase,
    /// que só usa a janela dela como marco de idade máxima. Apagar configuração alheia por
    /// tabela seria remover o que não é da fase.
    /// </summary>
    [Fact(DisplayName = "CA-04: remover a fase usada por documento de outra fase como âncora de idade é recusado")]
    public void DefinirCronogramaFases_RemoveFaseAncoraDeIdadeDeOutraFase_Recusa()
    {
        FaseCronograma inscricao = FaseNoEditalComFim(1, "INSCRICAO", new DateTimeOffset(2027, 3, 10, 23, 59, 0, TimeSpan.Zero));
        FaseCronograma habilitacao = Fase(2, "HABILITACAO");
        ProcessoSeletivo processo = ProcessoComFases(inscricao, habilitacao);
        ExigirFolhas(processo, ExigenciaAncoradaNoFimDaFase(habilitacao.Id, inscricao.Id, FinalidadeFormulario.Habilitacao));

        Result resultado = processo.DefinirCronogramaFases(
            [Fase(1, "HABILITACAO", habilitacao.FaseCanonicaOrigemId)], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FaseCronograma.ReferenciadaPorExigenciaViva");
    }

    [Fact(DisplayName = "Redefinir o cronograma sem exigência viva é aceito (contraprova)")]
    public void DefinirCronogramaFases_SemExigenciaViva_Aceita()
    {
        ProcessoSeletivo processo = ProcessoComFases(Fase(1, "INSCRICAO"));

        Result resultado = processo.DefinirCronogramaFases([Fase(1, "INSCRICAO")], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Remover fase usada só como âncora de IdadeMaximaEmissao (não ExigidoNaFaseId) é recusado")]
    public void DefinirCronogramaFases_RemoveFaseAncoraDeIdade_Recusa()
    {
        FaseCronograma faseExigencia = Fase(1, "INSCRICAO");
        FaseCronograma faseAncora = FaseNaExecucaoComFim(2, "ANALISE", FimDeJaneiro);
        ProcessoSeletivo processo = ProcessoComFases(faseExigencia, faseAncora);
        ExigirFolhas(processo, ExigenciaAncoradaNoFimDaFase(faseExigencia.Id, faseAncora.Id));

        // Só a Ordem 1 (faseExigencia, referenciada por ExigidoNaFaseId) é mantida, com a identidade de origem
        // explícita: recriá-la com origem nova faria a fase da exigência contar como removida também.
        Result resultado = processo.DefinirCronogramaFases(
            [Fase(1, "INSCRICAO", faseExigencia.FaseCanonicaOrigemId)], []);

        resultado.IsFailure.Should().BeTrue(
            "faseAncora não é a fase de ExigidoNaFaseId, mas é a âncora de IdadeMaximaEmissao.ReferenciaFaseId");
        resultado.Error!.Code.Should().Be("FaseCronograma.ReferenciadaPorExigenciaViva");
    }

    /// <summary>
    /// Duas fases saem na mesma redefinição, e o documento de uma delas ancora na janela da
    /// outra. Ele não é referência externa a proteger: sai junto com a própria fase, pela poda
    /// em cascata. Contá-lo recusava uma remoção que o próprio comando resolve, e o operador
    /// não tinha como sair do impasse — esvaziar a lista de documentos antes é exatamente o
    /// que a cascata existe para dispensar.
    /// </summary>
    [Fact(DisplayName = "Remover as duas fases de uma vez é aceito quando o documento ancorado sai junto")]
    public void DefinirCronogramaFases_RemoveAsDuasFases_Aceita()
    {
        FaseCronograma faseAncora = FaseNaExecucaoComFim(1, "ANALISE", FimDeJaneiro);
        FaseCronograma faseExigencia = Fase(2, "RECURSO");
        FaseCronograma faseQueFica = Fase(3, "RESULTADO");
        ProcessoSeletivo processo = ProcessoComFases(faseAncora, faseExigencia, faseQueFica);
        ExigirFolhas(processo, ExigenciaAncoradaNoFimDaFase(faseExigencia.Id, faseAncora.Id));

        // As duas primeiras saem juntas; só a terceira permanece, com a identidade preservada.
        Result resultado = processo.DefinirCronogramaFases(
            [Fase(1, "RESULTADO", faseQueFica.FaseCanonicaOrigemId)], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.DocumentosExigidos.Should().BeEmpty("o documento saiu junto com a fase em que era exigido");
    }

    [Fact(DisplayName = "Fase sobrevivente que perde o extremo usado como âncora de idade é recusado")]
    public void DefinirCronogramaFases_FaseSobreviventePerdeExtremoAncoraDeIdade_Recusa()
    {
        FaseCronograma fase = FaseNaExecucaoComFim(1, "ANALISE", FimDeJaneiro);
        ProcessoSeletivo processo = ProcessoComFases(fase);
        ExigirFolhas(processo, ExigenciaAncoradaNoFimDaFase(fase.Id, fase.Id));

        // Mesma Ordem E mesma FaseCanonicaOrigemId — a fase sobrevive via reconciliação, mas perde o Fim.
        Result resultado = processo.DefinirCronogramaFases(
            [FaseNaExecucaoComFim(1, "ANALISE", fim: null, fase.FaseCanonicaOrigemId)], []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("IdadeMaximaEmissao.FaseExtremoAusente");
    }

    [Fact(DisplayName = "Fase sobrevivente que preserva o extremo usado como âncora de idade é aceito (contraprova)")]
    public void DefinirCronogramaFases_FaseSobrevivePreservandoExtremoAncoraDeIdade_Aceita()
    {
        FaseCronograma fase = FaseNaExecucaoComFim(1, "ANALISE", FimDeJaneiro);
        ProcessoSeletivo processo = ProcessoComFases(fase);
        ExigirFolhas(processo, ExigenciaAncoradaNoFimDaFase(fase.Id, fase.Id));

        DateTimeOffset novoFim = new(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);
        Result resultado = processo.DefinirCronogramaFases(
            [FaseNaExecucaoComFim(1, "ANALISE", novoFim, fase.FaseCanonicaOrigemId)], []);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message, "o Fim continua definido — só o valor mudou, o que é uma edição legítima");
    }
}
