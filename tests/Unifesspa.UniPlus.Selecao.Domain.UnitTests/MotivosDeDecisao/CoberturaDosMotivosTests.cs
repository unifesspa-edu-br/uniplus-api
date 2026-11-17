namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// A conferência de cobertura dos motivos (UNI-REQ-0123) e a recópia ao congelar (UNI-REQ-0122).
/// </summary>
public sealed class CoberturaDosMotivosTests
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

    [Fact(DisplayName = "Ao congelar, a escolha que entra na versão de motivo desativado é recusada até a troca")]
    public void Recopiar_QueEntraDesativado_Recusa()
    {
        MotivoDeDecisao origem = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel);
        MotivoDoProcesso escolhido = Escolher(ProcessoParaMotivos.Rascunho(), origem);
        MotivoDoProcesso semOrigem = Escolher(ProcessoParaMotivos.Rascunho(), origem);
        origem.Desativar();

        Result resultado = MotivosAoCongelar.Recopiar(
            [new MotivoEscolhidoComOrigem(escolhido, origem), new MotivoEscolhidoComOrigem(semOrigem, null)], numeroDaVersaoQueNasce: 1);

        resultado.Errors.Should().HaveCount(2).And.OnlyContain(e => e.Error.Code == MotivoDeDecisaoErrorCodes.MotivoDesativado);
    }

    [Fact(DisplayName = "Ao congelar, a escolha que entra na versão recopia a descrição vigente do cadastro")]
    public void Recopiar_QueEntra_CopiaADescricaoVigente()
    {
        MotivoDeDecisao origem = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel);
        MotivoDoProcesso escolhido = Escolher(ProcessoParaMotivos.Rascunho(), origem);
        origem.Atualizar("Redação vigente na publicação.");

        MotivosAoCongelar.Recopiar([new MotivoEscolhidoComOrigem(escolhido, origem)], numeroDaVersaoQueNasce: 1).IsSuccess.Should().BeTrue();

        escolhido.Descricao.Should().Be("Redação vigente na publicação.");
    }

    [Fact(DisplayName = "A escolha que entrou em versão anterior continua utilizável com o motivo desativado depois, sem recopiar")]
    public void Recopiar_DeVersaoAnteriorDesativado_AceitaEMantemADescricao()
    {
        MotivoDeDecisao origem = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel);
        MotivoDoProcesso publicado = Escolher(ProcessoParaMotivos.Rascunho(), origem);
        origem.Atualizar("Redação posterior à publicação.");
        origem.Desativar();

        MotivosAoCongelar.Recopiar([new MotivoEscolhidoComOrigem(publicado, origem)], numeroDaVersaoQueNasce: 2).IsSuccess.Should().BeTrue();

        publicado.Descricao.Should().Be("Descrição do motivo RENDA_ACIMA_DO_LIMITE");
    }

    [Fact(DisplayName = "Na versão que sucede, só a escolha nova é recopiada e só a nova recusa o motivo desativado")]
    public void Recopiar_NaSucessora_SoAEscolhaNova()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        LugarDaDecisao lugar = ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO");
        MotivoDeDecisao antiga = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Favoravel, "ANTIGO");
        MotivoDeDecisao nova = ProcessoParaMotivos.DaIsencao(FundamentoIsencaoCodigo.CadastroUnico, SentidoDoMotivo.Desfavoravel, "NOVO");
        MotivoDoProcesso publicada = ProcessoParaMotivos.Escolha(processo, lugar, antiga, versaoDeEntrada: 1);
        MotivoDoProcesso pendente = ProcessoParaMotivos.Escolha(processo, lugar, nova, versaoDeEntrada: 2);
        antiga.Desativar();
        nova.Atualizar("Redação vigente no fechamento.");

        Result resultado = MotivosAoCongelar.Recopiar(
            [new MotivoEscolhidoComOrigem(publicada, antiga), new MotivoEscolhidoComOrigem(pendente, nova)], numeroDaVersaoQueNasce: 2);

        resultado.IsSuccess.Should().BeTrue();
        pendente.Descricao.Should().Be("Redação vigente no fechamento.");
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
        ProcessoParaMotivos.Escolha(processo, ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO"), origem, versaoDeEntrada: 1);
}
