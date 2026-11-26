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
    public const string BaseLegalDoFadaCodigoAtivos = "ux_bases_legais_do_fada_codigo_ativos";

    public const string CalendarioDeDiasUteisVigenteUnico = "ex_calendarios_de_dias_uteis_vigente_unico";

    public const string DiaNaoUtilUnicidade = "ix_dias_nao_uteis_unicidade";

    public const string CategoriaDeDocumentoCodigoAtivos = "ux_categorias_de_documento_codigo_ativos";

    public const string CondicaoDeAtendimentoCodigoAtivos = "ux_condicoes_de_atendimento_codigo_ativos";

    public const string CursoCodigoAtivos = "ux_cursos_codigo_ativos";

    public const string OfertaDeCursoCodigoAtivos = "ux_ofertas_de_curso_codigo_ativos";

    public const string FaseCanonicaCodigoAtivos = "ux_tipos_de_fase_codigo_ativos";

    public const string FatoDoCandidatoCodigoUnico = "ux_dados_do_candidato_codigo";

    public const string ValorDeDominioDoFatoCodigoUnico = "ux_opcoes_dos_dados_dado_codigo";

    public const string ManifestacaoDaProtecaoInicialDoAutorUnica = "ux_manifestacoes_da_protecao_inicial_do_autor";

    public const string ManifestacaoDaProtecaoSubstituicaoUnica = "ux_manifestacoes_da_protecao_substitui";

    public const string ModalidadeCodigoAtivos = "ux_modalidades_codigo_ativos";

    public const string PesoPorAreaDoEnemResolucaoGrupoAtivos = "ux_pesos_por_area_do_enem_resolucao_grupo_ativos";

    public const string PrecedenciaEntreFasesParAtivos = "ux_precedencias_entre_fases_par_ativos";

    public const string RecursoDeAcessibilidadeCodigoAtivos = "ux_recursos_de_acessibilidade_codigo_ativos";

    public const string RecursoDeAcessibilidadeNomeAtivos = "ux_recursos_de_acessibilidade_nome_ativos";

    public const string ReferenciaDemograficaCensoAtivos = "ux_referencias_demograficas_censo_ativos";

    public const string TipoDeConceitoValorCodigoUnico = "ux_opcoes_do_tipo_de_conceito_codigo";

    public const string TipoDeConceitoValorRotuloUnico = "ux_opcoes_do_tipo_de_conceito_rotulo";

    public const string TipoDeConceitoValorPosicaoUnica = "ux_opcoes_do_tipo_de_conceito_posicao";

    public const string TipoDeEtapaConceitoAdmitidoChave = "pk_conceitos_do_tipo_de_etapa";

    public const string TipoDeBancaCodigoAtivos = "ux_tipos_de_banca_codigo_ativos";

    public const string TipoDeDeficienciaCodigoAtivos = "ux_tipos_de_deficiencia_codigo_ativos";

    public const string TipoDeDeficienciaNomeAtivos = "ux_tipos_de_deficiencia_nome_ativos";

    public const string TipoDeDocumentoCodigoAtivos = "ux_tipos_de_documento_codigo_ativos";

    public const string TermoDeConsentimentoCodigoAtivos = "ux_termos_de_consentimento_codigo_ativos";

    public const string VersaoDoTermoNumeroUnico = "ux_versoes_do_termo_consentimento_termo_numero";
}
