namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed partial class EnvelopeFechadoE2ETests
{
    /// <summary>
    /// O conjunto exato de itens que este corpus produz: remover um item do checklist, ou
    /// acrescentar um item indevido marcado ok, muda este conjunto.
    /// </summary>
    private static readonly string[] ItensEsperadosDoChecklist =
    [
        // ── PendenciaDeConformidade (itens estruturais originais) ──
        "atendimento_especializado_ausente",
        "distribuicao_vagas_ausente",
        "distribuicao_vagas_oferta_sem_nome",
        "opcoes_de_curso_nao_declaradas",
        "opcoes_de_curso_acima_das_ofertas",
        "classificacao_ausente",
        "cronograma_fases_ausente",
        "taxa_inscricao_nao_declarada",
        "taxa_inscricao_sem_fundamento_de_isencao",
        "exigencias_base_legal_nao_resolvida",
        "bonus_regional_nao_declarado",
        "bonus_regional_fato_de_elegibilidade",
        "criterios_desempate_ausentes",
        "classificacao_divisor_media_invalido",
        "classificacao_escala_nota_final_nao_declarada",
        "etapa_com_nota_sem_nota_maxima",
        "etapa_sem_nota_com_escala",
        "etapa_do_enem_com_escala",
        "nota_minima_da_regra_acima_da_nota_maxima",
        "etapa_com_nota_sem_forma_de_avaliacao",
        "etapa_sem_nota_com_forma_de_avaliacao",
        "etapa_sem_nota_com_itens_de_avaliacao",
        "etapa_com_itens_fora_da_nota_maxima",
        "etapa_humana_sem_regra_de_consolidacao",
        "etapa_humana_sem_passo_do_lancamento",
        "etapa_humana_sem_banca",
        "classificacao_resolucao_peso_area_enem_ausente",
        "classificacao_edicao_enem_nao_declarada",
        "distribuicao_vagas_oferta_sem_grupo_area_enem",
        "classificacao_grupo_area_enem_da_oferta_fora_do_quadro",

        // ── Gates que a raiz aplica antes do agregador genérico ──
        "identificador_legivel_nao_declarado",
        "localidade_nao_declarada",
        "algoritmo_contagem_prazo_nao_declarado",
        "calendario_vigente_ausente",
        "fuso_institucional_nao_reconhecido",
        "cascata_pendente",
        // ── PendenciaDoCronograma ──
        "cronograma_fase_agrupadora_sem_etapa_pontuada",
        "cronograma_etapa_declara_a_fase",
        "cronograma_inscricao_propria_sem_fase_de_coleta",
        "cronograma_fase_que_coleta_inscricao_sem_janela",
        "cronograma_fase_sem_unidade_responsavel",
        "cronograma_janela_de_isencao",
        "cronograma_vagas_sem_fase_que_produz_resultado",
        "cronograma_conclusao_do_ciclo_recursal",
        "cronograma_ancora_do_recurso",
        "cronograma_ancora_do_recurso_da_etapa",
        "cronograma_recorte_de_competencia_das_bancas",
        "cronograma_etapa_fora_da_janela_da_fase",
        "cronograma_resultado_declarado_com_papel",
        "cronograma_tipo_que_congela_nao_eh_resultado",
        "cronograma_tipo_de_ato_unico_declarado_uma_vez",
        // ── PendenciaDaCascata, detalhamento por razão ──
        "cascata_modalidade_fora_do_regime_federal",
        "cascata_origem_ausente",
        "cascata_fallback_nao_ofertado",
        "cascata_origem_nao_segue_cascata",
        "cascata_destino_desconhecido",
        // ── PendenciaPreCanonicalizacao ──
        "exigencia_condicional_vazia_determina_resultado",
        "exigencia_finalidade_incoerente_com_a_fase",
        "exigencia_fato_comprovado_fora_do_processo",
        "exigencia_menos_protegida_que_fato_comprovado",
        "exigencia_remove_vantagem_sem_vantagem_viva",
        "exigencia_consequencia_incoerente_com_acao_da_vaga",
        "grupo_remove_vantagem_sem_vantagem_viva",
        "grupo_consequencia_incoerente_com_acao_da_vaga",
        "referencia_temporal_ausente_com_gatilho_etario",
        "referencia_temporal_fase_fora_do_cronograma",
        "referencia_temporal_extremo_da_fase_ausente",
        "referencia_temporal_fim_inscricao_indisponivel",
        "modalidade_regra_cita_fato_nao_coletado",
        "derivacao_fatos_citados_inexistentes",
        "formulario_fato_ausente_de_versao_publicada",
        "formulario_campo_opcional_alimenta_regra",
        "exigencia_repete_por_grupo_do_formulario",
        "formulario_edicao_enem_sem_classificacao_enem",
        "fato_coletavel_sem_valores_ofertados",
        "fato_coletavel_municipio_citado_fora_da_area_do_bonus",
        "termo_exigido_sem_forma_de_aceite",
        "formulario_inscricao_ausente",
        "formulario_inscricao_sem_conjunto_basico",
        "formulario_inscricao_sem_edicao_enem",
        "formulario_fase_incoerente",
        "formulario_isencao_sem_taxa",
        "formulario_item_fora_de_secao",
        "formulario_itens_em_excesso",
        "modalidade_regra_incoerente_com_a_natureza",
        "modalidade_remanejamento_incoerente_com_a_natureza",
        "modalidade_cotas_da_lei_diferentes_entre_ofertas",
        "modalidade_reserva_pcd_sem_cota_pcd_da_lei",
        "derivacao_cota_e_acao_afirmativa_juntas",
        "grafo_dependencia_com_ciclo",
        "criterios_desempate_em_excesso",
        "desempate_area_enem_areas_mal_formadas",
        "desempate_area_enem_citada_por_dois_criterios",
        "desempate_area_enem_sem_quadro",
        "desempate_area_enem_fora_do_quadro",
    ];
}
