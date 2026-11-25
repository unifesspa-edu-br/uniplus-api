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
    public void FormatoPermitido_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<FormatoPermitido>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (FormatoPermitido.Pdf, "PDF", "PDF"),
                (FormatoPermitido.Jpeg, "JPEG", "JPEG"),
                (FormatoPermitido.Png, "PNG", "PNG"));

    [Fact]
    public void SituacaoInscricao_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<SituacaoInscricao>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (SituacaoInscricao.Rascunho, "RASCUNHO", "Rascunho"),
                (SituacaoInscricao.Submetida, "SUBMETIDA", "Submetida"),
                (SituacaoInscricao.Cancelada, "CANCELADA", "Cancelada"));

    [Fact]
    public void SituacaoDoProcessoSeletivo_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<SituacaoDoProcessoSeletivo>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (SituacaoDoProcessoSeletivo.Rascunho, "RASCUNHO", "Rascunho"),
                (SituacaoDoProcessoSeletivo.Publicado, "PUBLICADO", "Publicado"),
                (SituacaoDoProcessoSeletivo.Encerrado, "ENCERRADO", "Encerrado"),
                (SituacaoDoProcessoSeletivo.Cancelado, "CANCELADO", "Cancelado"));

    [Fact]
    public void OrigemDaResposta_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<OrigemDaResposta>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (OrigemDaResposta.Informada, "INFORMADA", "Informada"),
                (OrigemDaResposta.Calculada, "CALCULADA", "Calculada"));

    [Fact]
    public void TipoDoValorDeResposta_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<TipoDoValorDeResposta>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (TipoDoValorDeResposta.SimOuNao, "SIM_OU_NAO", "Sim ou não"),
                (TipoDoValorDeResposta.Numero, "NUMERO", "Número"),
                (TipoDoValorDeResposta.Opcao, "OPCAO", "Opção"),
                (TipoDoValorDeResposta.Texto, "TEXTO", "Texto"),
                (TipoDoValorDeResposta.Data, "DATA", "Data"),
                (TipoDoValorDeResposta.Endereco, "ENDERECO", "Endereço"),
                (TipoDoValorDeResposta.Municipio, "MUNICIPIO", "Município"));

    [Fact]
    public void SituacaoDoArquivoDoCandidato_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<SituacaoDoArquivoDoCandidato>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (SituacaoDoArquivoDoCandidato.Pendente, "PENDENTE", "Pendente"),
                (SituacaoDoArquivoDoCandidato.EmVerificacao, "EM_VERIFICACAO", "Em verificação"),
                (SituacaoDoArquivoDoCandidato.Verificado, "VERIFICADO", "Verificado"),
                (SituacaoDoArquivoDoCandidato.Recusado, "RECUSADO", "Recusado"),
                (SituacaoDoArquivoDoCandidato.ReenvioNecessario, "REENVIO_NECESSARIO", "Reenvio necessário"));

    [Fact]
    public void MotivoDeRecusaDoArquivo_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<MotivoDeRecusaDoArquivo>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (MotivoDeRecusaDoArquivo.TamanhoExcedido, "TAMANHO_EXCEDIDO", "Tamanho excedido"),
                (MotivoDeRecusaDoArquivo.FormatoNaoCorresponde, "FORMATO_NAO_CORRESPONDE", "Formato incompatível"),
                (MotivoDeRecusaDoArquivo.PdfComSenha, "PDF_COM_SENHA", "PDF com senha"),
                (MotivoDeRecusaDoArquivo.NaoPassouNaVerificacaoDeSeguranca, "NAO_PASSOU_NA_VERIFICACAO_DE_SEGURANCA", "Não passou na verificação de segurança"));

    [Fact]
    public void StatusArquivoEnviado_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<StatusArquivoEnviado>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (StatusArquivoEnviado.Pendente, "PENDENTE", "Pendente"),
                (StatusArquivoEnviado.Confirmado, "CONFIRMADO", "Confirmado"));

    [Fact]
    public void FinalidadeDoDocumento_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<FinalidadeDoDocumento>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (FinalidadeDoDocumento.DocumentoDoEdital, "DOCUMENTO_DO_EDITAL", "Documento do edital"),
                (FinalidadeDoDocumento.AnexoDoEdital, "ANEXO_DO_EDITAL", "Anexo do edital"),
                (FinalidadeDoDocumento.DocumentoDeAto, "DOCUMENTO_DE_ATO", "Documento de ato"));

    [Fact]
    public void FormatoDeDocumento_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<FormatoDeDocumento>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (FormatoDeDocumento.Docx, "DOCX", "DOCX"),
                (FormatoDeDocumento.Odt, "ODT", "ODT"),
                (FormatoDeDocumento.Pdf, "PDF", "PDF"));

    [Fact]
    public void SituacaoDoDocumento_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<SituacaoDoDocumento>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (SituacaoDoDocumento.NaoExigido, "NAO_EXIGIDO", "Não exigido"),
                (SituacaoDoDocumento.Pendente, "PENDENTE", "Pendente"),
                (SituacaoDoDocumento.PodeSerExigido, "PODE_SER_EXIGIDO", "Pode ser exigido"),
                (SituacaoDoDocumento.DependeDeFasePosterior, "DEPENDE_DE_FASE_POSTERIOR", "Depende de fase posterior"),
                (SituacaoDoDocumento.Satisfeita, "SATISFEITA", "Satisfeita"));

    [Fact]
    public void SentidoDoMotivo_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<SentidoDoMotivo>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (SentidoDoMotivo.Favoravel, "FAVORAVEL", "favorável"),
                (SentidoDoMotivo.Desfavoravel, "DESFAVORAVEL", "desfavorável"));

    [Fact]
    public void SituacaoDaAvaliacao_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<SituacaoDaAvaliacao>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (SituacaoDaAvaliacao.Apurada, "APURADA", "Apurada"),
                (SituacaoDaAvaliacao.InscritoSemLinha, "INSCRITO_SEM_LINHA", "Inscrito sem linha"),
                (SituacaoDaAvaliacao.EmConflito, "EM_CONFLITO", "Em conflito"));

    [Fact]
    public void SituacaoDaProvaNoEnem_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<SituacaoDaProvaNoEnem>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (SituacaoDaProvaNoEnem.Presente, "PRESENTE", "Presente"),
                (SituacaoDaProvaNoEnem.Ausente, "AUSENTE", "Ausente"));

    [Fact]
    public void TipoDeOcorrenciaDoLote_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<TipoDeOcorrenciaDoLote>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (TipoDeOcorrenciaDoLote.InscritoSemLinha, "INSCRITO_SEM_LINHA", "Inscrito sem linha"),
                (TipoDeOcorrenciaDoLote.LinhaSemInscrito, "LINHA_SEM_INSCRITO", "Linha sem inscrito"),
                (TipoDeOcorrenciaDoLote.Conflito, "CONFLITO", "Conflito"));

    [Fact]
    public void SituacaoDasModalidades_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<SituacaoDasModalidades>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (SituacaoDasModalidades.Concorre, "CONCORRE", "Concorre"),
                (SituacaoDasModalidades.NaoConcorre, "NAO_CONCORRE", "Não concorre"),
                (SituacaoDasModalidades.FaltaResponder, "FALTA_RESPONDER", "Falta responder"));
    [Fact]
    public void TipoDeLeiauteDoInep_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<TipoDeLeiauteDoInep>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal((TipoDeLeiauteDoInep.Envio, "ENVIO", "Envio"), (TipoDeLeiauteDoInep.Retorno, "RETORNO", "Retorno"));

    [Fact]
    public void SituacaoDoCertame_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<SituacaoDoCertame>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (SituacaoDoCertame.EmBreve, "EM_BREVE", "Em breve"),
                (SituacaoDoCertame.InscricoesAbertas, "INSCRICOES_ABERTAS", "Inscrições abertas"),
                (SituacaoDoCertame.UltimosDias, "ULTIMOS_DIAS", "Últimos dias"),
                (SituacaoDoCertame.Encerradas, "ENCERRADAS", "Encerradas"));

    [Fact]
    public void TipoDeCondicaoDeHomologacao_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<TipoDeCondicaoDeHomologacao>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal((TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep, "RESULTADO_NO_RETORNO_DO_INEP", "Resultado no retorno do INEP"));
    [Fact]
    public void FundamentoIsencao_Valores_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<FundamentoIsencao>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (FundamentoIsencao.CadastroUnico, "CADASTRO_UNICO", "Cadastro Único"),
                (FundamentoIsencao.DoacaoMedulaOssea, "DOACAO_MEDULA_OSSEA", "Doação de medula óssea"),
                (FundamentoIsencao.CarenciaSocioeconomica, "CARENCIA_SOCIOECONOMICA", "Carência socioeconômica"));
}
