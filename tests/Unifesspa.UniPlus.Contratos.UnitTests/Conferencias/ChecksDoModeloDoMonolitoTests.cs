namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;


/// <summary>
/// Fixa o nome e o texto SQL de cada restrição de verificação das tabelas de enum com código de contrato. A leitura é do
/// modelo de design-time do <c>MonolitoDbContext</c> (o que o EF usa para gerar a migration, e que guarda os CHECKs que o
/// modelo de execução pode descartar) e do instantâneo da migration. O texto fica escrito aqui, literal, para que a troca
/// da forma de montá-lo na configuração não mude o que o banco recebe sem que este teste acuse.
/// </summary>
public sealed class ChecksDoModeloDoMonolitoTests
{
    private static readonly RestricaoEsperada[] Esperadas =
    [
        new("dados_do_candidato", "ck_dados_do_candidato_classificacao_minima_do_tipo", "(tipo NOT IN ('TEXTO', 'DATA', 'ENDERECO') AND classificacao_protecao <> 'IDENTIFICADOR') OR (tipo = 'TEXTO' AND classificacao_protecao IN ('PESSOAL', 'IDENTIFICADOR', 'SENSIVEL')) OR (tipo IN ('DATA', 'ENDERECO') AND classificacao_protecao IN ('PESSOAL', 'SENSIVEL'))"),
        new("dados_do_candidato", "ck_dados_do_candidato_classificacao_protecao", "classificacao_protecao IN ('PUBLICO', 'INTERNO', 'PESSOAL', 'IDENTIFICADOR', 'SENSIVEL')"),
        new("dados_do_candidato", "ck_dados_do_candidato_hipotese_legal", "hipotese_legal IN ('CONSENTIMENTO', 'CUMPRIMENTO_OBRIGACAO_LEGAL', 'EXECUCAO_POLITICAS_PUBLICAS', 'ESTUDOS_POR_ORGAO_DE_PESQUISA', 'EXECUCAO_CONTRATO', 'EXERCICIO_REGULAR_DE_DIREITOS', 'PROTECAO_DA_VIDA', 'TUTELA_DA_SAUDE', 'INTERESSE_LEGITIMO', 'PROTECAO_DO_CREDITO', 'PREVENCAO_A_FRAUDE')"),
        new("tipos_de_fase", "ck_tipos_de_fase_origem_data", "origem_data IN ('NO_EDITAL', 'NA_EXECUCAO')"),
        new("modalidades", "ck_modalidades_acao_quando_indeferido", "acao_quando_indeferido IS NULL OR acao_quando_indeferido IN ('RECLASSIFICA_AC', 'RECLASSIFICA_CONFORME_EDITAL')"),
        new("modalidades", "ck_modalidades_composicao_vagas", "composicao_vagas IN ('RESTANTE_DAS_VAGAS_OFERTADAS', 'DENTRO_DA_RESERVA_LEGAL', 'RETIRADA_DE_OUTRA_MODALIDADE', 'SUPLEMENTAR_AO_TOTAL')"),
        new("modalidades", "ck_modalidades_natureza_legal", "natureza_legal IN ('COTA_RESERVADA', 'AMPLA', 'ACAO_AFIRMATIVA')"),
        new("modalidades", "ck_modalidades_regra_remanejamento", "regra_remanejamento IS NULL OR regra_remanejamento IN ('SEGUE_ORDEM_DE_REMANEJAMENTO', 'DESTINO_UNICO', 'CRUZADO')"),
        new("modalidades", "ck_modalidades_retirada_de_outra_modalidade_origem", "(composicao_vagas = 'RETIRADA_DE_OUTRA_MODALIDADE') = (composicao_origem IS NOT NULL)"),
        new("termos_de_consentimento", "ck_termos_de_consentimento_forma_aceite_rascunho", "forma_aceite_rascunho IN ('A_DEFINIR', 'REGISTRO_SEM_ENDERECO_IP', 'REGISTRO_COM_ENDERECO_IP')"),
        new("dias_nao_uteis", "ck_dias_nao_uteis_municipio_coerente", "(abrangencia = 'MUNICIPAL') = (municipio_ibge IS NOT NULL) AND (abrangencia = 'MUNICIPAL') = (municipio_nome IS NOT NULL) AND (abrangencia = 'MUNICIPAL') = (municipio_uf IS NOT NULL)"),
        new("dias_nao_uteis", "ck_dias_nao_uteis_uf_coerente", "(abrangencia = 'ESTADUAL') = (uf IS NOT NULL)"),
        new("versoes_do_termo_consentimento", "ck_versoes_do_termo_consentimento_forma_aceite", "forma_aceite IN ('A_DEFINIR', 'REGISTRO_SEM_ENDERECO_IP', 'REGISTRO_COM_ENDERECO_IP')"),
        new("ofertas_de_curso", "ck_ofertas_de_curso_base_legal_programa", "programa_de_oferta = 'REGULAR' OR base_legal IS NOT NULL"),
        new("ofertas_de_curso", "ck_ofertas_de_curso_formato_pedagogico", "formato_pedagogico IN ('PRESENCIAL', 'SEMIPRESENCIAL', 'EAD')"),
        new("ofertas_de_curso", "ck_ofertas_de_curso_funcionamento_regime_de_turno", "(regime_de_funcionamento <> 'INTENSIVO' OR regime_de_turno = 'INTEGRAL')"),
        new("ofertas_de_curso", "ck_ofertas_de_curso_programa_de_oferta", "programa_de_oferta IN ('REGULAR', 'FORMA_PARA', 'PARFOR', 'PRONERA', 'PEPETI', 'CONVENIO_OUTRO', 'OUTRO')"),
        new("ofertas_de_curso", "ck_ofertas_de_curso_regime_de_funcionamento", "regime_de_funcionamento IN ('INTENSIVO', 'EXTENSIVO', 'ALTERNANCIA_PEDAGOGICA')"),
        new("ofertas_de_curso", "ck_ofertas_de_curso_regime_de_turno", "regime_de_turno IN ('REGULAR', 'INTEGRAL')"),
        new("ofertas_de_curso", "ck_ofertas_de_curso_turnos_dominio", "turnos::text[] <@ ARRAY['MATUTINO', 'VESPERTINO', 'NOTURNO']::text[]"),
        new("ofertas_de_curso", "ck_ofertas_de_curso_turnos_regime", "coalesce(array_ndims(turnos), 0) = 1 AND coalesce(array_lower(turnos, 1), 1) = 1 AND ((regime_de_turno = 'REGULAR' AND cardinality(turnos) = 1) OR (regime_de_turno = 'INTEGRAL' AND cardinality(turnos) = 2 AND turnos[1] <> turnos[2]))"),
        new("enderecos_da_inscricao", "ck_enderecos_da_inscricao_origem", "origem IN ('INFORMADA', 'CALCULADA')"),
        new("regras_de_comprovacao", "ck_regras_de_comprovacao_forma_coerente", "(forma = 'DOCUMENTO' AND documento_exigido_id IS NOT NULL AND quantidade_minima IS NOT NULL AND consequencia IS NULL) OR (forma = 'TODOS_OS_ITENS' AND documento_exigido_id IS NULL AND quantidade_minima IS NULL AND consequencia IS NULL AND criterio_de_distincao IS NULL AND data_referencia IS NULL AND ocorrencias_esperadas IS NULL) OR (forma = 'QUANTIDADE_MINIMA_DE_ITENS' AND documento_exigido_id IS NULL AND quantidade_minima IS NOT NULL AND criterio_de_distincao IS NULL AND data_referencia IS NULL AND ocorrencias_esperadas IS NULL)"),
        new("regras_de_comprovacao", "ck_regras_de_comprovacao_criterio_de_distincao_coerente", "(criterio_de_distincao IS NULL AND data_referencia IS NULL AND ocorrencias_esperadas IS NULL) OR (criterio_de_distincao IS NOT NULL AND criterio_de_distincao IN ('COMPETENCIA_MENSAL', 'EXERCICIO_ANUAL') AND data_referencia IS NOT NULL AND ocorrencias_esperadas IS NULL) OR (criterio_de_distincao IS NOT NULL AND criterio_de_distincao = 'OCORRENCIA' AND data_referencia IS NULL AND (ocorrencias_esperadas IS NULL OR jsonb_array_length(ocorrencias_esperadas) > 0))"),
        new("avaliacoes_da_etapa", "ck_avaliacoes_da_etapa_situacao", "situacao IN ('APURADA', 'INSCRITO_SEM_LINHA', 'EM_CONFLITO')"),
        new("avaliacoes_da_etapa", "ck_avaliacoes_da_etapa_nota", "(situacao = 'APURADA') = (nota IS NOT NULL) AND (nota IS NULL OR nota BETWEEN 0 AND 1000)"),
        new("lancamentos_de_nota", "ck_lancamentos_de_nota_situacao", "situacao IN ('PRESENTE', 'AUSENTE')"),
        new("ocorrencias_da_importacao", "ck_ocorrencias_da_importacao_forma", "(tipo = 'INSCRITO_SEM_LINHA' AND inscricao_id IS NOT NULL AND numeros_das_linhas IS NULL) OR (tipo = 'LINHA_SEM_INSCRITO' AND inscricao_id IS NULL AND numeros_das_linhas IS NOT NULL AND cardinality(numeros_das_linhas) = 1) OR (tipo = 'CONFLITO' AND inscricao_id IS NOT NULL AND numeros_das_linhas IS NOT NULL AND cardinality(numeros_das_linhas) >= 2)"),
        new("tipos_conceito", "ck_tipos_conceito_forma", "forma IN ('NUMERICO', 'POR_OPCOES')"),
        new("tipos_conceito", "ck_tipos_conceito_parametros_da_forma", "(forma = 'NUMERICO' AND escala_minima IS NOT NULL AND escala_maxima IS NOT NULL AND casas_decimais IS NOT NULL AND escala_minima < escala_maxima AND casas_decimais BETWEEN 0 AND 4) OR (forma = 'POR_OPCOES' AND escala_minima IS NULL AND escala_maxima IS NULL AND casas_decimais IS NULL)"),
        new("tipos_de_fase", "ck_tipos_de_fase_codigo_canonico", "codigo IN ('INSCRICAO', 'SOLICITACAO_ISENCAO', 'HOMOLOGACAO_INSCRICOES', 'ENSALAMENTO', 'AVALIACAO', 'CLASSIFICACAO', 'RESULTADO_FINAL', 'HABILITACAO', 'HETEROIDENTIFICACAO', 'AVALIACAO_BIOPSICOSSOCIAL', 'MATRICULA', 'HOMOLOGACAO_RESULTADO_FINAL', 'LISTA_ESPERA', 'CHAMADA')"),
        new("tipos_de_fase", "ck_tipos_de_fase_complementacao", "permite_complementacao = false OR codigo IN ('HOMOLOGACAO_INSCRICOES')"),
        new("areas_dos_pesos_do_enem", "ck_areas_dos_pesos_do_enem_codigo", "codigo IN ('REDACAO', 'CIENCIAS_DA_NATUREZA', 'CIENCIAS_HUMANAS', 'LINGUAGENS', 'MATEMATICA')"),
        new("cursos", "ck_cursos_grupo_area_enem", "grupo_area_enem IS NULL OR grupo_area_enem IN ('TECNOLOGICA', 'HUMANISTICA_I', 'HUMANISTICA_II', 'SAUDE_E_BIOLOGICAS')"),
        new("pesos_por_area_do_enem", "ck_pesos_por_area_do_enem_grupo_curso", "grupo_curso IN ('TECNOLOGICA', 'HUMANISTICA_I', 'HUMANISTICA_II', 'SAUDE_E_BIOLOGICAS')"),
        new("decisoes_da_migracao", "ck_decisoes_da_migracao_envio", "(envio_id IS NULL) = (envio_etapa IS NULL) AND (envio_etapa IS NULL OR envio_etapa IN ('GESTAO_DE_ACESSOS', 'PROTECAO_DE_DADOS'))"),
        new("decisoes_da_migracao", "ck_decisoes_da_migracao_envio_de_origem", "(envio_de_origem_id IS NULL) = (envio_de_origem_etapa IS NULL) AND (envio_de_origem_etapa IS NULL OR envio_de_origem_etapa = 'GESTAO_DE_ACESSOS')"),
        new("decisoes_da_migracao", "ck_decisoes_da_migracao_ramo", "(ator = 'HUMANO' AND etapa IN ('GESTAO_DE_ACESSOS', 'PROTECAO_DE_DADOS') AND decisao IN ('MIGRAR', 'RECUSAR') AND decisor_emissor IS NOT NULL AND decisor_conta_id IS NOT NULL AND decisor_tem_concessao IS NOT NULL AND envio_de_origem_id IS NOT NULL AND (envio_etapa IS NULL OR envio_etapa = etapa) AND (etapa <> 'PROTECAO_DE_DADOS' OR envio_id IS NOT NULL) AND (etapa <> 'GESTAO_DE_ACESSOS' OR envio_id IS NULL OR envio_id = envio_de_origem_id)) OR (ator = 'SISTEMA' AND decisor_emissor IS NULL AND decisor_conta_id IS NULL AND decisor_nome IS NULL AND decisor_tem_concessao IS NULL AND envio_id IS NULL AND ((etapa = 'GESTAO_DE_ACESSOS' AND decisao = 'RETER' AND envio_de_origem_id IS NOT NULL) OR (etapa = 'SISTEMA' AND decisao IN ('NAO_MIGRADA', 'FORA_DA_LISTA'))))"),
        new("envios_de_decisoes", "ck_envios_de_decisoes_etapa", "etapa IN ('GESTAO_DE_ACESSOS', 'PROTECAO_DE_DADOS')"),
        new("eventos_de_bloqueio_do_perfil", "ck_eventos_de_bloqueio_do_perfil_desativacao", "tipo <> 'DESATIVACAO' OR (ator = 'HUMANO' AND ator_emissor IS NOT NULL AND ator_conta_id IS NOT NULL AND motivo IS NOT NULL AND desativacao_id IS NOT NULL AND suspensao_id IS NULL AND papel IS NULL AND origem IS NULL AND decisao_id IS NULL AND versao_que_entrou_id IS NULL AND permissao_codigo IS NULL)"),
        new("eventos_de_bloqueio_do_perfil", "ck_eventos_de_bloqueio_do_perfil_reativacao", "tipo <> 'REATIVACAO' OR (ator = 'HUMANO' AND ator_emissor IS NOT NULL AND ator_conta_id IS NOT NULL AND motivo IS NOT NULL AND desativacao_id IS NOT NULL AND suspensao_id IS NULL AND papel IS NULL AND origem IS NULL AND decisao_id IS NULL AND versao_que_entrou_id IS NULL AND permissao_codigo IS NULL)"),
        new("eventos_de_bloqueio_do_perfil", "ck_eventos_de_bloqueio_do_perfil_retomada", "tipo <> 'RETOMADA' OR (ator = 'SISTEMA' AND suspensao_id IS NOT NULL AND desativacao_id IS NULL AND ator_emissor IS NULL AND ator_conta_id IS NULL AND ator_nome IS NULL AND papel IS NULL AND motivo IS NULL AND origem IS NOT NULL AND ((origem = 'DECISAO_DA_ETAPA' AND decisao_id IS NOT NULL AND versao_que_entrou_id IS NULL AND permissao_codigo IS NULL) OR (origem = 'TROCA_DE_VIGENCIA' AND versao_que_entrou_id IS NOT NULL AND decisao_id IS NULL AND permissao_codigo IS NULL) OR (origem = 'RECLASSIFICACAO' AND permissao_codigo IS NOT NULL AND decisao_id IS NULL AND versao_que_entrou_id IS NULL)))"),
        new("eventos_de_bloqueio_do_perfil", "ck_eventos_de_bloqueio_do_perfil_suspensao", "tipo <> 'SUSPENSAO' OR (ator = 'HUMANO' AND ator_emissor IS NOT NULL AND ator_conta_id IS NOT NULL AND motivo IS NOT NULL AND suspensao_id IS NOT NULL AND desativacao_id IS NULL AND papel IS NOT NULL AND origem IS NULL AND decisao_id IS NULL AND versao_que_entrou_id IS NULL AND permissao_codigo IS NULL)"),
        new("eventos_de_bloqueio_do_perfil", "ck_eventos_de_bloqueio_do_perfil_tipo", "tipo IN ('SUSPENSAO', 'RETOMADA', 'DESATIVACAO', 'REATIVACAO')"),
        new("liberacoes_da_reclassificacao", "ck_liberacoes_da_reclassificacao_motivo", "(resultado = 'MANTIDA') = (motivo IS NOT NULL) AND resultado IN ('LIBERADA', 'MANTIDA')"),
        new("migracoes_da_concessao", "ck_migracoes_da_concessao_situacao", "situacao IN ('RETIDA', 'AGUARDANDO_PROTECAO_DE_DADOS', 'MIGRADA', 'RECUSADA', 'NAO_MIGRADA', 'FORA_DA_LISTA')"),
    ];

