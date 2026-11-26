namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConsequenciasDoDescumprimento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel sobre as consequências do descumprimento.</summary>
internal static class ItensDasConsequenciasDoDescumprimento
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatosDeModalidade) =>
    [
        new ItemConformidade("exigencia_acrescimo_nao_oferecido", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: RETIRA_O_ACRESCIMO com acréscimo oferecido (exigência)", CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarAcrescimo(configuracao, "documentosExigidos") && !CoerenciaDaConsequenciaDoDescumprimento.AcrescimoDasExigencias(configuracao).Any()),
        new ItemConformidade("grupo_acrescimo_nao_oferecido", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: RETIRA_O_ACRESCIMO com acréscimo oferecido (grupo)", CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarAcrescimo(configuracao, "documentosExigidos.regrasDeComprovacao") && !CoerenciaDaConsequenciaDoDescumprimento.AcrescimoDosGrupos(configuracao).Any()),
        new ItemConformidade("exigencia_sem_modalidade_em_questao", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: modalidade em questão (exigência)", CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarCoerencia(configuracao, "documentosExigidos") && !CoerenciaDaConsequenciaDoDescumprimento.SemModalidadeDasExigencias(configuracao, fatosDeModalidade).Any()),
        new ItemConformidade("grupo_sem_modalidade_em_questao", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: modalidade em questão (grupo)", CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarCoerencia(configuracao, "documentosExigidos.regrasDeComprovacao") && !CoerenciaDaConsequenciaDoDescumprimento.SemModalidadeDosGrupos(configuracao, fatosDeModalidade).Any()),
        new ItemConformidade("exigencia_consequencia_incoerente_com_acao_da_vaga", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: coerente com a ação da vaga (exigência)", CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarCoerencia(configuracao, "documentosExigidos") && !CoerenciaDaConsequenciaDoDescumprimento.CoerenciaDasExigencias(configuracao, fatosDeModalidade).Any()),
        new ItemConformidade("grupo_consequencia_incoerente_com_acao_da_vaga", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: coerente com a ação da vaga (grupo)", CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarCoerencia(configuracao, "documentosExigidos.regrasDeComprovacao") && !CoerenciaDaConsequenciaDoDescumprimento.CoerenciaDosGrupos(configuracao, fatosDeModalidade).Any()),
        new ItemConformidade("exigencia_retira_a_modalidade_sem_ampla_concorrencia", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: ampla concorrência ofertada (exigência)", CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarCoerencia(configuracao, "documentosExigidos") && !CoerenciaDaConsequenciaDoDescumprimento.SemAmplaDasExigencias(configuracao, fatosDeModalidade).Any()),
        new ItemConformidade("grupo_retira_a_modalidade_sem_ampla_concorrencia", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: ampla concorrência ofertada (grupo)", CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarCoerencia(configuracao, "documentosExigidos.regrasDeComprovacao") && !CoerenciaDaConsequenciaDoDescumprimento.SemAmplaDosGrupos(configuracao, fatosDeModalidade).Any()),
        new ItemConformidade("modalidade_retira_a_modalidade_sem_ampla_concorrencia", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: ampla concorrência ofertada (modalidade)", CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarCoerencia(configuracao, "distribuicaoVagas") && !CoerenciaDaConsequenciaDoDescumprimento.SemAmplaDasModalidades(configuracao).Any()),
    ];
}
