namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Recusas do grafo de coleta e da derivação de fatos do candidato. Todas são de configuração (422):
/// grafo ou regra mal formados. A edição é permitida só em rascunho; a recusa de mutação de processo
/// publicado é a geral (<c>ProcessoSeletivo.MutacaoPosPublicacaoBloqueada</c>). Os códigos usam
/// constantes e escapam do teste que confere os códigos de erro: são registrados à mão para não
/// caírem em 500.
/// </summary>
internal static class ErrosDaColetaDeFatos
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        .. DosCamposColetados(),
        .. DasConfiguracoesDeDerivacao(),
        .. DasRegrasDeDerivacao(),
        .. DoGrafoConjuntoEDosDocumentos(),
        .. DoCongelamentoDosFatos(),
    ];

    /// <summary>Grafo de coleta: a obrigatoriedade, a classificação de proteção, o componente e a duplicidade dos campos coletados, e o fato da isenção citado por documento de outro formulário.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DosCamposColetados() =>
    [
        new("DocumentoExigido.FatoDaIsencaoEmOutraFinalidade", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.fato_da_isencao_em_outra_finalidade", "Fato do formulário de isenção citado por documento de outro formulário")),
        new("FatoColetado.ObrigatoriedadeInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fato_coletado.obrigatoriedade_invalida", "Obrigatoriedade do campo inválida")),
        new("FatoColetado.FatoDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fato_coletado.fato_duplicado", "O fato já é coletado por outro formulário do processo")),
        new("FatoColetado.PrecondicaoCitaFatoNaoColetado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fato_coletado.precondicao_cita_fato_nao_coletado", "A regra de derivação cita um fato que este processo não coleta nem deriva")),
        new("FatoColetado.ClassificacaoProtecaoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fato_coletado.classificacao_protecao_invalida", "Classificação de proteção de dados fora do vocabulário fechado")),
        new("FatoColetado.ComponenteExibicaoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fato_coletado.componente_exibicao_invalido", "Componente de exibição fora do vocabulário fechado")),
    ];

    /// <summary>Alvo de derivação, resolvido pela Application: o fato configurado tem de existir e ser derivado com vínculo de regra de derivação.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DasConfiguracoesDeDerivacao() =>
    [
        new("ConfiguracaoDerivacaoFato.FatoDesconhecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_derivacao_fato.fato_desconhecido", "O fato derivado não pertence ao vocabulário de fatos do candidato")),
        new("ConfiguracaoDerivacaoFato.FatoNaoDerivavel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_derivacao_fato.fato_nao_derivavel", "O fato não é um alvo de derivação — só um fato derivado com binding de regra de derivação pode ter regras configuradas")),
        new("ConfiguracaoDerivacaoFato.CodigoFatoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_derivacao_fato.codigo_fato_obrigatorio", "O código do fato derivado é obrigatório")),
        new("ConfiguracaoDerivacaoFato.SemRegras", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_derivacao_fato.sem_regras", "A derivação de um fato precisa de ao menos uma regra")),
        new("ConfiguracaoDerivacaoFato.OrdemRegraDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_derivacao_fato.ordem_regra_duplicada", "Duas regras da mesma derivação têm a mesma ordem")),
        new("ConfiguracaoDerivacaoFato.RegrasComESemCodigo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_derivacao_fato.regras_com_e_sem_codigo", "As regras de uma derivação contribuem código todas, ou nenhuma no derivado booleano")),
        new("ConfiguracaoDerivacaoFato.ContribuicaoIncoerenteComODominio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_derivacao_fato.contribuicao_incoerente_com_o_dominio", "As regras do derivado booleano não contribuem código, e as do derivado categórico contribuem")),
        new("ConfiguracaoDerivacaoFato.CodigoFatoDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_derivacao_fato.codigo_fato_duplicado", "Um fato tem mais de uma configuração de derivação neste processo")),
        new(ProcessoSeletivoErrorCodes.DerivacaoDaOferta, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.derivacao_da_oferta", "O fato é derivado das modalidades que o processo oferta e não se configura no processo")),
    ];

    /// <summary>A forma de cada regra de derivação e das condições dela.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DasRegrasDeDerivacao() =>
    [
        new("RegraDerivacaoConfigurada.OrdemInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_derivacao_configurada.ordem_invalida", "A ordem da regra não pode ser negativa")),
        new("RegraDerivacaoConfigurada.ContribuiEmBranco", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_derivacao_configurada.contribui_em_branco", "O código contribuído pela regra não pode ficar em branco")),
        new("CondicaoRegraDerivacao.ClausulaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.condicao_regra_derivacao.clausula_invalida", "O ordinal da cláusula da regra não pode ser negativo")),
    ];

    /// <summary>Ciclo do grafo de dependência conjunto, pelas quatro classes de aresta juntas, e os documentos do processo.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DoGrafoConjuntoEDosDocumentos() =>
    [
        new("GrafoDependenciaConjunta.GrafoConjuntoComCiclo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.grafo_dependencia_conjunta.grafo_conjunto_com_ciclo", "O grafo de dependência conjunto (produção, pré-condição, derivação e gatilho) forma um ciclo")),
        new("ProcessoSeletivo.DocumentoNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.documento_nao_encontrado", "Documento do Edital não encontrado ou não pertence a este processo")),
        new("ProcessoSeletivo.DocumentoNaoEnviadoComoEdital", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.documento_nao_enviado_como_edital", "Somente um documento enviado como edital pode ser o documento do edital")),
        new("ProcessoSeletivo.DocumentoNaoConfirmado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.documento_nao_confirmado", "Somente um documento confirmado pode ser referenciado na publicação")),
    ];

    /// <summary>Congelamento dos parâmetros na publicação (UNI-REQ-0019): o vocabulário de fatos é fechado e só cresce (ADR-0111), então estes
    /// códigos não deveriam ocorrer, mas precisam de mapeamento nomeado, não de um 500 genérico. Inclui a reconferência de coletabilidade,
    /// porque o catálogo pode reclassificar a origem de um fato depois que ele já virou campo coletado, e o fato de seleção cujo catálogo
    /// não declara valores de domínio nem é categórico conhecido do processo.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DoCongelamentoDosFatos() =>
    [
        new("ProcessoSeletivo.FatoCongeladoNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fato_congelado_nao_encontrado", "Fato citado em condição de gatilho não encontrado no catálogo de fatos do candidato")),
        new("ProcessoSeletivo.PressupostoSemCadastro", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.pressuposto_sem_cadastro", "Fato citado pelas regras de um formulário não encontrado no catálogo de fatos do candidato")),
        new("ProcessoSeletivo.FatoColetadoNaoMaisDeclarado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fato_coletado_nao_mais_declarado", "Fato coletado deixou de ser declarado/vinculado a campo de inscrição no catálogo de fatos do candidato")),
        new("ProcessoSeletivo.FatoDeSelecaoSemOrigemDeValores", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fato_de_selecao_sem_origem_de_valores", "Fato de seleção sem origem conhecida de valores selecionáveis")),
    ];
}
