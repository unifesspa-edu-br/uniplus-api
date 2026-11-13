namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel sobre a coleta dos fatos: referência temporal, derivação, citações dos formulários e fatos coletáveis.</summary>
internal static class ItensDaColetaDeFatos
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(EstadoDaConfiguracao estado, FatosDeModalidade fatosDeModalidade) =>
    [
        new ItemConformidade("referencia_temporal_ausente_com_gatilho_etario", DimensaoConformidade.ColetaDeFatos, "Referência temporal de fatos: configurada quando alguma regra cita a faixa etária", !ReferenciaTemporalDosFatos.ReferenciaTemporalFatosAusenteQuandoExigida(estado)),
        new ItemConformidade("referencia_temporal_fase_fora_do_cronograma", DimensaoConformidade.ColetaDeFatos, "Referência temporal de fatos: fase âncora pertence ao cronograma", !ReferenciaTemporalDosFatos.ReferenciaTemporalFatosFaseNaoPertenceAoCronograma(estado)),
        new ItemConformidade("referencia_temporal_extremo_da_fase_ausente", DimensaoConformidade.ColetaDeFatos, "Referência temporal de fatos: extremo da fase âncora definido", !ReferenciaTemporalDosFatos.ReferenciaTemporalFatosExtremoDaFaseAusente(estado)),
        new ItemConformidade("referencia_temporal_fim_inscricao_indisponivel", DimensaoConformidade.ColetaDeFatos, "Referência temporal de fatos: fase de coleta com Fim definido para FIM_INSCRICAO", !ReferenciaTemporalDosFatos.ReferenciaTemporalFatosFimInscricaoIndisponivel(estado)),
        new ItemConformidade("modalidade_regra_cita_fato_nao_coletado", DimensaoConformidade.DistribuicaoVagas, "Modalidades: a regra de aplicabilidade cita só fatos que o processo coleta ou deriva", PendenciasDosFatos.PendenciaDasRegrasDasModalidades(estado, fatosDeModalidade.MembroPorAgregado) is null),
        new ItemConformidade("derivacao_fatos_citados_inexistentes", DimensaoConformidade.ColetaDeFatos, "Regras de derivação: fatos citados existem no processo", PendenciasDosFatos.PendenciaDeFatosCitados(estado) is null),
        new ItemConformidade("formulario_fato_ausente_de_versao_publicada", DimensaoConformidade.ColetaDeFatos, "Formulários: na retificação, fato de outra finalidade só quando coletado em todas as versões publicadas", CitacoesDosFormularios.PendenciaDeFatoAusenteDeVersaoPublicada(estado) is null),
        new ItemConformidade("formulario_campo_opcional_alimenta_regra", DimensaoConformidade.ColetaDeFatos, "Formulários: campo que alimenta derivação, negação ou impedimento é obrigatório sempre que exibido", PendenciasDosFatos.PendenciaDeCampoOpcionalQueAlimentaRegra(estado) is null),
        new ItemConformidade("exigencia_repete_por_grupo_do_formulario", DimensaoConformidade.ExigenciasDocumentais, "Exigência documental: repete por grupo dos formulários, conhecido até a fase da exigência", PendenciasDasExigencias.PendenciaDaRepeticaoPorGrupo(estado) is null),
        new ItemConformidade("formulario_edicao_enem_sem_classificacao_enem", DimensaoConformidade.ColetaDeFatos, "Formulários: só pedem a edição do ENEM quando a classificação é baseada em ENEM", PendenciasDosFormularios.EdicaoDoEnemSemClassificacaoEnem(estado.Campos, estado.Classificacao) is null),
        new ItemConformidade("fato_coletavel_sem_valores_ofertados", DimensaoConformidade.ColetaDeFatos, "Fato coletável de escopo do processo: oferta declara ao menos um valor", PendenciasDosFatos.PendenciaDeFatoColetadoSemValoresOfertados(estado) is null),
        new ItemConformidade("fato_coletavel_municipio_citado_fora_da_area_do_bonus", DimensaoConformidade.ColetaDeFatos, "Fato com os municípios do bônus regional: condição cita só município da área", PendenciasDosFatos.PendenciaDeMunicipioDoBonusForaDaArea(estado) is null),
    ];
}