    public static TheoryData<string, string, string> RestricoesEsperadas()
    {
        TheoryData<string, string, string> dados = [];
        foreach (RestricaoEsperada esperada in Esperadas)
        {
            dados.Add(esperada.Tabela, esperada.Nome, esperada.Sql);
        }

        return dados;
    }

    [Fact(DisplayName = "A lista esperada tem as 46 restrições de enum com código de contrato")]
    public void ListaEsperada_TemAsRestricoes() =>
        Esperadas.Select(esperada => esperada.Nome).Should().OnlyHaveUniqueItems().And.HaveCount(46);

    [Theory(DisplayName = "O modelo de design-time guarda a restrição com o nome e o texto literais")]
    [MemberData(nameof(RestricoesEsperadas))]
    public void ModeloDeProjeto_GuardaARestricaoLiteral(string tabela, string nome, string sql) =>
        RestricoesDa(ModelosDoMonolito.Valor.Projeto, tabela).Should().ContainSingle(restricao => restricao.Nome == nome)
            .Which.Sql.Should().Be(sql);

    [Theory(DisplayName = "O instantâneo da migration guarda a restrição com o nome e o texto literais")]
    [MemberData(nameof(RestricoesEsperadas))]
    public void InstantaneoDaMigration_GuardaARestricaoLiteral(string tabela, string nome, string sql) =>
        RestricoesDa(ModelosDoMonolito.Valor.InstantaneoDaMigration, tabela).Should().ContainSingle(restricao => restricao.Nome == nome)
            .Which.Sql.Should().Be(sql);

    private static List<(string? Nome, string Sql)> RestricoesDa(IModel modelo, string tabela) =>
    [
        .. modelo.GetEntityTypes()
            .Where(entidade => entidade.GetTableName() == tabela)
            .SelectMany(entidade => entidade.GetCheckConstraints())
            .Select(restricao => (restricao.Name, restricao.Sql)),
    ];

    private sealed record RestricaoEsperada(string Tabela, string Nome, string Sql);
}
