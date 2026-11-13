namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel sobre as exigências documentais e a consequência de indeferimento, na ordem do gate.</summary>
internal static class ItensDasExigenciasDocumentais
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(EstadoDaConfiguracao estado, FatosDeModalidade fatosDeModalidade, ProtecaoVigenteDasExigencias? protecao) =>
    [
        new ItemConformidade("exigencia_condicional_vazia_determina_resultado", DimensaoConformidade.ExigenciasDocumentais, "Exigência documental: sem CONDICIONAL vazia que determina resultado", PendenciasDasExigencias.PendenciaDasExigenciasDocumentais(estado) is null),
        new ItemConformidade("exigencia_finalidade_incoerente_com_a_fase", DimensaoConformidade.ExigenciasDocumentais, "Exigência documental: declara o formulário a que pertence, entre os que a sua fase responde", PendenciasDasExigencias.PendenciaDaFinalidadeDasExigencias(estado) is null),
        new ItemConformidade("exigencia_fato_comprovado_fora_do_processo", DimensaoConformidade.ExigenciasDocumentais, "Exigência documental: comprova só fatos que o processo coleta ou que são derivados congelados na publicação", ProtecaoDasExigencias.PendenciaDoFatoComprovadoForaDoProcesso(estado, protecao) is null),
        new ItemConformidade("exigencia_menos_protegida_que_fato_comprovado", DimensaoConformidade.ExigenciasDocumentais, "Exigência documental: o tipo de documento é tão protegido quanto o mais protegido dos fatos que a exigência comprova", ProtecaoDasExigencias.PendenciaDaProtecaoDasExigencias(estado, protecao) is null),
        new ItemConformidade("exigencia_remove_vantagem_sem_vantagem_viva", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: REMOVE_VANTAGEM com vantagem viva (exigência)", !ConsequenciaDeIndeferimento.ExisteExigenciaRemoveVantagemSemVantagemViva(estado)),
        new ItemConformidade("exigencia_consequencia_incoerente_com_acao_da_vaga", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: coerente com a ação da vaga (exigência)", !ConsequenciaDeIndeferimento.ExisteExigenciaConsequenciaIncoerenteComAcaoDaVaga(estado, fatosDeModalidade)),
        new ItemConformidade("grupo_remove_vantagem_sem_vantagem_viva", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: REMOVE_VANTAGEM com vantagem viva (grupo)", !ConsequenciaDeIndeferimento.ExisteGrupoRemoveVantagemSemVantagemViva(estado)),
        new ItemConformidade("grupo_consequencia_incoerente_com_acao_da_vaga", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: coerente com a ação da vaga (grupo)", !ConsequenciaDeIndeferimento.ExisteGrupoConsequenciaIncoerenteComAcaoDaVaga(estado, fatosDeModalidade)),
    ];
}
