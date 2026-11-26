namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio do processo seletivo declarados como constantes, e não repetidos
/// como literal no agregado e no registro de erros da API.
/// </summary>
public static class ProcessoSeletivoErrorCodes
{
    /// <summary>A regra citada não corresponde à regra atual do catálogo.</summary>
    public const string RegraCitadaNaoCorrespondeAoCatalogo = "ProcessoSeletivo.RegraCitadaNaoCorrespondeAoCatalogo";

    /// <summary>A pessoa autenticada não tem a permissão na unidade dona; responde 403.</summary>
    public const string SemPermissaoNaUnidade = "ProcessoSeletivo.SemPermissaoNaUnidade";

    /// <summary>O token não identifica o solicitante por completo, então nenhuma decisão de acesso foi tomada; responde 401.</summary>
    public const string IdentidadeIncompleta = "ProcessoSeletivo.IdentidadeIncompleta";

    /// <summary>A pessoa tem a permissão, mas está inscrita no processo e não pode atuar nele; responde 403.</summary>
    public const string ImpedidoPorInscricaoNoProcesso = "ProcessoSeletivo.ImpedidoPorInscricaoNoProcesso";

    /// <summary>O tipo de documento do vínculo aguarda a aprovação do Representante do controlador e não aceita vínculo novo.</summary>
    public const string TipoDocumentoNoVinculoComProtecaoPendente = "DocumentoExigido.TipoDocumentoComProtecaoPendente";

    /// <summary>O tipo de documento do processo aguarda a aprovação do Representante do controlador na publicação.</summary>
    public const string TipoDocumentoComProtecaoPendente = "ProcessoSeletivo.TipoDocumentoComProtecaoPendente";

    /// <summary>O dado do candidato do processo aguarda a aprovação do Representante do controlador na publicação.</summary>
    public const string FatoComProtecaoPendente = "ProcessoSeletivo.FatoComProtecaoPendente";

    /// <summary>O tipo de documento do vínculo não existe mais no cadastro.</summary>
    public const string TipoDocumentoDoVinculoNaoEncontrado = "DocumentoExigido.TipoDocumentoNaoEncontrado";

    /// <summary>Inscrição própria sem nenhuma fase que homologa as inscrições.</summary>
    public const string InscricaoPropriaSemHomologacaoDasInscricoes = "ProcessoSeletivo.InscricaoPropriaSemHomologacaoDasInscricoes";

    /// <summary>Inscrição própria com nota importada de exame e sem nenhuma fase que homologa as inscrições.</summary>
    public const string ImportacaoDeExameSemHomologacaoDasInscricoes = "ProcessoSeletivo.ImportacaoDeExameSemHomologacaoDasInscricoes";

    /// <summary>Importação externa sem nenhuma fase que homologa as inscrições.</summary>
    public const string ImportacaoExternaSemHomologacaoDasInscricoes = "ProcessoSeletivo.ImportacaoExternaSemHomologacaoDasInscricoes";

    /// <summary>Importação externa cuja fase que homologa as inscrições não tem o período de recurso entre o preliminar e o definitivo.</summary>
    public const string ImportacaoExternaSemJanelaDeRecurso = "ProcessoSeletivo.ImportacaoExternaSemJanelaDeRecurso";

    /// <summary>A fase que homologa as inscrições não prevê a banca de homologação das inscrições, nela nem nas etapas dela.</summary>
    public const string HomologacaoDasInscricoesSemBanca = "ProcessoSeletivo.HomologacaoDasInscricoesSemBanca";

    /// <summary>Inscrição própria com nota do ENEM e sem banca de importação das notas do INEP.</summary>
    public const string NotaDoEnemSemBancaDeImportacaoDasNotas = "ProcessoSeletivo.NotaDoEnemSemBancaDeImportacaoDasNotas";

    /// <summary>Importação externa sem banca de importação de candidatos.</summary>
    public const string ImportacaoExternaSemBancaDeImportacaoDeCandidatos = "ProcessoSeletivo.ImportacaoExternaSemBancaDeImportacaoDeCandidatos";

