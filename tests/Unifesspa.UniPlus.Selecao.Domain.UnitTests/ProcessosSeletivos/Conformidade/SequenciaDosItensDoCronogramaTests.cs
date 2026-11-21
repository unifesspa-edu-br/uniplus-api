namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Conformidade;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>Os itens do painel sobre o cronograma saem todos, uma vez cada, na ordem em que o painel os apresenta.</summary>
public sealed class SequenciaDosItensDoCronogramaTests
{
    private static readonly string[] SequenciaEsperada =
    [
        "cronograma_fases_ausente",
        "cronograma_fases_declaradas",
        "cronograma_fases_em_conjunto",
        "etapas_declaradas",
        "etapas_em_conjunto",
        "cronograma_fase_agrupadora_sem_etapa_pontuada",
        "cronograma_etapa_declara_a_fase",
        "cronograma_inscricao_propria_sem_fase_de_coleta",
        "cronograma_homologacao_das_inscricoes_exigida",
        "cronograma_homologacao_das_inscricoes_com_ciclo_recursal",
        "cronograma_homologacao_das_inscricoes_com_confirmacao_do_inep",
        "cronograma_fase_que_coleta_inscricao_sem_janela",
        "cronograma_fase_sem_unidade_responsavel",
        "cronograma_fase_sem_definitivo_sem_data_final",
        "cronograma_janela_de_isencao",
        "cronograma_vagas_sem_fase_que_produz_resultado",
        "cronograma_precedencia_entre_fases",
        "cronograma_conclusao_do_ciclo_recursal",
        "cronograma_ancora_do_recurso",
        "cronograma_ancora_do_recurso_da_etapa",
        "cronograma_candidatos_atendidos_das_bancas",
        "cronograma_procedimento_de_recurso",
        "cronograma_banca_de_recursos_sem_candidatos_atendidos",
        "cronograma_recurso_com_banca_que_julga",
        "cronograma_instancia_superior_admitida_pelo_tipo_de_banca",
        "cronograma_tipo_de_banca_com_o_minimo_de_bancas",
        "cronograma_banca_com_regra_de_deliberacao",
        "cronograma_banca_com_o_minimo_de_integrantes",
        "etapa_correcao_independente_com_dois_corretores",
        "cronograma_etapa_fora_da_janela_da_fase",
        "cronograma_resultado_declarado_com_papel",
        "cronograma_tipo_que_congela_nao_eh_resultado",
        "cronograma_tipo_de_ato_unico_declarado_uma_vez",
    ];

    [Fact(DisplayName = "Os itens do cronograma saem na ordem do painel, sem repetir nenhum")]
    public void Avaliar_ProcessoConforme_DevolveOsItensNaOrdemDoPainel()
    {
        ConfiguracaoDeclarada configuracao = ApoioDaPublicacaoDoProcessoSeletivo.NovoProcessoConforme().Declaracao();

        string[] codigos = [.. ItensDoCronograma.Avaliar(configuracao, CadastrosDeTeste.Padrao).Select(static item => item.Codigo)];

        codigos.Should().Equal(SequenciaEsperada);
        codigos.Distinct().Should().HaveCount(codigos.Length);
    }
}
