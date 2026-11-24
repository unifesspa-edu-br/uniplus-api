namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Recusas da configuração de classificação: arredondamento, eliminação e quadro de pesos por área do ENEM.
/// </summary>
internal static class ErrosDaClassificacao
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        .. DoQueNaoSeAplicaNaClassificacaoImportada(),
        .. DasEliminacoesDeclaradas(),
        .. DoCalculoLocal(),
        .. DaResolucaoDePesosPorArea(),
        .. DoGrupoDoQuadroDePesos(),
        .. DaDefinicaoDaRegraDeEliminacao(),
        .. DaEliminacaoNoProcesso(),
    ];

    /// <summary>Arredondamento, escala e eliminação locais numa classificação importada.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DoQueNaoSeAplicaNaClassificacaoImportada() =>
    [
        new("ConfiguracaoClassificacao.ArredondamentoIndevido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.arredondamento_indevido", "Arredondamento local não se aplica quando a classificação é importada")),
        new("ConfiguracaoClassificacao.CasasArredondamentoIndevido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.casas_arredondamento_indevido", "Casas de arredondamento não se aplicam quando a classificação é importada")),
        new("ConfiguracaoClassificacao.EscalaNotaFinalInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.escala_nota_final_invalida", "A escala da nota final deve ser maior que zero")),
        new("ConfiguracaoClassificacao.EscalaNotaFinalIndevida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.escala_nota_final_indevida", "A escala da nota final não se aplica quando a classificação é importada")),
        new("ConfiguracaoClassificacao.EliminacaoIndevida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.eliminacao_indevida", "Regras de eliminação não se aplicam quando a classificação é importada")),
    ];

    /// <summary>Eliminações e cortes declarados mais de uma vez ou fora do quadro.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DasEliminacoesDeclaradas() =>
    [
        new("ConfiguracaoClassificacao.FaltaEmDiaDeProvaEnemRepetida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.falta_em_dia_de_prova_enem_repetida", "A eliminação por falta em dia de prova do ENEM só pode ser declarada uma vez")),
        new("ConfiguracaoClassificacao.AusenciaNaEtapaRepetida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.ausencia_na_etapa_repetida", "A eliminação por ausência só pode ser declarada uma vez para cada etapa")),
        new("ConfiguracaoClassificacao.CorteEmAreaRepetido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.corte_em_area_repetido", "A mesma área tem mais de um corte")),
        new("ConfiguracaoClassificacao.CorteEmAreaForaDoQuadro", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.corte_em_area_fora_do_quadro", "A área do corte não está em todos os grupos do quadro de pesos por área")),
    ];

    /// <summary>Arredondamento obrigatório do cálculo local e a regra dele no catálogo.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DoCalculoLocal() =>
    [
        new("ConfiguracaoClassificacao.ArredondamentoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.arredondamento_obrigatorio", "Cálculo local exige regra de arredondamento")),
        new("ConfiguracaoClassificacao.CasasArredondamentoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.casas_arredondamento_obrigatorio", "Cálculo local exige casas decimais de arredondamento maior que zero")),
        new("ConfiguracaoClassificacao.RegraNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.regra_nao_encontrada", "Regra não encontrada no catálogo de regras")),
        new("ConfiguracaoClassificacao.RegraTipoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.regra_tipo_invalido", "A regra referenciada não é do tipo esperado")),
        new("ConfiguracaoClassificacao.RegraArredondamentoVersaoObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.regra_arredondamento_versao_obrigatoria", "Versão da regra de arredondamento é obrigatória quando o código é informado")),
    ];

    /// <summary>Resolução de Pesos por Área declarada e congelada na classificação baseada em ENEM.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaResolucaoDePesosPorArea() =>
    [
        new("ConfiguracaoClassificacao.ResolucaoPesoAreaEnemObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.resolucao_peso_area_enem_obrigatoria", "A classificação baseada em ENEM com cálculo local exige a resolução de Pesos por Área")),
        new("ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIndevida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.resolucao_peso_area_enem_indevida", "A resolução de Pesos por Área só se aplica à classificação baseada em ENEM com cálculo local")),
        new("ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.resolucao_peso_area_enem_invalida", "A resolução de Pesos por Área excede o tamanho permitido ou contém caractere de controle, de formatação ou de quebra de linha")),
        new("ConfiguracaoClassificacao.ResolucaoPesoAreaEnemNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.resolucao_peso_area_enem_nao_encontrada", "A resolução de Pesos por Área informada não tem pesos cadastrados")),
        new("ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIncompleta", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.resolucao_peso_area_enem_incompleta", "A resolução de Pesos por Área não tem pesos para todos os grupos de área")),
        new("ConfiguracaoClassificacao.QuadroPesoAreaEnemVazio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.quadro_peso_area_enem_vazio", "A resolução de Pesos por Área declarada não tem nenhum grupo de área congelado")),
        new("ConfiguracaoClassificacao.QuadroPesoAreaEnemGrupoRepetido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.quadro_peso_area_enem_grupo_repetido", "O quadro de pesos por área repete um grupo de área")),
    ];

    /// <summary>Base legal, áreas, pesos e corte de cada grupo do quadro de pesos por área.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DoGrupoDoQuadroDePesos() =>
    [
        new("GrupoPesoAreaEnemCongelado.BaseLegalObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.grupo_peso_area_enem_congelado.base_legal_obrigatoria", "A base legal do grupo no quadro de pesos por área é obrigatória")),
        new("GrupoPesoAreaEnemCongelado.BaseLegalInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.grupo_peso_area_enem_congelado.base_legal_invalida", "A base legal do grupo no quadro de pesos por área excede o tamanho permitido ou contém o caractere nulo ou caractere que não é texto")),
        new("GrupoPesoAreaEnemCongelado.AreaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.grupo_peso_area_enem_congelado.area_invalida", "Área do quadro de pesos por área sem código ou rótulo válido")),
        new("GrupoPesoAreaEnemCongelado.AreaRepetida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.grupo_peso_area_enem_congelado.area_repetida", "A mesma área aparece mais de uma vez no grupo do quadro de pesos por área")),
        new("GrupoPesoAreaEnemCongelado.PesoNegativo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.grupo_peso_area_enem_congelado.peso_negativo", "O peso de uma área do quadro de pesos por área não pode ser negativo")),
        new("GrupoPesoAreaEnemCongelado.PesosSomamZero", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.grupo_peso_area_enem_congelado.pesos_somam_zero", "Os pesos das áreas do grupo do quadro de pesos por área não podem somar zero")),
        new("GrupoPesoAreaEnemCongelado.CorteForaDaFaixa", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.grupo_peso_area_enem_congelado.corte_fora_da_faixa", "O corte de uma área do quadro de pesos por área deve estar entre 0 e 1000")),
        new("GrupoPesoAreaEnemCongelado.SemAreas", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.grupo_peso_area_enem_congelado.sem_areas", "Cada grupo do quadro de pesos por área precisa de ao menos uma área")),
    ];

    /// <summary>Parâmetros e regra de cada eliminação.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaDefinicaoDaRegraDeEliminacao() =>
    [
        new("RegraEliminacao.ParametrosIncompativeisComRegra", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_eliminacao.parametros_incompativeis_com_regra", "Os parâmetros informados não correspondem à regra referenciada")),
        new("RegraEliminacao.NotaMinimaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_eliminacao.nota_minima_invalida", "A nota mínima da eliminação deve ser não negativa")),
        new("RegraEliminacao.MinimoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_eliminacao.minimo_invalido", "O mínimo do corte em área deve ser não negativo")),
        new("RegraEliminacao.AreaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_eliminacao.area_invalida", "A área do corte não é um código de área do ENEM bem formado")),
        new("RegraEliminacao.RegraNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_eliminacao.regra_nao_encontrada", "Regra de eliminação não encontrada no catálogo de regras")),
        new("RegraEliminacao.RegraTipoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_eliminacao.regra_tipo_invalido", "A regra referenciada não é do tipo regra_eliminacao")),
        new("RegraEliminacao.EtapaRefENotaMinimaObrigatorios", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_eliminacao.etapa_ref_e_nota_minima_obrigatorios", "EtapaRef e NotaMinima são obrigatórios para a regra ELIMINACAO_NOTA_MINIMA_ETAPA")),
        new("RegraEliminacao.EtapaRefObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_eliminacao.etapa_ref_obrigatorio", "EtapaRef é obrigatório para a regra ELIMINACAO_AUSENCIA_NA_ETAPA")),
        new("RegraEliminacao.AreaEMinimoObrigatorios", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_eliminacao.area_e_minimo_obrigatorios", "AreaCodigo e Minimo são obrigatórios para a regra ELIMINACAO_CORTE_EM_AREA")),
    ];

    /// <summary>Etapa e classificação que a regra de eliminação exige do processo.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaEliminacaoNoProcesso() =>
    [
        new("ProcessoSeletivo.EtapaRefEliminacaoInexistente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_ref_eliminacao_inexistente", "A regra de eliminação referencia uma etapa que não existe neste processo")),
        new("ProcessoSeletivo.AusenciaNaEtapaDoEnem", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.ausencia_na_etapa_do_enem", "A eliminação por ausência não se aplica à etapa de nota do ENEM")),
        new("ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.eliminacao_enem_fora_de_processo_enem", "A regra de eliminação só se aplica quando a classificação está configurada como baseada em ENEM")),
    ];
}