    public const string CriteriosDesempateEmExcesso = "ProcessoSeletivo.CriteriosDesempateEmExcesso";

    public const string IdentificadorLegivelTamanho = "ProcessoSeletivo.IdentificadorLegivelTamanho";
    public const string IdentificadorLegivelFormatoInvalido = "ProcessoSeletivo.IdentificadorLegivelFormatoInvalido";
    public const string IdentificadorLegivelComFormatoDeGuid = "ProcessoSeletivo.IdentificadorLegivelComFormatoDeGuid";
    public const string IdentificadorLegivelAusente = "ProcessoSeletivo.IdentificadorLegivelAusente";
    public const string IdentificadorLegivelImutavel = "ProcessoSeletivo.IdentificadorLegivelImutavel";
    public const string FadaAplicaSemConfiguracao = "ProcessoSeletivo.FadaAplicaSemConfiguracao";
    public const string FadaNaoAplicaComConfiguracao = "ProcessoSeletivo.FadaNaoAplicaComConfiguracao";
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
    public const string CotaEAcaoAfirmativaDerivaveisJuntas = "ProcessoSeletivo.CotaEAcaoAfirmativaDerivaveisJuntas";
    public const string RegraIncoerenteComANatureza = "ProcessoSeletivo.RegraIncoerenteComANatureza";
    public const string TipoDeAtoUnicoDeclaradoMaisDeUmaVez = "ProcessoSeletivo.TipoDeAtoUnicoDeclaradoMaisDeUmaVez";
    public const string ResultadoDeclaradoSemPapel = "ProcessoSeletivo.ResultadoDeclaradoSemPapel";
    public const string TipoQueCongelaDeclaradoComoResultado = "ProcessoSeletivo.TipoQueCongelaDeclaradoComoResultado";
    public const string RetificacaoDesfazCicloRecursalAberto = "ProcessoSeletivo.RetificacaoDesfazCicloRecursalAberto";
    public const string EtapaComPreliminarSemDefinitivo = "ProcessoSeletivo.EtapaComPreliminarSemDefinitivo";
    public const string FaseComPreliminarSemDefinitivo = "ProcessoSeletivo.FaseComPreliminarSemDefinitivo";
    public const string FaseSemDefinitivoSemDataFinal = "ProcessoSeletivo.FaseSemDefinitivoSemDataFinal";
    public const string PrimeiraFaseNoMesmoMomentoDaAnterior = "ProcessoSeletivo.PrimeiraFaseNoMesmoMomentoDaAnterior";
    public const string FasesComPrecedenciaNoMesmoMomento = "ProcessoSeletivo.FasesComPrecedenciaNoMesmoMomento";
    public const string TipoProcessoNaoEncontradoOuInativo = "ProcessoSeletivo.TipoProcessoNaoEncontradoOuInativo";
    public const string UnidadeAdministradoraNaoEncontrada = "ProcessoSeletivo.UnidadeAdministradoraNaoEncontrada";
    public const string AlgoritmoContagemPrazoNaoEncontrado = "ProcessoSeletivo.AlgoritmoContagemPrazoNaoEncontrado";

    public const string TipoProcessoAusente = "ProcessoSeletivo.TipoProcessoAusente";
    public const string UnidadeAdministradoraAusente = "ProcessoSeletivo.UnidadeAdministradoraAusente";
    public const string OrigemCandidatosAusente = "ProcessoSeletivo.OrigemCandidatosAusente";

    public const string RemanejamentoIncoerenteComANatureza = "ProcessoSeletivo.RemanejamentoIncoerenteComANatureza";

    /// <summary>
    /// O tipo de documento congelado na exigência é menos protegido que o mais protegido dos fatos
    /// que ela comprova, na classificação congelada de cada fato no processo.
    /// </summary>
    public const string ExigenciaMenosProtegidaQueFatoComprovado = "ProcessoSeletivo.ExigenciaMenosProtegidaQueFatoComprovado";
}
