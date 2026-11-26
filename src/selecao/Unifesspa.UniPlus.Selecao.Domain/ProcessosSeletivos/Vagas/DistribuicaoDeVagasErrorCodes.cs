namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

public static class DistribuicaoDeVagasErrorCodes
{
    private const string Prefixo = "ConfiguracaoDistribuicaoVagas.";

    public const string VagasOfertadasBaseInvalido = Prefixo + "VagasOfertadasBaseInvalido";
    public const string PercentualDaReservaLegalForaDoLimite = Prefixo + "PercentualDaReservaLegalForaDoLimite";
    public const string QuantidadeVagaNegativa = Prefixo + "QuantidadeVagaNegativa";
    public const string ModalidadesVazias = Prefixo + "ModalidadesVazias";
    public const string ModalidadeDuplicada = Prefixo + "ModalidadeDuplicada";
    public const string VagasOfertadasBaseAcimaDasVagasAutorizadas = Prefixo + "VagasOfertadasBaseAcimaDasVagasAutorizadas";
    public const string ModalidadeNaoAdmitidaPelaRegra = Prefixo + "ModalidadeNaoAdmitidaPelaRegra";
    public const string ModalidadeDoRolAusente = Prefixo + "ModalidadeDoRolAusente";
    public const string ReferenciaDemograficaObrigatoria = Prefixo + "ReferenciaDemograficaObrigatoria";
    public const string ReferenciaDemograficaIndevida = Prefixo + "ReferenciaDemograficaIndevida";
    public const string ModalidadesFederaisIncompletas = Prefixo + "ModalidadesFederaisIncompletas";
    public const string RetiradaFederalDeveSerDeAmplaConcorrencia = Prefixo + "RetiradaFederalDeveSerDeAmplaConcorrencia";
    public const string RegraAjusteObrigatoria = Prefixo + "RegraAjusteObrigatoria";
    public const string RegraDeAjusteVedadaNaLeiDeCotas = Prefixo + "RegraDeAjusteVedadaNaLeiDeCotas";
    public const string QuadroModalidadeAusente = Prefixo + "QuadroModalidadeAusente";
    public const string QuantidadeCalculadaNaoInformavel = Prefixo + "QuantidadeCalculadaNaoInformavel";
    public const string QuantidadeDeclaradaObrigatoria = Prefixo + "QuantidadeDeclaradaObrigatoria";
    public const string ComposicaoOrigemNaoSelecionada = Prefixo + "ComposicaoOrigemNaoSelecionada";
    public const string RemanejamentoDestinoNaoSelecionado = Prefixo + "RemanejamentoDestinoNaoSelecionado";
    public const string RemanejamentoParNaoSelecionado = Prefixo + "RemanejamentoParNaoSelecionado";
    public const string RemanejamentoFallbackNaoSelecionado = Prefixo + "RemanejamentoFallbackNaoSelecionado";
    public const string ParCruzadoNaoReciproco = Prefixo + "ParCruzadoNaoReciproco";
    public const string QuadroNaoCompletaAsVagasOfertadasBase = Prefixo + "QuadroNaoCompletaAsVagasOfertadasBase";
    public const string QuadroExcedeAsVagasOfertadasBase = Prefixo + "QuadroExcedeAsVagasOfertadasBase";

    public const string DistribuicaoVaziaNoProcesso = "ProcessoSeletivo.DistribuicaoVagasVazia";
    public const string OfertaDeCursoDuplicada = "ProcessoSeletivo.OfertaCursoDuplicada";
    public const string AcaoQuandoIndeferidoDivergente = "ProcessoSeletivo.AcaoQuandoIndeferidoDivergente";
}
