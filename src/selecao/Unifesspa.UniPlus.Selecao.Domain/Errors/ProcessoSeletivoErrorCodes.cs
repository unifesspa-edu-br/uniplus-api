namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio do processo seletivo declarados como constantes, e não repetidos
/// como literal no agregado e no registro de erros da API.
/// </summary>
public static class ProcessoSeletivoErrorCodes
{
    public const string CriteriosDesempateEmExcesso = "ProcessoSeletivo.CriteriosDesempateEmExcesso";

    public const string IdentificadorLegivelTamanho = "ProcessoSeletivo.IdentificadorLegivelTamanho";
    public const string IdentificadorLegivelFormatoInvalido = "ProcessoSeletivo.IdentificadorLegivelFormatoInvalido";
    public const string IdentificadorLegivelComFormatoDeGuid = "ProcessoSeletivo.IdentificadorLegivelComFormatoDeGuid";
    public const string IdentificadorLegivelAusente = "ProcessoSeletivo.IdentificadorLegivelAusente";
    public const string IdentificadorLegivelImutavel = "ProcessoSeletivo.IdentificadorLegivelImutavel";
    public const string BonusRegionalAplicaSemConfiguracao = "ProcessoSeletivo.BonusRegionalAplicaSemConfiguracao";
    public const string BonusRegionalNaoAplicaComConfiguracao = "ProcessoSeletivo.BonusRegionalNaoAplicaComConfiguracao";
    public const string OpcoesDeCursoInvalido = "ProcessoSeletivo.OpcoesDeCursoInvalido";
    public const string ConflitoDeConcorrencia = "ProcessoSeletivo.ConflitoDeConcorrencia";
    public const string OpcoesDeCursoAcimaDasOfertas = "ProcessoSeletivo.OpcoesDeCursoAcimaDasOfertas";
    public const string FatoElegibilidadeAusente = "ProcessoSeletivo.FatoElegibilidadeAusente";
    public const string FatoElegibilidadeForaDoProcesso = "ProcessoSeletivo.FatoElegibilidadeForaDoProcesso";
    public const string FatoElegibilidadeDeMembroDeGrupo = "ProcessoSeletivo.FatoElegibilidadeDeMembroDeGrupo";
    public const string FatoElegibilidadeNaoBooleano = "ProcessoSeletivo.FatoElegibilidadeNaoBooleano";
    public const string FatoElegibilidadeConhecidoDepoisDaInscricao = "ProcessoSeletivo.FatoElegibilidadeConhecidoDepoisDaInscricao";
    public const string FatoElegibilidadeDaIsencao = "ProcessoSeletivo.FatoElegibilidadeDaIsencao";
    public const string IdentificadorLegivelEmUso = "ProcessoSeletivo.IdentificadorLegivelEmUso";
    public const string RecursoAcessibilidadeReferenciadoPorCondicaoViva = "ProcessoSeletivo.RecursoAcessibilidadeReferenciadoPorCondicaoViva";
    public const string RegraDeAplicabilidadeDivergente = "ProcessoSeletivo.RegraDeAplicabilidadeDivergente";
    public const string DerivacaoDaOferta = "ProcessoSeletivo.DerivacaoDaOferta";
    public const string RegraDeModalidadeCitaFatoNaoColetado = "ProcessoSeletivo.RegraDeModalidadeCitaFatoNaoColetado";
    public const string CotasDaLeiDiferentesEntreOfertas = "ProcessoSeletivo.CotasDaLeiDiferentesEntreOfertas";
    public const string ReservaPcdSemCotaPcdDaLei = "ProcessoSeletivo.ReservaPcdSemCotaPcdDaLei";
    public const string RegraIncoerenteComANatureza = "ProcessoSeletivo.RegraIncoerenteComANatureza";
    public const string TipoDeAtoUnicoDeclaradoMaisDeUmaVez = "ProcessoSeletivo.TipoDeAtoUnicoDeclaradoMaisDeUmaVez";
    public const string ResultadoDeclaradoSemPapel = "ProcessoSeletivo.ResultadoDeclaradoSemPapel";
    public const string TipoQueCongelaDeclaradoComoResultado = "ProcessoSeletivo.TipoQueCongelaDeclaradoComoResultado";
    public const string ConclusaoDuplicada = "ProcessoSeletivo.ConclusaoDuplicada";
    public const string RetificacaoDesfazCicloRecursalAberto = "ProcessoSeletivo.RetificacaoDesfazCicloRecursalAberto";
    public const string EtapaComPreliminarSemDefinitivo = "ProcessoSeletivo.EtapaComPreliminarSemDefinitivo";
    public const string RemanejamentoIncoerenteComANatureza = "ProcessoSeletivo.RemanejamentoIncoerenteComANatureza";
}
