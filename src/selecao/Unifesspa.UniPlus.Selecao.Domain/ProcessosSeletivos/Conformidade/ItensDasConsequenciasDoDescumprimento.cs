namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConsequenciasDoDescumprimento;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel sobre as consequências do descumprimento.</summary>
internal static class ItensDasConsequenciasDoDescumprimento
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatosDeModalidade) =>
    [
        new ItemConformidade("exigencia_acrescimo_nao_oferecido", DimensaoConformidade.ExigenciasDocumentais, "Descumprimento: retirada de acréscimo somente quando oferecido (exigência)", CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarAcrescimo(configuracao, "documentosExigidos") && !CoerenciaDaConsequenciaDoDescumprimento.AcrescimoDasExigencias(configuracao).Any()),
        new ItemConformidade("grupo_acrescimo_nao_oferecido", DimensaoConformidade.ExigenciasDocumentais, "Descumprimento: retirada de acréscimo somente quando oferecido (grupo)", CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarAcrescimo(configuracao, "documentosExigidos.regrasDeComprovacao") && !CoerenciaDaConsequenciaDoDescumprimento.AcrescimoDosGrupos(configuracao).Any()),
        new ItemConformidade("exigencia_sem_modalidade_em_questao", DimensaoConformidade.ExigenciasDocumentais, "Descumprimento: retirada da modalidade apenas quando há modalidade em questão (exigência)", CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarCoerencia(configuracao, "documentosExigidos") && !CoerenciaDaConsequenciaDoDescumprimento.SemModalidadeDasExigencias(configuracao, fatosDeModalidade).Any()),
        new ItemConformidade("grupo_sem_modalidade_em_questao", DimensaoConformidade.ExigenciasDocumentais, "Descumprimento: retirada da modalidade apenas quando há modalidade em questão (grupo)", CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarCoerencia(configuracao, "documentosExigidos.regrasDeComprovacao") && !CoerenciaDaConsequenciaDoDescumprimento.SemModalidadeDosGrupos(configuracao, fatosDeModalidade).Any()),
        new ItemConformidade("exigencia_consequencia_incoerente_com_acao_da_vaga", DimensaoConformidade.ExigenciasDocumentais, "Descumprimento: consequência coerente com a ação da vaga (exigência)", CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarCoerencia(configuracao, "documentosExigidos") && !CoerenciaDaConsequenciaDoDescumprimento.CoerenciaDasExigencias(configuracao, fatosDeModalidade).Any()),
        new ItemConformidade("grupo_consequencia_incoerente_com_acao_da_vaga", DimensaoConformidade.ExigenciasDocumentais, "Descumprimento: consequência coerente com a ação da vaga (grupo)", CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarCoerencia(configuracao, "documentosExigidos.regrasDeComprovacao") && !CoerenciaDaConsequenciaDoDescumprimento.CoerenciaDosGrupos(configuracao, fatosDeModalidade).Any()),
        new ItemConformidade("exigencia_retira_a_modalidade_sem_ampla_concorrencia", DimensaoConformidade.ExigenciasDocumentais, "Descumprimento: ampla concorrência ofertada para retirada da modalidade (exigência)", CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarCoerencia(configuracao, "documentosExigidos") && !CoerenciaDaConsequenciaDoDescumprimento.SemAmplaDasExigencias(configuracao, fatosDeModalidade).Any()),
        new ItemConformidade("grupo_retira_a_modalidade_sem_ampla_concorrencia", DimensaoConformidade.ExigenciasDocumentais, "Descumprimento: ampla concorrência ofertada para retirada da modalidade (grupo)", CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarCoerencia(configuracao, "documentosExigidos.regrasDeComprovacao") && !CoerenciaDaConsequenciaDoDescumprimento.SemAmplaDosGrupos(configuracao, fatosDeModalidade).Any()),
        new ItemConformidade("modalidade_retira_a_modalidade_sem_ampla_concorrencia", DimensaoConformidade.ExigenciasDocumentais, "Descumprimento: ampla concorrência ofertada para retirada da modalidade (modalidade)", CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarCoerencia(configuracao, "distribuicaoVagas") && !CoerenciaDaConsequenciaDoDescumprimento.SemAmplaDasModalidades(configuracao).Any()),
        .. ItensDasBancas(configuracao, fatosDeModalidade),
        .. ItensDosFormularios(configuracao, fatosDeModalidade),
    ];

    private static IReadOnlyList<ItemConformidade> ItensDasBancas(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos)
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(configuracao.Cronograma.CronogramaFases, configuracao.Origem);
        EtapasDoProcesso etapas = EtapasDoProcesso.Das(configuracao.Avaliacao.Etapas, configuracao.Origem);
        return [
            new ItemConformidade("banca_acrescimo_nao_oferecido", DimensaoConformidade.ExigenciasDocumentais, "Descumprimento: retirada de acréscimo somente quando oferecido (banca)", PodeAvaliarAcrescimoDasBancas(configuracao) && !ConsequenciasDasBancas.Acrescimo(configuracao, cronograma, etapas).Any()),
            new ItemConformidade("banca_sem_modalidade_em_questao", DimensaoConformidade.ExigenciasDocumentais, "Descumprimento: retirada da modalidade apenas quando há modalidade em questão (banca)", PodeAvaliarCoerenciaDasBancas(configuracao) && !ConsequenciasDasBancas.SemModalidade(configuracao, fatos, cronograma, etapas).Any()),
            new ItemConformidade("banca_retira_a_modalidade_sem_ampla_concorrencia", DimensaoConformidade.ExigenciasDocumentais, "Descumprimento: ampla concorrência ofertada para retirada da modalidade (banca)", PodeAvaliarCoerenciaDasBancas(configuracao) && !ConsequenciasDasBancas.SemAmpla(configuracao, fatos, cronograma, etapas).Any()),
        ];
    }

    private static IReadOnlyList<ItemConformidade> ItensDosFormularios(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos)
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(configuracao.Cronograma.CronogramaFases, configuracao.Origem);
        bool declaracoes = !CoerenciaDaConsequenciaDoDescumprimento.Recusada(configuracao, "formularios");
        bool julgados = declaracoes && !ConsequenciasDosFormularios.ComFaseSemJulgar(configuracao, cronograma);
        return [
            new ItemConformidade("formulario_consequencia_em_formulario_facultativo", DimensaoConformidade.ExigenciasDocumentais, "Descumprimento: formulário facultativo sem consequência da falta de envio", declaracoes && !ConsequenciasDosFormularios.Facultativos(configuracao).Any()),
            new ItemConformidade("formulario_consequencia_em_formulario_da_inscricao", DimensaoConformidade.ExigenciasDocumentais, "Descumprimento: formulário da inscrição sem consequência da falta de envio", julgados && !ConsequenciasDosFormularios.DaInscricao(configuracao, cronograma).Any()),
            new ItemConformidade("formulario_acrescimo_nao_oferecido", DimensaoConformidade.ExigenciasDocumentais, "Descumprimento: retirada de acréscimo somente quando oferecido (formulário)", julgados && CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarAcrescimo(configuracao, "formularios") && !ConsequenciasDosFormularios.Acrescimo(configuracao, cronograma).Any()),
            new ItemConformidade("formulario_sem_modalidade_em_questao", DimensaoConformidade.ExigenciasDocumentais, "Descumprimento: retirada da modalidade apenas quando há modalidade em questão (formulário)", julgados && PodeAvaliarCoerenciaDosFormularios(configuracao) && !ConsequenciasDosFormularios.SemModalidade(configuracao, fatos, cronograma).Any()),
            new ItemConformidade("formulario_retira_a_modalidade_sem_ampla_concorrencia", DimensaoConformidade.ExigenciasDocumentais, "Descumprimento: ampla concorrência ofertada para retirada da modalidade (formulário)", julgados && PodeAvaliarCoerenciaDosFormularios(configuracao) && !ConsequenciasDosFormularios.SemAmpla(configuracao, fatos, cronograma).Any()),
        ];
    }

    private static bool PodeAvaliarCoerenciaDosFormularios(ConfiguracaoDeclarada configuracao) =>
        CoerenciaDaConsequenciaDoDescumprimento.PodeAvaliarCoerencia(configuracao, "formularios")
        && !CoerenciaDaConsequenciaDoDescumprimento.Recusada(configuracao, "documentosExigidos");

    private static bool PodeAvaliarAcrescimoDasBancas(ConfiguracaoDeclarada configuracao) =>
        !ConsequenciasDasBancas.SemJulgar(configuracao) && !CoerenciaDaConsequenciaDoDescumprimento.Recusada(configuracao, "bonusRegional");

    private static bool PodeAvaliarCoerenciaDasBancas(ConfiguracaoDeclarada configuracao) =>
        !ConsequenciasDasBancas.SemJulgar(configuracao)
        && !CoerenciaDaConsequenciaDoDescumprimento.Recusada(configuracao, "distribuicaoVagas")
        && !CoerenciaDaConsequenciaDoDescumprimento.Recusada(configuracao, "documentosExigidos");
}
