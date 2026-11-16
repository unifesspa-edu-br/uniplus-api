namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaSessaoEditorialDoProcessoSeletivo;

/// <summary>
/// O motivo da sessão é mutação como qualquer outra, e o descarte sem reposição da
/// configuração congelada é <b>irrepresentável</b> — não apenas desencorajado.
/// </summary>
public sealed class ProcessoSeletivoMotivoEDescarteDaSessaoEditorialTests
{
    [Fact(DisplayName = "Alterar o motivo exige precondição e incrementa a revisão")]
    public void AlterarMotivo_ExigePrecondicaoEIncrementa()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao rascunho);

        processo.AlterarMotivoRetificacao("Novo motivo", PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be("Precondicao.Requerida");

        Result aceito = processo.AlterarMotivoRetificacao("Novo motivo", PrecondicaoIfMatch.DeTags([rascunho.ETag]));

        aceito.IsSuccess.Should().BeTrue(aceito.Error?.Message);
        rascunho.Motivo.Should().Be("Novo motivo");
        rascunho.Revisao.Should().Be(2);
    }

    [Fact(DisplayName = "Alterar o motivo sem sessão aberta recusa (RetificacaoNaoAberta)")]
    public void AlterarMotivo_SemSessao_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out _);

        Result resultado = processo.AlterarMotivoRetificacao("Motivo", PrecondicaoIfMatch.Curinga);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RascunhoRetificacao.NaoAberta");
    }

    [Fact(DisplayName = "Alterar o motivo passa pela allowlist INTEIRA — um processo fora de estado editável recusa, mesmo com a sessão aberta e a precondição correta")]
    public void AlterarMotivo_ForaDeEstadoEditavel_Recusa()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao rascunho);
        ForcarStatus(processo, StatusProcesso.Encerrado);

        Result resultado = processo.AlterarMotivoRetificacao("Novo motivo", PrecondicaoIfMatch.DeTags([rascunho.ETag]));

        resultado.IsFailure.Should().BeTrue(
            "esta rota é mutação como as outras — se ela só conferisse a precondição, a edição escaparia por uma porta que os Definir* já tinham fechado");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.MutacaoForaDeEstadoEditavel");
        rascunho.Revisao.Should().Be(1, "uma mutação recusada não move a revisão");
    }

    [Fact(DisplayName = "Descartar SEM ter reposto a configuração congelada é RECUSADO pelo próprio agregado — encerrar a sessão agora deixaria o certame servindo a configuração editada")]
    public void Descartar_SemRestauracao_Recusa()
    {
        ProcessoSeletivo processo = ComSessaoAberta(out RascunhoRetificacao rascunho);

        // O administrador editou: a configuração viva já diverge do que o edital publicou.
        processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.DeTags([rascunho.ETag]))
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DescartarRetificacao(PrecondicaoIfMatch.Curinga);

        resultado.IsFailure.Should().BeTrue(
            "um fitness test diz 'ninguém faz isso hoje'; ele não diz 'isso não pode acontecer'. O agregado precisa recusar");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.DescarteSemRestauracao");
        processo.Rascunho.Should().NotBeNull("a sessão sobrevive a um descarte recusado");
    }

    [Fact(DisplayName = "Descartar DEPOIS de repor a configuração congelada é aceito — e a sessão morre")]
    public void Descartar_ComRestauracao_Aceita()
    {
        ProcessoSeletivo processo = NovoProcessoPublicado(out VersaoConfiguracao versao);
        RascunhoRetificacao rascunho = AbrirSessao(processo, versao, "Correção").Value!;
        processo.DefinirCriteriosDesempate([], PrecondicaoIfMatch.DeTags([rascunho.ETag]))
            .IsSuccess.Should().BeTrue();

        // A reposição carimba a versão que repôs — é a prova que o descarte exige.
        processo.RestaurarConfiguracaoCongelada(versao, GrafoDoProcesso(processo))
            .IsSuccess.Should().BeTrue();
        Result resultado = processo.DescartarRetificacao(PrecondicaoIfMatch.Curinga);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Rascunho.Should().BeNull();
    }

    /// <summary>
    /// O grafo das seis dimensões <b>como o agregado as tem agora</b>. Estes testes provam a
    /// mecânica do <b>carimbo de restauração</b>, não a fidelidade da reidratação — essa é do
    /// <c>RestauradorDeConfiguracao</c>, e tem prova de round-trip byte a byte própria.
    /// </summary>
    private static GrafoConfiguracao GrafoDoProcesso(ProcessoSeletivo processo) => new(
        [.. processo.Etapas],
        processo.AtendimentoEspecializado!,
        [.. processo.DistribuicaoVagas],
        processo.BonusRegional,
        [.. processo.CriteriosDesempate],
        processo.Classificacao!,
        [.. processo.CronogramaFases],
        [.. processo.DocumentosExigidos],
        [.. processo.NosExigencia],
        processo.ReferenciaTemporalFatos,
        numeroDeOpcoesDeCurso: 1);
}
