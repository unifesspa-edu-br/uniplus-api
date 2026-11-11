namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O portão de cobertura dos motivos (UNI-REQ-0123), a recópia ao congelar (UNI-REQ-0122) e o
/// início da avaliação de cada decisão.
/// </summary>
public sealed class CoberturaEInicioDaAvaliacaoTests
{
    [Fact(DisplayName = "Processo que não cobra taxa não exige motivo de isenção")]
    public void SemTaxa_Cobertura_Aceita()
    {
        CoberturaDosMotivos.Conferir(ProcessoParaMotivos.Rascunho(), []).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Cada fundamento habilitado exige um motivo favorável e um desfavorável, citando o que falta")]
    public void FundamentoSemCobertura_Recusa()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico, FundamentoIsencaoCodigo.DoacaoMedulaOssea);
        MotivoDoProcesso[] escolhidos =
        [
            Escolher(processo, ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel, "NIS_LOCALIZADO")),
            Escolher(processo, ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Desfavoravel, "NIS_NAO_LOCALIZADO")),
            Escolher(processo, ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.DoacaoMedulaOssea, SentidoDoMotivo.Favoravel, "DOACAO_CONFIRMADA")),
        ];

        Result resultado = CoberturaDosMotivos.Conferir(processo, escolhidos);

        FieldError falta = resultado.Errors.Should().ContainSingle().Subject;
        falta.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.CoberturaInsuficiente);
        falta.Error.Message.Should().Contain("desfavorável").And.Contain(FundamentoIsencaoCodigo.DoacaoMedulaOssea);
    }

    [Fact(DisplayName = "Motivo de outro tipo ou de outro fundamento não cobre o fundamento")]
    public void MotivoDeOutroTipo_NaoCobre()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico);
        MotivoDoProcesso[] escolhidos =
        [
            Escolher(processo, ProcessoParaMotivos.Motivo(TipoMotivoDeDecisao.HomologacaoDasInscricoes, "DOCUMENTO_ILEGIVEL", SentidoDoMotivo.Favoravel)),
            Escolher(processo, ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CarenciaSocioeconomica, SentidoDoMotivo.Desfavoravel)),
        ];

        CoberturaDosMotivos.Conferir(processo, escolhidos).Errors.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Com os dois sentidos em cada fundamento, a cobertura passa")]
    public void CoberturaCompleta_Aceita()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho(FundamentoIsencaoCodigo.CadastroUnico);
        MotivoDoProcesso[] escolhidos =
        [
            Escolher(processo, ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel, "NIS_LOCALIZADO")),
            Escolher(processo, ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Desfavoravel, "NIS_NAO_LOCALIZADO")),
        ];

        CoberturaDosMotivos.Conferir(processo, escolhidos).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Ao congelar, a escolha pendente de motivo desativado é recusada até a troca")]
    public void Recopiar_PendenteDesativado_Recusa()
    {
        MotivoDeDecisao origem = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel);
        MotivoDoProcesso escolhido = Escolher(ProcessoParaMotivos.Rascunho(), origem);
        MotivoDoProcesso semOrigem = Escolher(ProcessoParaMotivos.Rascunho(), origem);
        origem.Desativar();

        Result resultado = MotivosAoCongelar.Recopiar([new MotivoEscolhidoComOrigem(escolhido, origem), new MotivoEscolhidoComOrigem(semOrigem, null)]);

        resultado.Errors.Should().HaveCount(2).And.OnlyContain(e => e.Error.Code == MotivoDeDecisaoErrorCodes.MotivoDesativado);
    }

    [Fact(DisplayName = "Ao congelar, a escolha pendente recopia a descrição vigente e congela")]
    public void Recopiar_Pendente_CopiaADescricaoVigenteECongela()
    {
        MotivoDeDecisao origem = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel);
        MotivoDoProcesso escolhido = Escolher(ProcessoParaMotivos.Rascunho(), origem);
        origem.Atualizar("Redação vigente na publicação.");

        MotivosAoCongelar.Recopiar([new MotivoEscolhidoComOrigem(escolhido, origem)]).IsSuccess.Should().BeTrue();
        MotivosAoCongelar.Congelar([escolhido]);

        escolhido.Descricao.Should().Be("Redação vigente na publicação.");
        escolhido.Congelado.Should().BeTrue();
    }

    [Fact(DisplayName = "No processo já publicado, o motivo desativado depois continua utilizável")]
    public void Recopiar_CongeladoDesativado_Aceita()
    {
        MotivoDeDecisao origem = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel);
        MotivoDoProcesso escolhido = Escolher(ProcessoParaMotivos.Rascunho(), origem);
        escolhido.Congelar();
        origem.Desativar();

        MotivosAoCongelar.Recopiar([new MotivoEscolhidoComOrigem(escolhido, origem)]).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "O início da avaliação é o que vier primeiro entre a janela da fase e o primeiro registro")]
    public void InicioDaAvaliacao_OPrimeiroEntreJanelaERegistro()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        DateTimeOffset registroAntes = ProcessoParaMotivos.InicioDaHomologacao.AddDays(-3);

        InicioDaAvaliacao.Instante(processo, TipoMotivoDeDecisao.HomologacaoDasInscricoes, null).Should().Be(ProcessoParaMotivos.InicioDaHomologacao);
        InicioDaAvaliacao.Instante(processo, TipoMotivoDeDecisao.HomologacaoDasInscricoes, registroAntes).Should().Be(registroAntes);
        InicioDaAvaliacao.Instante(processo, TipoMotivoDeDecisao.IsencaoDaTaxa, null).Should().Be(ProcessoParaMotivos.InicioDaInscricao);
    }

    [Fact(DisplayName = "Sem fase da decisão no cronograma e sem registro, a avaliação não começou")]
    public void InicioDaAvaliacao_SemFase_NaoComecou()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();

        InicioDaAvaliacao.Instante(processo, TipoMotivoDeDecisao.Heteroidentificacao, null).Should().BeNull();
        InicioDaAvaliacao.JaComecou(processo, TipoMotivoDeDecisao.Heteroidentificacao, null, DateTimeOffset.MaxValue).Should().BeFalse();
    }

    [Fact(DisplayName = "Há uma política por tipo, e a sentinela não tem política")]
    public void Politicas_UmaPorTipo()
    {
        Enum.GetValues<TipoMotivoDeDecisao>().Where(t => t != TipoMotivoDeDecisao.Nenhum)
            .Should().OnlyContain(t => PoliticasDosTiposDeMotivo.Do(t)!.Tipo == t);
        PoliticasDosTiposDeMotivo.Do(TipoMotivoDeDecisao.Nenhum).Should().BeNull();
    }

    [Fact(DisplayName = "Os códigos de tipo e sentido vão e voltam do wire")]
    public void Codigos_IdaEVolta()
    {
        Enum.GetValues<TipoMotivoDeDecisao>().Where(t => t != TipoMotivoDeDecisao.Nenhum)
            .Should().OnlyContain(t => TipoMotivoDeDecisaoCodigo.FromCodigo(t.ToCodigo()) == t);
        TipoMotivoDeDecisaoCodigo.FromCodigo("isencao_da_taxa").Should().Be(TipoMotivoDeDecisao.Nenhum);
        SentidoDoMotivoCodigo.FromCodigo(SentidoDoMotivoCodigo.Favoravel).Should().Be(SentidoDoMotivo.Favoravel);
        SentidoDoMotivoCodigo.FromCodigo(null).Should().Be(SentidoDoMotivo.Nenhum);
        FluentActions.Invoking(() => TipoMotivoDeDecisao.Nenhum.ToCodigo()).Should().Throw<ArgumentOutOfRangeException>();
    }

    private static MotivoDoProcesso Escolher(ProcessoSeletivo processo, MotivoDeDecisao origem) =>
        EscolhaDosMotivos.Incluir(new InclusaoDeMotivo(
            processo, [], origem, null, PrecondicaoIfMatch.Ausente, ProcessoParaMotivos.InicioDaInscricao, null)).Value!;
}
