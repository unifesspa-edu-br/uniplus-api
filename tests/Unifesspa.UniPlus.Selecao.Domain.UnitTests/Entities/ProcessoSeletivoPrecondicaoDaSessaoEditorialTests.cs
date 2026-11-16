namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaPublicacaoDoProcessoSeletivo;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaSessaoEditorialDoProcessoSeletivo;

/// <summary>
/// A precondição protege a <b>sessão inteira</b> (ADR-0110 D5), não só o motivo: o ETag
/// carrega a identidade da sessão e a revisão avança a cada mutação aceita.
/// </summary>
public sealed class ProcessoSeletivoPrecondicaoDaSessaoEditorialTests
{
    [Fact(DisplayName = "CA-03: com sessão aberta, um Definir* SEM If-Match é recusado (precondição requerida)")]
    public void Definir_ComSessao_SemPrecondicao_Recusa()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out _);

        Result resultado = processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue(
            "os seis Definir* são as rotas que de fato alteram a configuração — uma revisão que governasse só o motivo seria decorativa (D5)");
        resultado.Error!.Code.Should().Be("Precondicao.Requerida");
    }

    [Fact(DisplayName = "CA-03: com sessão aberta, um Definir* com If-Match DEFASADO é recusado (precondição falhou)")]
    public void Definir_ComSessao_PrecondicaoDefasada_Recusa()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao rascunho);
        string tagInicial = rascunho.ETag;

        // Um primeiro administrador edita e faz a revisão avançar.
        processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.DeTags([tagInicial]))
            .IsSuccess.Should().BeTrue();

        // O segundo ainda tem o tag antigo em mãos — a edição cega que a precondição impede.
        Result resultado = processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.DeTags([tagInicial]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Precondicao.Falhou");
    }

    [Fact(DisplayName = "Toda mutação aceita sob sessão INCREMENTA a revisão")]
    public void Definir_ComSessao_IncrementaRevisao()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao rascunho);
        rascunho.Revisao.Should().Be(1);

        processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.DeTags([rascunho.ETag]))
            .IsSuccess.Should().BeTrue();
        rascunho.Revisao.Should().Be(2);

        processo.DefinirBonusRegional(aplica: false, bonus: null, PrecondicaoIfMatch.DeTags([rascunho.ETag]))
            .IsSuccess.Should().BeTrue();
        rascunho.Revisao.Should().Be(3);
    }

    // Ordem duplicada é recusa de REGRA DE NEGÓCIO com a precondição correta (Story #851 §3.5:
    // lista VAZIA deixou de ser recusa — um processo sem prova é válido).
    [Fact(DisplayName = "Uma mutação RECUSADA não move a revisão — o ETag do cliente continua válido")]
    public void Definir_Recusado_NaoIncrementaRevisao()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao rascunho);

        Result resultado = processo.DefinirEtapas(
            [
                EtapaDaProvaObjetiva("Prova A", CaraterEtapa.Classificatoria, 1m),
                EtapaDaProvaObjetiva("Prova B", CaraterEtapa.Classificatoria, 1m),
            ],
            PrecondicaoIfMatch.DeTags([rascunho.ETag]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.OrdemEtapaDuplicada");
        rascunho.Revisao.Should().Be(1, "a mutação não foi aceita — mover a revisão invalidaria o ETag de quem não mudou nada");
    }

    [Fact(DisplayName = "If-Match: * casa com a sessão existente")]
    public void Definir_ComCuringa_Aceita()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out _);

        Result resultado = processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.Curinga);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Uma lista VAZIA de tags fortes não casa — é o que resta de um If-Match só com weak tags, e sai 412, não 400")]
    public void Definir_ListaVaziaDeTagsFortes_Recusa()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out _);

        Result resultado = processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.DeTags([]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Precondicao.Falhou");
    }

    [Fact(DisplayName = "CA-04 (ABA): o ETag de uma sessão encerrada NÃO valida a sessão nova, ainda que a revisão coincida")]
    public void ETag_DeSessaoAnterior_NaoValidaSessaoNova()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);
        RascunhoRetificacao primeira = AbrirSessao(processo, versao, "Primeira sessão").Value!;
        string tagDaPrimeira = primeira.ETag;
        RascunhoRetificacao segunda = ReabrirAposEncerrar(processo, versao);

        segunda.Revisao.Should().Be(primeira.Revisao, "a contagem reinicia — é justamente esta coincidência que cria o ABA");
        segunda.Id.Should().NotBe(primeira.Id);

        Result resultado = processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.DeTags([tagDaPrimeira]));

        resultado.IsFailure.Should().BeTrue("o ETag morre com a sessão que o emitiu");
        resultado.Error!.Code.Should().Be("Precondicao.Falhou");
    }

    // A sessão morre e outra nasce com a revisão reiniciada em 1 — a MESMA da antiga. Se o ETag
    // fosse só a revisão, o tag antigo validaria a sessão nova e o administrador que abandonou a
    // primeira edição sobrescreveria, às cegas, o trabalho de quem abriu a segunda.
    private static RascunhoRetificacao ReabrirAposEncerrar(ProcessoSeletivo processo, VersaoConfiguracao versao)
    {
        DescartarSessao(processo);
        return AbrirSessao(processo, versao, "Segunda sessão", "user-conta-2").Value!;
    }

    // O descarte é de outra frente; aqui só interessa o efeito dele sobre o agregado (a sessão
    // deixa de existir), por reflection porque o setter é privado.
    private static void DescartarSessao(ProcessoSeletivo processo) =>
        typeof(ProcessoSeletivo)
            .GetProperty(nameof(ProcessoSeletivo.Rascunho))!
            .SetValue(processo, null);
}
