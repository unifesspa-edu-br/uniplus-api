namespace Unifesspa.UniPlus.Configuracao.Application.Permissoes;

/// <summary>
/// Os tipos de resource que a decisão de acesso enxerga em Configuração, no formato de
/// <c>ResourceTipos</c>. A restrição de tipo de uma concessão é comparada por igualdade exata, então cada valor é o
/// contrato com quem restringe uma concessão por tipo.
/// </summary>
public static class ResourceTiposConfiguracao
{
    /// <summary>Tipo <c>campus</c>.</summary>
    public const string Campus = "campus";

    /// <summary>Tipo <c>curso</c>.</summary>
    public const string Curso = "curso";

    /// <summary>Tipo <c>tipo-documento</c>.</summary>
    public const string TipoDocumento = "tipo-documento";

    /// <summary>Tipo <c>fato-candidato</c>.</summary>
    public const string FatoCandidato = "fato-candidato";

    /// <summary>Tipo <c>modalidade</c>.</summary>
    public const string Modalidade = "modalidade";

    /// <summary>Tipo <c>precedencia-fase</c>.</summary>
    public const string PrecedenciaFase = "precedencia-fase";

    /// <summary>Tipo <c>fase-canonica</c>.</summary>
    public const string FaseCanonica = "fase-canonica";

    /// <summary>Tipo <c>referencia-reserva-demografica</c>.</summary>
    public const string ReferenciaReservaDemografica = "referencia-reserva-demografica";

    /// <summary>Tipo <c>recurso-acessibilidade</c>.</summary>
    public const string RecursoAcessibilidade = "recurso-acessibilidade";

    /// <summary>Tipo <c>tipo-banca</c>.</summary>
    public const string TipoBanca = "tipo-banca";

    /// <summary>Tipo <c>condicao-atendimento</c>.</summary>
    public const string CondicaoAtendimento = "condicao-atendimento";

    /// <summary>Tipo <c>peso-area-enem</c>.</summary>
    public const string PesoAreaEnem = "peso-area-enem";

    /// <summary>Tipo <c>modelo-formulario</c>.</summary>
    public const string ModeloFormulario = "modelo-formulario";

    /// <summary>Tipo <c>categoria-documento</c>.</summary>
    public const string CategoriaDocumento = "categoria-documento";

    /// <summary>Tipo <c>tipo-deficiencia</c>.</summary>
    public const string TipoDeficiencia = "tipo-deficiencia";

    /// <summary>Tipo <c>termo-consentimento</c>.</summary>
    public const string TermoConsentimento = "termo-consentimento";

    /// <summary>Tipo <c>base-legal-bonus-regional</c>.</summary>
    public const string BaseLegalBonusRegional = "base-legal-bonus-regional";

    /// <summary>Tipo <c>tipo-processo</c>.</summary>
    public const string TipoProcesso = "tipo-processo";

    /// <summary>Tipo <c>calendario-dias-uteis</c>.</summary>
    public const string CalendarioDiasUteis = "calendario-dias-uteis";

    /// <summary>Tipo <c>tipo-etapa</c>.</summary>
    public const string TipoEtapa = "tipo-etapa";

    /// <summary>Tipo <c>local-oferta</c>.</summary>
    public const string LocalOferta = "local-oferta";

    /// <summary>Tipo <c>oferta-curso</c>.</summary>
    public const string OfertaCurso = "oferta-curso";
}
