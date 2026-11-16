namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Recusas da publicação do processo, da janela de isenção e da cadeia de versões da configuração.
/// </summary>
internal static class ErrosDaPublicacao
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        .. PeriodoDoEdital(),
        .. FasesDoCronogramaNaPublicacao(),
        .. JanelaDeIsencao(),
        .. AtoDaConfiguracao(),
        .. CadeiaDeVersoesDaConfiguracao(),
        .. ConformidadeParaPublicar(),
    ];

    // Publicação do Processo Seletivo — congelamento dos parâmetros na publicação, UNI-REQ-0019. Todo
    // 422: status dominante do módulo para violação de regra de negócio,
    // inclusive quando a origem física é constraint de banco (ADR-0102).
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> PeriodoDoEdital() =>
    [
        new("DadosEdital.PeriodoInscricaoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.dados_edital.periodo_inscricao_invalido", "O fim do período de inscrição não pode anteceder o início")),
        new("DadosEdital.PeriodoInscricaoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.dados_edital.periodo_inscricao_obrigatorio", "O período de inscrição exige início e fim definidos")),
        // As situações que o operador precisa distinguir ao datar a inscrição.
        new("ProcessoSeletivo.PeriodoInscricaoNaoInformavel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.periodo_inscricao_nao_informavel", "O período de inscrição vem da janela da fase do cronograma que coleta inscrição e não pode ser informado na publicação")),
        new("ProcessoSeletivo.PeriodoInscricaoObrigatorioSemFaseDeColeta", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.periodo_inscricao_obrigatorio_sem_fase_de_coleta", "O processo não tem fase do cronograma que colete inscrição, então o período precisa ser informado na publicação")),
        new("ProcessoSeletivo.FaseQueColetaInscricaoSemJanela", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fase_que_coleta_inscricao_sem_janela", "A fase que coleta inscrição precisa de início e fim definidos para que o Edital declare o período")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> FasesDoCronogramaNaPublicacao() =>
    [
        new("ProcessoSeletivo.FaseSemUnidadeResponsavel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fase_sem_unidade_responsavel", "Toda fase do cronograma precisa de uma unidade responsável para o processo ser publicado")),
        new("FaseCronograma.UnidadeResponsavelNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.unidade_responsavel_nao_encontrada", "Unidade responsável da fase não encontrada ou não está mais viva")),
    ];

    // As regras de contorno da janela de solicitação de isenção (UNI-REQ-0106).
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> JanelaDeIsencao() =>
    [
        new("ProcessoSeletivo.JanelaDeIsencaoSemPrazo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.janela_de_isencao_sem_prazo", "A fase que abre a solicitação de isenção precisa de início e fim definidos")),
        new("ProcessoSeletivo.JanelaDeIsencaoNaoAbreComAInscricao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.janela_de_isencao_nao_abre_com_a_inscricao", "A janela de solicitação de isenção abre no mesmo instante em que abrem as inscrições")),
        new("ProcessoSeletivo.JanelaDeIsencaoNaoTerminaAntesDaInscricao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.janela_de_isencao_nao_termina_antes_da_inscricao", "A solicitação de isenção precisa terminar antes do fim das inscrições")),
        new("ProcessoSeletivo.JanelaDeIsencaoMenorQueCincoDias", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.janela_de_isencao_menor_que_cinco_dias", "A janela de solicitação de isenção precisa de ao menos cinco dias corridos")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> AtoDaConfiguracao() =>
    [
        new("ProcessoSeletivo.TipoDeAtoSemVersaoVigente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.tipo_de_ato_sem_versao_vigente", "Não há versão vigente do tipo de ato na data da publicação")),
        new("ProcessoSeletivo.TipoDeAtoNaoCongelaConfiguracao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.tipo_de_ato_nao_congela_configuracao", "O ato que cria a versão da configuração precisa ser de um tipo que congela configuração")),
        new("ProcessoSeletivo.MotivoRetificacaoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.motivo_retificacao_obrigatorio", "O motivo da retificação é obrigatório")),
    ];

    /// <summary>
    /// A cadeia de versões congeladas da configuração.
    /// </summary>
    /// <remarks>
    /// VersaoConfiguracao (ADR-0104/0063): entidade forensic — os guards de
    /// SHAPE (bytes vazios, hash malformado, id zerado) lançam
    /// ArgumentException (defesa em profundidade contra erro de programação
    /// do caller, nunca alcançável a partir de input do usuário) e não têm
    /// entrada aqui. O mesmo vale para as duas travas de banco que repetem
    /// uma guarda de forma da entidade — um ato que retifica a si mesmo e o
    /// contrato de abertura (versão 1 sem retificação, as demais com): o
    /// domínio nunca monta essa linha, então a violação só aparece por
    /// gravação malformada e propaga como exceção, sem código público. Já as invariantes de NEGÓCIO da cadeia de versões
    /// afloram como DomainError, pelo domínio e pelos guard rails de banco
    /// que fecham a corrida check-then-act (ADR-0102).
    ///
    /// Dessas, UMA é corrida e vale 409; as outras quatro são 422. A separação
    /// não é de julgamento — sai da ordem em que o trigger de sucessão
    /// confere as coisas.
    ///
    /// O trigger carrega como `anterior` a linha de MAIOR numero_versao e,
    /// antes de qualquer outra checagem, devolve o controle quando o número
    /// que chega não é maior que o dela — deixando a duplicidade para o
    /// índice único, de propósito, para não confundir "número ocupado" com
    /// "buraco na numeração".
    ///
    /// É esse retorno antecipado que decide a questão. Uma sucessora montada
    /// sobre leitura que envelheceu traz exatamente o número que a publicação
    /// concorrente acabou de gravar, cai no retorno antecipado, e sai pelo
    /// índice único: toda corrida vira NumeroDuplicado, e só ela. Nada no
    /// corpo está errado, e 422 mentiria mandando o operador corrigir o que
    /// não tem defeito — é o conflito com o estado atual do recurso que a
    /// RFC 9110 descreve, resolvido relendo o recurso e reavaliando a intenção.
    ///
    /// Cadeia quebrada e vigência regressiva o agregado já conferiu em memória
    /// contra a MESMA linha que o trigger vai encontrar: as versões são
    /// append-only e únicas por número, então a anterior que o cliente leu e a
    /// que o trigger acha são a mesma, com o mesmo ato e a mesma vigência.
    /// Não são alcançáveis por corrida — só por gravação malformada, que nomeia
    /// a anterior certa e ainda assim contradiz o que ela diz. Somam-se a elas a recusa de um id que o corpo
    /// informa (ato que já criou versão, e reenviar não conserta) e a guarda
    /// contra INSERT por fora do agregado (numeração com buraco). Nenhuma
    /// delas melhora ao ser retentada.
    ///
    /// O `code` continua específico em todas. O status responde "dá para
    /// tentar de novo?"; o code responde "por quê?" — colapsar no
    /// `uniplus.concorrencia.conflito` da ADR-0119 apagaria a segunda
    /// resposta sem melhorar a primeira, e confundiria com o conflito de
    /// xmin, que é outro mecanismo.
    /// </remarks>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> CadeiaDeVersoesDaConfiguracao() =>
    [
        new("VersaoConfiguracao.CadeiaQuebrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.versao_configuracao.cadeia_quebrada", "O ato criador da versão não retifica o ato criador da versão anterior")),
        // NÃO retentável, apesar de ser corrida: o código é compartilhado, e um dos caminhos
        // que o emitem é o atalho atômico de retificação, que relê a versão corrente a cada
        // execução. Repetir depois de perder a corrida retificaria a versão do VENCEDOR, criando
        // um ato normativo a mais — publicado no Diário Oficial — que ninguém pediu.
        new("VersaoConfiguracao.NumeroDuplicado", new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.versao_configuracao.numero_duplicado", "Outra publicação concorrente já criou esta versão da configuração")),
        new("VersaoConfiguracao.AtoCriadorJaCriouVersao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.versao_configuracao.ato_criador_ja_criou_versao", "O ato informado já criou uma versão da configuração")),
        new("VersaoConfiguracao.NumeracaoComBuraco", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.versao_configuracao.numeracao_com_buraco", "A numeração das versões da configuração é contígua")),
        new("VersaoConfiguracao.VigenciaRegressiva", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.versao_configuracao.vigencia_regressiva", "A vigência de uma versão não pode preceder a da versão anterior")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> ConformidadeParaPublicar() =>
    [
        new("ProcessoSeletivo.TransicaoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.transicao_invalida", "Só é possível publicar um processo em rascunho")),
        new("ProcessoSeletivo.ConformidadeInsuficiente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.conformidade_insuficiente", "Processo não conforme para publicação")),
        new("ProcessoSeletivo.ConformidadeLegalInsuficiente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.conformidade_legal_insuficiente", "Processo não conforme às obrigatoriedades legais vigentes")),
        new("ProcessoSeletivo.RegraLegalInavaliavel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.regra_legal_inavaliavel", "Regra legal vigente não pode ser avaliada")),
        new("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.mutacao_pos_publicacao_bloqueada", "Processo publicado não aceita mutação direta da configuração")),
    ];
}
