namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Recusas do grafo de coleta e da derivação de fatos do candidato.
/// </summary>
internal static class ErrosDaColetaDeFatos
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        // Grafo de coleta de fatos do candidato. Todas as recusas são de configuração
        // (422): grafo mal formado. A edição é permitida em rascunho e sob sessão de retificação
        // — a recusa de mutação de processo publicado sem sessão é a geral
        // (ProcessoSeletivo.MutacaoPosPublicacaoBloqueada). Os códigos de FatoColetado usam
        // constantes e escapam do fitness test — registrados aqui à mão para não caírem em 500.
        new("DocumentoExigido.FatoDaIsencaoEmOutraFinalidade", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.fato_da_isencao_em_outra_finalidade", "Fato do formulário de isenção citado por documento de outro formulário")),
        new("FatoColetado.ObrigatoriedadeInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fato_coletado.obrigatoriedade_invalida", "Obrigatoriedade do campo inválida")),
        new("FatoColetado.FatoDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fato_coletado.fato_duplicado", "O fato já é coletado por outro formulário do processo")),
        new("FatoColetado.PrecondicaoCitaFatoNaoColetado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fato_coletado.precondicao_cita_fato_nao_coletado", "A regra de derivação cita um fato que este processo não coleta nem deriva")),
        new("FatoColetado.ClassificacaoProtecaoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fato_coletado.classificacao_protecao_invalida", "Classificação de proteção de dados fora do vocabulário fechado")),
        new("FatoColetado.ComponenteExibicaoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fato_coletado.componente_exibicao_invalido", "Componente de exibição fora do vocabulário fechado")),
        new("CondicaoPrecondicaoFato.ClausulaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.condicao_precondicao_fato.clausula_invalida", "O ordinal da cláusula da pré-condição não pode ser negativo")),
        // Coletabilidade de fato — semântica cross-módulo resolvida na Application:
        // um fato coletado tem de existir no vocabulário e ser declarado com binding de campo de
        // inscrição (derivado/computado não é coletável).
        // Regra de derivação de fato. Todas as recusas são de configuração (422): regra
        // mal formada. A edição é permitida em rascunho e sob sessão de retificação — a
        // recusa de mutação de processo publicado sem sessão é a geral
        // (ProcessoSeletivo.MutacaoPosPublicacaoBloqueada). Os códigos usam constantes e escapam do
        // fitness test — registrados à mão para não caírem em 500.
        // Alvo de derivação — semântica cross-módulo resolvida na Application: o fato
        // configurado tem de existir e ser derivado com binding de regra de derivação.
        new("ConfiguracaoDerivacaoFato.FatoDesconhecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_derivacao_fato.fato_desconhecido", "O fato derivado não pertence ao vocabulário de fatos do candidato")),
        new("ConfiguracaoDerivacaoFato.FatoNaoDerivavel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_derivacao_fato.fato_nao_derivavel", "O fato não é um alvo de derivação — só um fato derivado com binding de regra de derivação pode ter regras configuradas")),
        new("ConfiguracaoDerivacaoFato.CodigoFatoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_derivacao_fato.codigo_fato_obrigatorio", "O código do fato derivado é obrigatório")),
        new("ConfiguracaoDerivacaoFato.SemRegras", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_derivacao_fato.sem_regras", "A derivação de um fato precisa de ao menos uma regra")),
        new("ConfiguracaoDerivacaoFato.OrdemRegraDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_derivacao_fato.ordem_regra_duplicada", "Duas regras da mesma derivação têm a mesma ordem")),
        new("ConfiguracaoDerivacaoFato.RegrasComESemCodigo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_derivacao_fato.regras_com_e_sem_codigo", "As regras de uma derivação contribuem código todas, ou nenhuma no derivado booleano")),
        new("ConfiguracaoDerivacaoFato.ContribuicaoIncoerenteComODominio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_derivacao_fato.contribuicao_incoerente_com_o_dominio", "As regras do derivado booleano não contribuem código, e as do derivado categórico contribuem")),
        new("ConfiguracaoDerivacaoFato.CodigoFatoDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_derivacao_fato.codigo_fato_duplicado", "Um fato tem mais de uma configuração de derivação neste processo")),
        new(ProcessoSeletivoErrorCodes.DerivacaoDaOferta, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.derivacao_da_oferta", "O fato é derivado das modalidades que o processo oferta e não se configura no processo")),
        new("RegraDerivacaoConfigurada.OrdemInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_derivacao_configurada.ordem_invalida", "A ordem da regra não pode ser negativa")),
        new("RegraDerivacaoConfigurada.ContribuiEmBranco", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_derivacao_configurada.contribui_em_branco", "O código contribuído pela regra não pode ficar em branco")),
        new("CondicaoRegraDerivacao.ClausulaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.condicao_regra_derivacao.clausula_invalida", "O ordinal da cláusula da regra não pode ser negativo")),
        // Grafo de dependência conjunto (§6): ciclo pelas quatro classes de aresta juntas.
        new("GrafoDependenciaConjunta.GrafoConjuntoComCiclo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.grafo_dependencia_conjunta.grafo_conjunto_com_ciclo", "O grafo de dependência conjunto (produção, pré-condição, derivação e gatilho) forma um ciclo")),
        new("ProcessoSeletivo.DocumentoNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.documento_nao_encontrado", "Documento do Edital não encontrado ou não pertence a este processo")),
        new("ProcessoSeletivo.DocumentoNaoEnviadoComoEdital", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.documento_nao_enviado_como_edital", "Somente um documento enviado como edital pode ser o documento do edital")),
        new("ProcessoSeletivo.DocumentoNaoConfirmado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.documento_nao_confirmado", "Somente um documento confirmado pode ser referenciado na publicação")),
        // Congelamento dos parâmetros na publicação (UNI-REQ-0019): defesa em profundidade — o vocabulário de fatos é fechado e
        // append-only (ADR-0111), então este código não deveria ocorrer em produção, mas
        // precisa de mapeamento nomeado (não um 500 genérico) se ocorrer.
        new("ProcessoSeletivo.FatoCongeladoNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fato_congelado_nao_encontrado", "Fato citado em condição de gatilho não encontrado no catálogo de fatos do candidato")),
        new("ProcessoSeletivo.PressupostoSemCadastro", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.pressuposto_sem_cadastro", "Fato citado pelas regras de um formulário não encontrado no catálogo de fatos do candidato")),
        // Reconferência de coletabilidade no congelamento: o catálogo pode reclassificar a
        // Origem de um fato depois que ele já virou FatoColetado (ex.: a migration que
        // reclassificou MODALIDADE de DECLARADO para DERIVADO).
        new("ProcessoSeletivo.FatoColetadoNaoMaisDeclarado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fato_coletado_nao_mais_declarado", "Fato coletado deixou de ser declarado/vinculado a campo de inscrição no catálogo de fatos do candidato")),
        // Um fato coletado vinculado como seleção (SELECAO_UNICA/SELECAO_MULTIPLA) cujo catálogo
        // não declara valores de domínio nem é um dos categóricos de escopo-processo conhecidos —
        // alcançável se o catálogo crescer um categórico DECLARADO novo antes de o resolvedor de
        // valores selecionáveis aprender a origem dele.
        new("ProcessoSeletivo.FatoDeSelecaoSemOrigemDeValores", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fato_de_selecao_sem_origem_de_valores", "Fato de seleção sem origem conhecida de valores selecionáveis")),
    ];
}
