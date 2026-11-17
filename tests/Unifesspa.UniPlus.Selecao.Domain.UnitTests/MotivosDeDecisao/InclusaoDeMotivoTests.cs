namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A inclusão de um motivo numa fase ou etapa (UNI-REQ-0285): livre no rascunho; depois da publicação, só pela
/// retificação, com justificativa e antes do início da avaliação naquele lugar (UNI-REQ-0124).
/// </summary>
public sealed class InclusaoDeMotivoTests
{
    private const string Homologacao = "HOMOLOGACAO_INSCRICOES";
    private const string Justificativa = "Retorno do INEP passou a ser exigido.";

    private static MotivoDeDecisao Desfavoravel() =>
        ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.HomologacaoDasInscricoes, "SEM_RESULTADO_INEP", SentidoDoMotivo.Desfavoravel);

    private static object CopiaEsperadaNoRascunho(MotivoDeDecisao origem, LugarDaDecisao lugar) => new
    {
        MotivoDeDecisaoOrigemId = origem.Id,
        Lugar = lugar,
        Tipo = TipoMotivoDeDecisao.HomologacaoDasInscricoes,
        Descricao = origem.Descricao,
        Subclassificacao = (string?)null,
        Sentido = SentidoDoMotivo.Desfavoravel,
        JustificativaDaInclusao = (string?)null,
    };

    [Fact(DisplayName = "No rascunho, a inclusão copia por valor o motivo do cadastro, no lugar e na versão 1, sem justificativa")]
    public void Rascunho_Inclui_CopiaPorValor()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        LugarDaDecisao lugar = ProcessoParaMotivos.LugarDaFase(processo, Homologacao);
        MotivoDeDecisao origem = Desfavoravel();

        MotivoDoProcesso copia = Incluir(processo, lugar, origem, justificativa: "ignorada no rascunho").Value!;

        copia.Should().BeEquivalentTo(CopiaEsperadaNoRascunho(origem, lugar));
        copia.Codigo.Valor.Should().Be("SEM_RESULTADO_INEP");
        copia.Vigencia.VersaoDoEditalDeEntrada.Should().Be(1);
        copia.Vigencia.VersaoDoEditalDeSaida.Should().BeNull();
    }

    [Fact(DisplayName = "A inclusão na etapa guarda a etapa como lugar")]
    public void Rascunho_NaEtapa_GuardaAEtapa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        LugarDaDecisao lugar = ProcessoParaMotivos.LugarDaEtapa(processo, "Prova objetiva");

        Incluir(processo, lugar, Desfavoravel()).Value!.Lugar.Should().Be(lugar);
    }

    [Fact(DisplayName = "A fase que o processo não tem é recusada como não encontrada")]
    public void FaseInexistente_Recusa()
    {
        LugarDaDecisao lugar = LugarDaDecisao.DaFase(Guid.CreateVersion7());

        Incluir(ProcessoParaMotivos.Rascunho(), lugar, Desfavoravel()).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.FaseOuEtapaNaoEncontrada);
    }

    [Fact(DisplayName = "A etapa que o processo não tem é recusada como não encontrada, e o id de fase não vale como etapa")]
    public void EtapaInexistente_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        Guid idDeFase = processo.CronogramaFases.First().FaseCanonicaOrigemId;

        Incluir(processo, LugarDaDecisao.DaEtapa(Guid.CreateVersion7()), Desfavoravel()).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.FaseOuEtapaNaoEncontrada);
        Incluir(processo, LugarDaDecisao.DaEtapa(idDeFase), Desfavoravel()).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.FaseOuEtapaNaoEncontrada);
    }

    [Fact(DisplayName = "O lugar inexistente é recusado antes da precondição e antes do bloqueio do processo publicado")]
    public void LugarInexistente_PrecedeAMutacaoBloqueada()
    {
        ProcessoSeletivo publicado = ProcessoParaMotivos.Publicado();
        LugarDaDecisao existente = ProcessoParaMotivos.LugarDaFase(publicado, Homologacao);

        Incluir(publicado, LugarDaDecisao.DaFase(Guid.CreateVersion7()), Desfavoravel(), Justificativa).Error!.Code
            .Should().Be(MotivoDeDecisaoErrorCodes.FaseOuEtapaNaoEncontrada);
        Incluir(publicado, existente, Desfavoravel(), Justificativa).Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    [Fact(DisplayName = "Motivo desativado não entra em nova escolha")]
    public void MotivoInativo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDeDecisao origem = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel);
        origem.Desativar();

        Incluir(processo, ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO"), origem).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.MotivoInativo);
    }

    [Fact(DisplayName = "O mesmo motivo não é escolhido duas vezes no mesmo lugar, fase ou etapa")]
    public void MesmoMotivoNoMesmoLugar_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDeDecisao origem = Desfavoravel();

        foreach (LugarDaDecisao lugar in new[] { ProcessoParaMotivos.LugarDaFase(processo, Homologacao), ProcessoParaMotivos.LugarDaEtapa(processo, "Prova objetiva") })
        {
            MotivoDoProcesso primeiro = Incluir(processo, lugar, origem).Value!;

            Incluir(processo, lugar, origem, escolhaAtual: [primeiro]).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.JaEscolhido);
        }
    }

    [Fact(DisplayName = "O mesmo motivo pode ser escolhido em mais de um lugar: na fase e nas etapas")]
    public void MesmoMotivoEmLugaresDistintos_Aceita()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        MotivoDeDecisao origem = Desfavoravel();
        MotivoDoProcesso naFase = Incluir(processo, ProcessoParaMotivos.LugarDaFase(processo, Homologacao), origem).Value!;
        MotivoDoProcesso naEtapa = Incluir(processo, ProcessoParaMotivos.LugarDaEtapa(processo, "Prova objetiva"), origem, escolhaAtual: [naFase]).Value!;

        Incluir(processo, ProcessoParaMotivos.LugarDaEtapa(processo, "Prova discursiva"), origem, escolhaAtual: [naFase, naEtapa]).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Processo publicado sem sessão de retificação não inclui motivo")]
    public void PublicadoSemSessao_ExigeRetificacao()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Publicado();

        Incluir(processo, ProcessoParaMotivos.LugarDaFase(processo, Homologacao), Desfavoravel(), Justificativa)
            .Error!.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
    }

    [Fact(DisplayName = "Na retificação, a inclusão antes do início da avaliação entra na versão seguinte à base, guarda a justificativa e avança a revisão da sessão")]
    public void Retificacao_AntesDaAvaliacao_Aceita()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.EmRetificacao();
        int revisao = processo.Rascunho!.Revisao;

        MotivoDoProcesso incluido = Incluir(processo, ProcessoParaMotivos.LugarDaFase(processo, Homologacao), Desfavoravel(), "  " + Justificativa + "  ").Value!;

        incluido.JustificativaDaInclusao.Should().Be(Justificativa);
        incluido.Vigencia.VersaoDoEditalDeEntrada.Should().Be(processo.Rascunho.NumeroVersaoBase + 1);
        processo.Rascunho.Revisao.Should().Be(revisao + 1);
    }

    [Theory(DisplayName = "Na retificação, a inclusão sem justificativa é recusada")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Retificacao_SemJustificativa_Recusa(string? justificativa)
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.EmRetificacao();

        Incluir(processo, ProcessoParaMotivos.LugarDaFase(processo, Homologacao), Desfavoravel(), justificativa)
            .Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.JustificativaObrigatoria);
    }

    [Fact(DisplayName = "Na retificação, a justificativa com caractere nulo é recusada pelo domínio, não pelo banco")]
    public void Retificacao_JustificativaComCaractereNulo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.EmRetificacao();

        Incluir(processo, ProcessoParaMotivos.LugarDaFase(processo, Homologacao), Desfavoravel(), "Norma\0nova")
            .Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.JustificativaCaractereInvalido);
    }

    [Fact(DisplayName = "Na retificação, a justificativa acima do tamanho é recusada")]
    public void Retificacao_JustificativaLonga_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.EmRetificacao();

        Incluir(processo, ProcessoParaMotivos.LugarDaFase(processo, Homologacao), Desfavoravel(), new string('j', EscolhaDosMotivos.JustificativaMaxLength + 1))
            .Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.JustificativaTamanho);
    }

    [Fact(DisplayName = "Na retificação, a inclusão na fase cuja avaliação já começou é recusada")]
    public void Retificacao_FaseIniciada_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.EmRetificacao();
        LugarDaDecisao lugar = ProcessoParaMotivos.LugarDaFase(processo, Homologacao);

        Incluir(processo, lugar, Desfavoravel(), Justificativa, iniciadas: ComAAvaliacaoIniciadaNaFase(lugar))
            .Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.AvaliacaoIniciada);
    }

    [Fact(DisplayName = "A avaliação que começou em outra fase não impede a inclusão: o início é por lugar")]
    public void Retificacao_OutraFaseIniciada_Aceita()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.EmRetificacao();
        LugarDaDecisao iniciada = ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO");

        Incluir(processo, ProcessoParaMotivos.LugarDaFase(processo, Homologacao), Desfavoravel(), Justificativa, iniciadas: ComAAvaliacaoIniciadaNaFase(iniciada))
            .IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A avaliação que começou numa etapa recusa a inclusão nela e deixa a etapa irmã livre")]
    public void Retificacao_EtapaIniciada_RecusaSoNela()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.EmRetificacao();
        LugarDaDecisao iniciada = ProcessoParaMotivos.LugarDaEtapa(processo, "Prova objetiva");
        FasesIniciadas iniciadas = new([], etapas: [new EtapaIniciada(iniciada.EtapaProcessoId!.Value, "Prova objetiva", NaVersaoVigente: true)]);

        Incluir(processo, iniciada, Desfavoravel(), Justificativa, iniciadas: iniciadas).Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.AvaliacaoIniciada);
        Incluir(processo, ProcessoParaMotivos.LugarDaEtapa(processo, "Prova discursiva"), Desfavoravel(), Justificativa, iniciadas: iniciadas).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A justificativa precede o início da avaliação na ordem das recusas")]
    public void Retificacao_JustificativaPrecedeAAvaliacaoIniciada()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.EmRetificacao();
        LugarDaDecisao lugar = ProcessoParaMotivos.LugarDaFase(processo, Homologacao);

        Incluir(processo, lugar, Desfavoravel(), justificativa: null, iniciadas: ComAAvaliacaoIniciadaNaFase(lugar))
            .Error!.Code.Should().Be(MotivoDeDecisaoErrorCodes.JustificativaObrigatoria);
    }

    [Fact(DisplayName = "No rascunho, o início da avaliação não é conferido: o processo nunca foi publicado")]
    public void Rascunho_NaoConfereOInicio()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        LugarDaDecisao lugar = ProcessoParaMotivos.LugarDaFase(processo, Homologacao);

        Incluir(processo, lugar, Desfavoravel(), iniciadas: ComAAvaliacaoIniciadaNaFase(lugar)).IsSuccess.Should().BeTrue();
    }

    private static FasesIniciadas ComAAvaliacaoIniciadaNaFase(LugarDaDecisao lugar) =>
        new([KeyValuePair.Create(lugar.FaseCanonicaOrigemId!.Value, (Guid?)null)], fasesProdutoras: [lugar.FaseCanonicaOrigemId!.Value]);

    private static Result<MotivoDoProcesso> Incluir(
        ProcessoSeletivo processo,
        LugarDaDecisao lugar,
        MotivoDeDecisao origem,
        string? justificativa = null,
        IReadOnlyCollection<MotivoDoProcesso>? escolhaAtual = null,
        FasesIniciadas? iniciadas = null) =>
        EscolhaDosMotivos.Incluir(new InclusaoDeMotivo(
            processo,
            lugar,
            escolhaAtual ?? [],
            origem,
            justificativa,
            processo.Rascunho is null ? PrecondicaoIfMatch.Ausente : PrecondicaoIfMatch.Curinga,
            iniciadas ?? FasesIniciadas.Nenhuma));
}
