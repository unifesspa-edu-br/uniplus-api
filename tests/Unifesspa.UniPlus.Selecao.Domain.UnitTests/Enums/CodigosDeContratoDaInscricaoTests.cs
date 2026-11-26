namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Leiautes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

public sealed class CodigosDeContratoDaInscricaoTests
{
    [Fact]
    public void FormatoPermitido_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<FormatoPermitido>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (FormatoPermitido.Pdf, "PDF"),
                (FormatoPermitido.Jpeg, "JPEG"),
                (FormatoPermitido.Png, "PNG"));

    [Fact]
    public void SituacaoInscricao_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<SituacaoInscricao>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (SituacaoInscricao.Rascunho, "RASCUNHO"),
                (SituacaoInscricao.Enviada, "ENVIADA"),
                (SituacaoInscricao.Cancelada, "CANCELADA"));

    [Fact]
    public void SituacaoDoProcessoSeletivo_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<SituacaoDoProcessoSeletivo>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (SituacaoDoProcessoSeletivo.Rascunho, "RASCUNHO"),
                (SituacaoDoProcessoSeletivo.Publicado, "PUBLICADO"),
                (SituacaoDoProcessoSeletivo.Encerrado, "ENCERRADO"),
                (SituacaoDoProcessoSeletivo.Cancelado, "CANCELADO"));

    [Fact]
    public void OrigemDaResposta_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<OrigemDaResposta>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (OrigemDaResposta.Informada, "INFORMADA"),
                (OrigemDaResposta.Calculada, "CALCULADA"));

    [Fact]
    public void TipoDoValorDeResposta_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<TipoDoValorDeResposta>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (TipoDoValorDeResposta.SimOuNao, "SIM_OU_NAO"),
                (TipoDoValorDeResposta.Numero, "NUMERO"),
                (TipoDoValorDeResposta.Opcao, "OPCAO"),
                (TipoDoValorDeResposta.Texto, "TEXTO"),
                (TipoDoValorDeResposta.Data, "DATA"),
                (TipoDoValorDeResposta.Endereco, "ENDERECO"),
                (TipoDoValorDeResposta.Municipio, "MUNICIPIO"));

    [Fact]
    public void SituacaoDoArquivoDoCandidato_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<SituacaoDoArquivoDoCandidato>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (SituacaoDoArquivoDoCandidato.Pendente, "PENDENTE"),
                (SituacaoDoArquivoDoCandidato.EmVerificacao, "EM_VERIFICACAO"),
                (SituacaoDoArquivoDoCandidato.Verificado, "VERIFICADO"),
                (SituacaoDoArquivoDoCandidato.Recusado, "RECUSADO"),
                (SituacaoDoArquivoDoCandidato.ReenvioNecessario, "REENVIO_NECESSARIO"));

    [Fact]
    public void MotivoDeRecusaDoArquivo_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<MotivoDeRecusaDoArquivo>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (MotivoDeRecusaDoArquivo.TamanhoExcedido, "TAMANHO_EXCEDIDO"),
                (MotivoDeRecusaDoArquivo.FormatoNaoCorresponde, "FORMATO_NAO_CORRESPONDE"),
                (MotivoDeRecusaDoArquivo.PdfComSenha, "PDF_COM_SENHA"),
                (MotivoDeRecusaDoArquivo.NaoPassouNaVerificacaoDeSeguranca, "NAO_PASSOU_NA_VERIFICACAO_DE_SEGURANCA"));

    [Fact]
    public void StatusArquivoEnviado_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<StatusArquivoEnviado>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (StatusArquivoEnviado.Pendente, "PENDENTE"),
                (StatusArquivoEnviado.Confirmado, "CONFIRMADO"));

    [Fact]
    public void FinalidadeDoDocumento_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<FinalidadeDoDocumento>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (FinalidadeDoDocumento.DocumentoDoEdital, "DOCUMENTO_DO_EDITAL"),
                (FinalidadeDoDocumento.AnexoDoEdital, "ANEXO_DO_EDITAL"),
                (FinalidadeDoDocumento.DocumentoDeAto, "DOCUMENTO_DE_ATO"));

    [Fact]
    public void FormatoDeDocumento_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<FormatoDeDocumento>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (FormatoDeDocumento.Docx, "DOCX"),
                (FormatoDeDocumento.Odt, "ODT"),
                (FormatoDeDocumento.Pdf, "PDF"));

    [Fact]
    public void SituacaoDoDocumento_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<SituacaoDoDocumento>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (SituacaoDoDocumento.NaoExigido, "NAO_EXIGIDO"),
                (SituacaoDoDocumento.Pendente, "PENDENTE"),
                (SituacaoDoDocumento.PodeSerExigido, "PODE_SER_EXIGIDO"),
                (SituacaoDoDocumento.DependeDeFasePosterior, "DEPENDE_DE_FASE_POSTERIOR"),
                (SituacaoDoDocumento.Satisfeita, "SATISFEITA"));

    [Fact]
    public void SentidoDoMotivo_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<SentidoDoMotivo>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (SentidoDoMotivo.Favoravel, "FAVORAVEL"),
                (SentidoDoMotivo.Desfavoravel, "DESFAVORAVEL"));

    [Fact]
    public void SituacaoDaAvaliacao_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<SituacaoDaAvaliacao>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (SituacaoDaAvaliacao.Apurada, "APURADA"),
                (SituacaoDaAvaliacao.InscritoSemLinha, "INSCRITO_SEM_LINHA"),
                (SituacaoDaAvaliacao.EmConflito, "EM_CONFLITO"));

    [Fact]
    public void SituacaoDaProvaNoEnem_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<SituacaoDaProvaNoEnem>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (SituacaoDaProvaNoEnem.Presente, "PRESENTE"),
                (SituacaoDaProvaNoEnem.Ausente, "AUSENTE"));

    [Fact]
    public void TipoDeOcorrenciaDoLote_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<TipoDeOcorrenciaDoLote>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (TipoDeOcorrenciaDoLote.InscritoSemLinha, "INSCRITO_SEM_LINHA"),
                (TipoDeOcorrenciaDoLote.LinhaSemInscrito, "LINHA_SEM_INSCRITO"),
                (TipoDeOcorrenciaDoLote.Conflito, "CONFLITO"));

    [Fact]
    public void SituacaoDasModalidades_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<SituacaoDasModalidades>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (SituacaoDasModalidades.Concorre, "CONCORRE"),
                (SituacaoDasModalidades.NaoConcorre, "NAO_CONCORRE"),
                (SituacaoDasModalidades.FaltaResponder, "FALTA_RESPONDER"));
    [Fact]
    public void TipoDeLeiauteDoInep_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<TipoDeLeiauteDoInep>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal((TipoDeLeiauteDoInep.Envio, "ENVIO"), (TipoDeLeiauteDoInep.Retorno, "RETORNO"));

    [Fact]
    public void SituacaoDoCertame_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<SituacaoDoCertame>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (SituacaoDoCertame.EmBreve, "EM_BREVE"),
                (SituacaoDoCertame.InscricoesAbertas, "INSCRICOES_ABERTAS"),
                (SituacaoDoCertame.UltimosDias, "ULTIMOS_DIAS"),
                (SituacaoDoCertame.Encerradas, "ENCERRADAS"));

    [Fact]
    public void TipoDeCondicaoDeHomologacao_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<TipoDeCondicaoDeHomologacao>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal((TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep, "RESULTADO_NO_RETORNO_DO_INEP"));
    [Fact]
    public void FundamentoIsencao_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<FundamentoIsencao>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (FundamentoIsencao.CadastroUnico, "CADASTRO_UNICO"),
                (FundamentoIsencao.DoacaoMedulaOssea, "DOACAO_MEDULA_OSSEA"),
                (FundamentoIsencao.CarenciaSocioeconomica, "CARENCIA_SOCIOECONOMICA"));

    [Fact]
    public void MotivoDasInscricoesFechadas_Valores_TemCodigoDeclarado() =>
        Enum.GetValues<MotivoDasInscricoesFechadas>().Select(static valor => (valor, valor.Codigo()))
            .Should().Equal(
                (MotivoDasInscricoesFechadas.PeriodoEncerrado, "PERIODO_ENCERRADO"),
                (MotivoDasInscricoesFechadas.PeriodoNaoIniciado, "PERIODO_NAO_INICIADO"),
                (MotivoDasInscricoesFechadas.ProcessoSemInscricao, "PROCESSO_SEM_INSCRICAO"),
                (MotivoDasInscricoesFechadas.PeriodoNaoConfirmado, "PERIODO_NAO_CONFIRMADO"));
}
