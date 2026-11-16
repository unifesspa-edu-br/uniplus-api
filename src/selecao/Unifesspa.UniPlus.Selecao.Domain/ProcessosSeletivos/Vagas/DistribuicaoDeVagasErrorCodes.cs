namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

public static class DistribuicaoDeVagasErrorCodes
{
    private const string Prefixo = "ConfiguracaoDistribuicaoVagas.";

    public const string VagasOfertadasBaseInvalido = Prefixo + "VoBaseInvalido";
    public const string PercentualDaReservaLegalForaDoLimite = Prefixo + "PrForaDoLimite";
    public const string ModalidadesVazias = Prefixo + "ModalidadesVazias";
    public const string ModalidadeDuplicada = Prefixo + "ModalidadeDuplicada";
    public const string VagasOfertadasBaseAcimaDasVagasAutorizadas = Prefixo + "VoBaseAcimaDasVagasAutorizadas";
    public const string ModalidadeNaoAdmitidaPelaRegra = Prefixo + "ModalidadeNaoAdmitidaPelaRegra";
    public const string ModalidadeDoRolAusente = Prefixo + "ModalidadeDoRolAusente";
    public const string ReferenciaDemograficaObrigatoria = Prefixo + "ReferenciaDemograficaObrigatoria";
    public const string ReferenciaDemograficaIndevida = Prefixo + "ReferenciaDemograficaIndevida";
    public const string ModalidadesFederaisIncompletas = Prefixo + "ModalidadesFederaisIncompletas";
    public const string RetiradaFederalDeveSerDeAmplaConcorrencia = Prefixo + "RetiradaFederalDeveSerDeAmplaConcorrencia";
    public const string RegraAjusteObrigatoria = Prefixo + "RegraAjusteObrigatoria";
    public const string MotorDeAjusteVedadoNaLei12711 = Prefixo + "MotorDeAjusteVedadoNaLei12711";
    public const string QuadroModalidadeAusente = Prefixo + "QuadroModalidadeAusente";
    public const string QuantidadeCalculadaNaoInformavel = Prefixo + "QuantidadeCalculadaNaoInformavel";
    public const string QuantidadeDeclaradaObrigatoria = Prefixo + "QuantidadeDeclaradaObrigatoria";
}
