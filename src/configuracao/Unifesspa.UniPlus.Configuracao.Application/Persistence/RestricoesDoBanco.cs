namespace Unifesspa.UniPlus.Configuracao.Application.Persistence;

/// <summary>
/// Nomes dos índices e das restrições do banco que um handler de Configuração traduz em recusa
/// nomeada ao ver a violação. O mapeamento do EF nomeia o objeto com a mesma constante, de modo que
/// renomear a tabela muda o nome em um lugar só e a violação continua sendo reconhecida. Os três
/// objetos criados por script SQL do módulo (calendário, dia não útil) têm o nome travado pelos
/// testes de persistência, que provocam a violação no banco real e comparam com estas constantes.
/// </summary>
public static class RestricoesDoBanco
{
    public const string BaseLegalBonusRegionalCodigoVivo = "ix_bases_legais_do_bonus_regional_codigo_vivo";

    public const string CalendarioDeDiasUteisVigenteUnico = "ex_calendarios_de_dias_uteis_vigente_unico";

    public const string DiaNaoUtilUnicidade = "ix_dias_nao_uteis_unicidade";

    public const string CategoriaDeDocumentoCodigoVivo = "ix_categorias_de_documento_codigo_vivo";

    public const string CondicaoDeAtendimentoCodigoVivo = "ix_condicoes_de_atendimento_codigo_vivo";

    public const string CursoCodigoVivo = "ix_cursos_codigo_vivo";

    public const string FaseCanonicaCodigoVivo = "ix_fases_canonicas_codigo_vivo";

    public const string FatoDoCandidatoCodigoUnico = "ux_fatos_do_candidato_codigo";

    public const string ValorDeDominioDoFatoCodigoUnico = "ux_valores_de_dominio_do_fato_fato_codigo";

    public const string ManifestacaoDaProtecaoInicialDoAutorUnica = "ux_manifestacoes_da_protecao_inicial_do_autor";

    public const string ManifestacaoDaProtecaoSubstituicaoUnica = "ux_manifestacoes_da_protecao_substitui";

    public const string ModalidadeCodigoVivo = "ix_modalidades_codigo_vivo";

    public const string PesoPorAreaDoEnemResolucaoGrupoVivo = "ix_pesos_por_area_do_enem_resolucao_grupo_vivo";

    public const string PrecedenciaEntreFasesParVivo = "ix_precedencias_entre_fases_par_vivo";

    public const string RecursoDeAcessibilidadeCodigoVivo = "ix_recursos_de_acessibilidade_codigo_vivo";

    public const string RecursoDeAcessibilidadeNomeVivo = "ix_recursos_de_acessibilidade_nome_vivo";

    public const string ReferenciaDemograficaCensoVivo = "ix_referencias_demograficas_censo_vivo";

    public const string TipoDeConceitoValorCodigoUnico = "ix_valores_tipo_conceito_codigo";

    public const string TipoDeConceitoValorRotuloUnico = "ix_valores_tipo_conceito_rotulo";

    public const string TipoDeConceitoValorPosicaoUnica = "ix_valores_tipo_conceito_posicao";

    public const string TipoDeEtapaConceitoAdmitidoChave = "pk_tipos_etapa_conceitos";

    public const string TipoDeBancaCodigoVivo = "ix_tipos_de_banca_codigo_vivo";

    public const string TipoDeDeficienciaCodigoVivo = "ix_tipos_de_deficiencia_codigo_vivo";

    public const string TipoDeDeficienciaNomeVivo = "ix_tipos_de_deficiencia_nome_vivo";

    public const string TipoDeDocumentoCodigoVivo = "ix_tipos_de_documento_codigo_vivo";

    public const string TermoDeConsentimentoCodigoVivo = "ix_termos_de_consentimento_codigo_vivo";

    public const string VersaoDoTermoNumeroUnico = "ux_versoes_do_termo_consentimento_termo_numero";
}
