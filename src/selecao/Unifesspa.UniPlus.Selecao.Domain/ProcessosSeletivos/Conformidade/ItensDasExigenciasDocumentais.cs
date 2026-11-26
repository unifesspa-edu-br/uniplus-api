namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel sobre as exigências documentais e a consequência de indeferimento, na ordem do gate.</summary>
internal static class ItensDasExigenciasDocumentais
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatosDeModalidade, ProtecaoVigenteDasExigencias? protecao) =>
    [
        new ItemConformidade("exigencia_prazo_de_emissao_escolhido", DimensaoConformidade.ExigenciasDocumentais, "Exigência documental: prazo de emissão escolhido", SemCausa(configuracao, "PrazoDeEmissao.EscolhaNaoDeclarada")),
        new ItemConformidade("exigencia_validade_escolhida", DimensaoConformidade.ExigenciasDocumentais, "Exigência documental: validade escolhida", SemCausa(configuracao, "ValidadeDoDocumento.EscolhaNaoDeclarada")),
        new ItemConformidade("exigencia_data_de_referencia_resolvida", DimensaoConformidade.ExigenciasDocumentais, "Exigência documental: data de referência definida", ReferenciasResolvidas(configuracao)),
        new ItemConformidade("exigencia_condicional_vazia_determina_resultado", DimensaoConformidade.ExigenciasDocumentais, "Exigência documental: sem CONDICIONAL vazia que determina resultado", PendenciasDasExigencias.PendenciaDasExigenciasDocumentais(configuracao) is null),
        new ItemConformidade("exigencia_no_formulario_da_sua_fase", DimensaoConformidade.ExigenciasDocumentais, "Exigência documental: declara o formulário a que pertence, entre os que a sua fase responde", PendenciasDasExigencias.PendenciaDoFormularioDasExigencias(configuracao) is null),
        new ItemConformidade("exigencia_fato_comprovado_fora_do_processo", DimensaoConformidade.ExigenciasDocumentais, "Exigência documental: comprova só fatos que o processo coleta ou que são derivados congelados na publicação", ProtecaoDasExigencias.PendenciaDoFatoComprovadoForaDoProcesso(configuracao, protecao) is null),
        new ItemConformidade("exigencia_menos_protegida_que_fato_comprovado", DimensaoConformidade.ExigenciasDocumentais, "Exigência documental: o tipo de documento é tão protegido quanto o mais protegido dos fatos que a exigência comprova", ProtecaoDasExigencias.PendenciaDaProtecaoDasExigencias(configuracao, protecao) is null),
        .. ItensDasConsequenciasDoDescumprimento.Avaliar(configuracao, fatosDeModalidade),
    ];
    private static bool SemCausa(ConfiguracaoDeclarada configuracao, string codigo) =>
        !DatasDeReferenciaNoCronograma.ConferirCompletude(configuracao.Cronograma.CronogramaFases, configuracao.Exigencias.DocumentosExigidos)
            .Any(causa => causa.Error.Code == codigo);

    private static bool ReferenciasResolvidas(ConfiguracaoDeclarada configuracao) =>
        DatasDeReferenciaNoCronograma.ConferirConjunto(configuracao.Cronograma.CronogramaFases, configuracao.Exigencias.DocumentosExigidos).Count == 0
        && SemCausa(configuracao, "DataDeReferenciaDoDocumento.DataDaFaseIndefinida")
        && SemCausa(configuracao, "DataDeReferenciaDoDocumento.FimDasInscricoesIndefinido");
}
