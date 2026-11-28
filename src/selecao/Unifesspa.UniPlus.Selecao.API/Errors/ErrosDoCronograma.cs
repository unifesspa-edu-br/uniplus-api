namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Application.Queries.RolDeRegras;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Recusas do cronograma de fases, das etapas e dos recursos.
/// </summary>
internal static class ErrosDoCronograma
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        .. EstruturaDoCronograma(),
        .. HomologacaoDasInscricoesPorOrigem(),
        .. BancasQueAOrigemExige(),
        .. EtapasNoCronograma(),
        .. DeclaracaoDaFase(),
        .. CandidatosAtendidosDasBancas(),
        .. DeclaracaoDaEtapa(),
        .. FechamentoDoCicloDeRecurso(),
        .. ProdutosETiposDeAto(),
        .. AncoraDoRecursoDaEtapa(),
        .. PrazoDoRecursoDaEtapa(),
        .. AncoraDoRecursoDaFase(),
        .. PrazoDoRecursoDaFase(),
    ];

    /// <summary>
    /// Estrutura do cronograma: ordem, fase canônica, marca de mesmo momento, precedência, janelas e o piso mínimo derivado da origem dos
    /// candidatos e das vagas ofertadas. Todos 422.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> EstruturaDoCronograma() =>
    [
        new("ProcessoSeletivo.CronogramaFasesVazio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.cronograma_fases_vazio", "O processo deve ter ao menos uma fase no cronograma")),
        new("ProcessoSeletivo.OrdemFaseDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.ordem_fase_duplicada", "Cada fase deve ter uma ordem única dentro do cronograma")),
        new("ProcessoSeletivo.FaseCanonicaDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fase_canonica_duplicada", "A mesma fase canônica não pode aparecer duas vezes no cronograma")),
        new("ProcessoSeletivo.PrecedenciaFaseViolada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.precedencia_fase_violada", "A ordem declarada viola a precedência entre fases do cadastro")),
        new("ProcessoSeletivo.SobreposicaoDeJanelasNaoPermitida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.sobreposicao_de_janelas_nao_permitida", "Os períodos de duas fases coincidem em parte, e o cadastro não permite essa coincidência")),
        new(ProcessoSeletivoErrorCodes.PrimeiraFaseNoMesmoMomentoDaAnterior, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.primeira_fase_no_mesmo_momento_da_anterior", "A primeira fase do cronograma não pode estar no mesmo momento da anterior")),
        new(ProcessoSeletivoErrorCodes.FasesComPrecedenciaNoMesmoMomento, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fases_com_precedencia_no_mesmo_momento", "Fases ligadas por precedência não podem estar no mesmo momento")),
        new("ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.inscricao_propria_sem_fase_de_coleta", "A origem dos candidatos é inscrição própria, e nenhuma fase coleta inscrição")),
        new("ProcessoSeletivo.VagasSemFaseQueProduzResultado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.vagas_sem_fase_que_produz_resultado", "Há vagas ofertadas, e nenhuma fase do cronograma produz resultado")),
    ];

    /// <summary>
    /// A homologação das inscrições que cada origem de candidatos exige do cronograma: a fase que tem a regra e, na importação
    /// externa, o ciclo de recurso dela. Todos 422.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> HomologacaoDasInscricoesPorOrigem() =>
    [
        new("ProcessoSeletivo.InscricaoPropriaSemHomologacaoDasInscricoes", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.inscricao_propria_sem_homologacao_das_inscricoes", "O processo com inscrição própria não tem fase que homologa as inscrições")),
        new("ProcessoSeletivo.ImportacaoDeExameSemHomologacaoDasInscricoes", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.importacao_de_exame_sem_homologacao_das_inscricoes", "O processo que usa a nota do ENEM não tem fase que homologa as inscrições")),
        new("ProcessoSeletivo.ImportacaoExternaSemHomologacaoDasInscricoes", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.importacao_externa_sem_homologacao_das_inscricoes", "O processo de importação externa não tem fase que homologa as inscrições")),
        new("ProcessoSeletivo.ImportacaoExternaSemJanelaDeRecurso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.importacao_externa_sem_janela_de_recurso", "A homologação da importação externa não tem preliminar, período de recurso e definitivo")),
    ];

    /// <summary>
    /// As bancas que a publicação exige conforme a origem dos candidatos e o uso da nota do ENEM: a de homologação das
    /// inscrições e as duas de importação. Todos 422.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> BancasQueAOrigemExige() =>
    [
        new(ProcessoSeletivoErrorCodes.HomologacaoDasInscricoesSemBanca, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.homologacao_das_inscricoes_sem_banca", "A fase que homologa as inscrições não prevê banca de homologação das inscrições")),
        new(ProcessoSeletivoErrorCodes.NotaDoEnemSemBancaDeImportacaoDasNotas, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.nota_do_enem_sem_banca_de_importacao_das_notas", "O processo que usa a nota do ENEM com inscrição própria não prevê a banca de importação das notas do INEP")),
        new(ProcessoSeletivoErrorCodes.ImportacaoExternaSemBancaDeImportacaoDeCandidatos, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.importacao_externa_sem_banca_de_importacao_de_candidatos", "O processo que importa candidatos de fora não prevê a banca de importação de candidatos")),
    ];

    /// <summary>
    /// A etapa dentro do cronograma: a fase que a abriga, a janela e a etapa que um documento exigido referencia.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> EtapasNoCronograma() =>
    [
        new("ProcessoSeletivo.AvaliacaoSemEtapa", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.avaliacao_sem_etapa", "Fase que agrupa etapas sem nenhuma etapa que aconteça nela")),
        new("ProcessoSeletivo.EtapaSemFaseNoCronograma", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_sem_fase_no_cronograma", "A etapa declara uma fase que não está no cronograma do processo")),
        new("ProcessoSeletivo.EtapaComecaAntesDaFase", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_comeca_antes_da_fase", "A etapa começa antes da fase que a abriga")),
        new("ProcessoSeletivo.EtapaTerminaDepoisDaFase", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_termina_depois_da_fase", "A etapa termina depois da fase que a abriga")),
        new("ProcessoSeletivo.EtapaForaDaJanelaDaFase", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_fora_da_janela_da_fase", "A etapa acontece inteira fora do período da fase que a abriga; ajuste as datas da etapa para ficarem dentro do período da fase")),
        new("ProcessoSeletivo.EtapaReferenciadaPorExigenciaDocumental", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_referenciada_por_exigencia_documental", "A etapa a remover é onde um documento exigido é coletado")),
        new("DocumentoExigido.EtapaNaoPertenceAoProcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.etapa_nao_pertence_ao_processo", "A etapa que coleta o documento não é uma etapa deste processo")),
        new("DocumentoExigido.EtapaNaoPertenceAFase", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.etapa_nao_pertence_a_fase", "A etapa que coleta o documento não acontece na fase em que ele é exigido")),
    ];

    /// <summary>
    /// As recusas da declaração da fase. Os três "não encontrada" servem à resolução entre módulos do tipo de fase, do tipo de banca e da categoria de documento,
    /// para o handler recusar um id que não resolve, no mesmo molde da oferta de curso e da regra de distribuição de vagas.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DeclaracaoDaFase() =>
    [
        new("FaseCronograma.JanelaObrigatoriaEmDataNoEdital", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.janela_obrigatoria_em_data_no_edital", "Fase com data fixada no edital exige início e término do período")),
        new("FaseCronograma.JanelaInvertida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.janela_invertida", "A data de término do período da fase é anterior à data de início")),
        new("FaseCronograma.AtoDuplicadoNaFase", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.ato_duplicado_na_fase", "A fase declara o mesmo tipo de ato com o mesmo papel mais de uma vez")),
        new("FaseCronograma.ParecerIndividualSemResultado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.parecer_individual_sem_resultado", "A fase promete parecer individual e não publica nenhum resultado")),
        new("FaseCronograma.FaseCanonicaNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.fase_canonica_nao_encontrada", "Tipo de fase não encontrado no catálogo ou removido")),
        new("FaseCronograma.TipoBancaNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.tipo_banca_nao_encontrado", "O tipo de banca não existe ou está desativado")),
        new("FaseCronograma.CategoriaDocumentoNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.categoria_documento_nao_encontrada", "A categoria de documento dos candidatos atendidos não existe ou está desativada")),
    ];

    /// <summary>
    /// Os candidatos atendidos dizem quais candidatos cada banca julga. Bancas do mesmo tipo que atendem os mesmos candidatos
    /// julgam em paralelo; só se recusa a banca sem categoria ao lado de banca com categoria e a categoria repetida na banca.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> CandidatosAtendidosDasBancas() =>
    [
        new("BancaRequerida.CategoriaRepetidaNosCandidatosAtendidos", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca_requerida.categoria_repetida_nos_candidatos_atendidos", "A banca repete uma categoria de documento nos candidatos atendidos")),
        new("BancaRequerida.CandidatosAtendidosObrigatorios", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca_requerida.candidatos_atendidos_obrigatorios", "Bancas do mesmo tipo: umas definem os candidatos atendidos e outra não")),
    ];

    /// <summary>
    /// O recurso da etapa prova as mesmas invariantes dos parâmetros do recurso que a regra da fase, e a recusa precisa nomear quem recusou.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DeclaracaoDaEtapa() =>
    [
        new("EtapaProcesso.JanelaInvertida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.janela_invertida", "A data de término do período da etapa é anterior à data de início")),
        new(EtapaProcessoErrorCodes.TipoBancaNaoEncontradoOuInativo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.tipo_banca_nao_encontrado_ou_inativo", "O tipo de banca não existe ou está desativado")),
        new("EtapaProcesso.AtoDuplicadoNaEtapa", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.ato_duplicado_na_etapa", "A etapa declara o mesmo tipo de ato com o mesmo papel mais de uma vez")),
    ];

    /// <summary>
    /// O fechamento do ciclo de recurso por matéria, dentro da própria fase ou etapa: o preliminar de um tipo de ato exige o
    /// definitivo do mesmo tipo no mesmo lugar. A retificação confere a configuração nova contra o que já foi publicado: o ciclo
    /// que o preliminar publicado abriu não pode ficar sem o definitivo que o fecha.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> FechamentoDoCicloDeRecurso() =>
    [
        new(ProcessoSeletivoErrorCodes.FaseComPreliminarSemDefinitivo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fase_com_preliminar_sem_definitivo", "A fase publica resultado preliminar sem o definitivo do mesmo tipo de ato")),
        new(ProcessoSeletivoErrorCodes.EtapaComPreliminarSemDefinitivo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_com_preliminar_sem_definitivo", "A etapa publica resultado preliminar sem o definitivo do mesmo tipo de ato")),
        new(ProcessoSeletivoErrorCodes.RetificacaoDesfazCicloRecursalAberto, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.retificacao_desfaz_ciclo_recursal_aberto", "A retificação retira ou muda de ciclo o preliminar publicado ou o definitivo pendente de uma matéria")),
    ];

    /// <summary>
    /// Cada produto declarado pela fase ou pela etapa é resolvido contra o catálogo de Publicações: o tipo existe e está vigente, e o
    /// papel só cabe onde o catálogo diz que o ato é resultado, classificação intrínseca do tipo, que nunca varia por edital. O
    /// catálogo também diz o que o tipo não pode ser: o resultado sem preliminar é declarado definitivo, e o tipo que congela a
    /// configuração não é item de resultado, porque a correção de um resultado não cria versão nova. O código declarado na etapa vira
    /// o nome do produto no cronograma público e a chave do produto no cronograma público.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> ProdutosETiposDeAto() =>
    [
        new("ProdutoDaFase.AtoNaoEncontradoNoCatalogo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.produto_da_fase.ato_nao_encontrado_no_catalogo", "O tipo de ato declarado pela fase não tem versão vigente no catálogo de Publicações")),
        new("ProdutoDaFase.PapelDesconhecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.produto_da_fase.papel_desconhecido", "O papel do produto não é declarável")),
        new("ProdutoDaFase.PapelEmAtoQueNaoEhResultado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.produto_da_fase.papel_em_ato_que_nao_eh_resultado", "Só ato que é resultado no catálogo recebe papel preliminar ou definitivo")),
        new(ProcessoSeletivoErrorCodes.ResultadoDeclaradoSemPapel, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.resultado_declarado_sem_papel", "O tipo de ato classificado como resultado precisa declarar papel preliminar ou definitivo")),
        new(ProcessoSeletivoErrorCodes.TipoQueCongelaDeclaradoComoResultado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.tipo_que_congela_declarado_como_resultado", "O tipo de ato que congela a configuração não pode ser declarado como item de resultado")),
        new("ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.produto_da_etapa.ato_nao_encontrado_no_catalogo", "O tipo de ato declarado pela etapa não tem versão vigente no catálogo de Publicações")),
        new("ProdutoDaEtapa.PapelDesconhecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.produto_da_etapa.papel_desconhecido", "O papel do produto da etapa não é declarável")),
        new("ProdutoDaEtapa.PapelEmAtoQueNaoEhResultado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.produto_da_etapa.papel_em_ato_que_nao_eh_resultado", "Só ato que é resultado no catálogo recebe papel preliminar ou definitivo")),
        new(ProcessoSeletivoErrorCodes.TipoDeAtoUnicoDeclaradoMaisDeUmaVez, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.tipo_de_ato_unico_declarado_mais_de_uma_vez", "O tipo de ato que só pode ser publicado uma vez por processo está declarado em mais de um produto do cronograma")),
    ];

    /// <summary>
    /// A âncora do recurso da etapa.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> AncoraDoRecursoDaEtapa() =>
    [
        new("EtapaProcesso.AncoraNaoEhProdutoPreliminarDaEtapa", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.ancora_nao_eh_produto_preliminar_da_etapa", "O prazo do recurso contra ato publicado começa na publicação de um resultado preliminar da própria etapa; indique um resultado preliminar publicado pela etapa")),
        new("EtapaProcesso.CienciaSemParecerIndividual", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.ciencia_sem_parecer_individual", "O prazo contado da ciência exige que a etapa prometa parecer individual")),
        new("EtapaProcesso.RecursoDuplicadoNaEtapa", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.recurso_duplicado_na_etapa", "Cada fato recorrível abre um único prazo de interposição; declare um só recurso para cada fato")),
        new("RecursoDaEtapa.RegraCatalogoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.regra_catalogo_invalida", "A regra declarada não é uma regra de prazo de recurso do catálogo")),
        new("RecursoDaEtapa.AncoraEmAtoCongelante", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.ancora_em_ato_congelante", "O evento que inicia o prazo do recurso da etapa não pode ser o ato que congela a configuração; escolha outro ato")),
        new("RecursoDaEtapa.AncoraEmAtoIrreversivel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.ancora_em_ato_irreversivel", "O evento que inicia o prazo do recurso da etapa não pode ser ato de efeito irreversível; escolha outro ato")),
        new("RecursoDaEtapa.AncoraObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.ancora_obrigatoria", "Declare de que instante o prazo corre: da publicação do ato ou da ciência do candidato")),
    ];

    /// <summary>
    /// Os prazos, o efeito suspensivo e a guarda da vaga do recurso da etapa. Os quatro códigos de prazo valem para o prazo de interposição e para o da 2ª instância, que se distinguem pelo campo e pela mensagem.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> PrazoDoRecursoDaEtapa() =>
    [
        new("RecursoDaEtapa.PrazoNaoPositivo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_recurso_da_etapa.prazo_nao_positivo", "O prazo do recurso deve ser maior que zero")),
        new("RecursoDaEtapa.PrazoEmDiasCorridos", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_recurso_da_etapa.prazo_em_dias_corridos", "O prazo do recurso não aceita dias corridos")),
        new("RecursoDaEtapa.PrazoSemUnidadeDeclaravel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_recurso_da_etapa.prazo_sem_unidade_declaravel", "O prazo do recurso precisa declarar dias úteis ou horas")),
        new("RecursoDaEtapa.PrazoEmFracaoDeDiaUtil", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_recurso_da_etapa.prazo_em_fracao_de_dia_util", "O prazo do recurso em dias úteis exige número inteiro")),
        new("RecursoDaEtapa.EfeitoSuspensivoNaoDeclarado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_recurso_da_etapa.efeito_suspensivo_nao_declarado", "O período de recurso precisa declarar um efeito suspensivo aceito")),
        new("RecursoDaEtapa.PrazoSegundaInstanciaNaoDeclarado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_recurso_da_etapa.prazo_segunda_instancia_nao_declarado", "O recurso que admite instância superior precisa declarar o prazo da 2ª instância")),
        new("RecursoDaEtapa.PrazoSegundaInstanciaSemInstanciaSuperior", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_recurso_da_etapa.prazo_segunda_instancia_sem_instancia_superior", "Sem instância superior, não há prazo de 2ª instância")),
        new("RecursoDaEtapa.NaoGuardaComInstanciaSuperior", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_recurso_da_etapa.nao_guarda_com_instancia_superior", "Com instância superior, a vaga fica guardada até o resultado definitivo")),
        new("RecursoDaEtapa.GuardaDaVagaNaoDeclaravel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_recurso_da_etapa.guarda_da_vaga_nao_declaravel", "A guarda da vaga declarada não é uma das opções aceitas")),
    ];

    /// <summary>
    /// A identidade da regra é o par (codigo, versao): pedir uma versão que não existe de um código que existe é o mesmo 404 de
    /// pedir um código inventado, e distinguir os dois revelaria quais códigos há no catálogo a quem só tentou adivinhar. A âncora
    /// do prazo é um produto preliminar da própria fase, referenciado pela identidade da linha: sem nenhum preliminar não há
    /// publicação a recorrer, e havendo, a âncora tem de ser um deles.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> AncoraDoRecursoDaFase() =>
    [
        new(FiltroDeTipoDasRegrasDoCatalogo.CodigoDeErro, new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.selecao.regra_catalogo.filtro_de_tipo_invalido", "Tipo do filtro de regras inválido")),
        new("RegraCatalogo.NaoEncontrada", new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.regra_catalogo.nao_encontrada", "Regra não encontrada no catálogo na versão pedida")),
        new("RegraRecursoFase.FaseSemProdutoPreliminar", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.fase_sem_produto_preliminar", "A fase admite recurso e não publica nenhum produto com papel preliminar")),
        new("RegraRecursoFase.AncoraNaoEhProdutoPreliminarDaFase", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.ancora_nao_eh_produto_preliminar_da_fase", "O evento que inicia o prazo do recurso da fase tem de ser a publicação de um dos produtos preliminares da própria fase; escolha um deles")),
        new("RegraRecursoFase.AncoraEmAtoCongelante", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.ancora_em_ato_congelante", "O evento que inicia o prazo do recurso da fase nunca é o ato que congela a configuração; escolha outro ato")),
        new("RegraRecursoFase.AncoraEmAtoIrreversivel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.ancora_em_ato_irreversivel", "Não cabe recurso contra ato de efeito irreversível")),
        new("RegraRecursoFase.RegraCatalogoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.regra_catalogo_invalida", "A regra indicada não permite contar o prazo de recurso a partir de uma publicação")),
    ];

    /// <summary>
    /// Os prazos, o efeito suspensivo e a guarda da vaga do recurso da fase. Os quatro códigos de prazo valem para o prazo de interposição e para o da 2ª instância, que se distinguem pelo campo e pela mensagem. O estado que o requisito proíbe é recusado pela construção da entidade, venha de onde vier, inclusive da leitura do registro da versão publicada, que não passa pelo validator HTTP.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> PrazoDoRecursoDaFase() =>
    [
        new("RegraRecursoFase.PrazoNaoPositivo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_recurso_da_fase.prazo_nao_positivo", "O prazo do recurso deve ser maior que zero")),
        new("RegraRecursoFase.PrazoEmDiasCorridos", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_recurso_da_fase.prazo_em_dias_corridos", "O prazo do recurso não aceita dias corridos")),
        new("RegraRecursoFase.PrazoSemUnidadeDeclaravel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_recurso_da_fase.prazo_sem_unidade_declaravel", "O prazo do recurso precisa declarar dias úteis ou horas")),
        new("RegraRecursoFase.PrazoEmFracaoDeDiaUtil", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_recurso_da_fase.prazo_em_fracao_de_dia_util", "O prazo do recurso em dias úteis exige número inteiro")),
        new("RegraRecursoFase.EfeitoSuspensivoNaoDeclarado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_recurso_da_fase.efeito_suspensivo_nao_declarado", "O período de recurso precisa declarar um efeito suspensivo aceito")),
        new("RegraRecursoFase.PrazoSegundaInstanciaNaoDeclarado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_recurso_da_fase.prazo_segunda_instancia_nao_declarado", "O recurso que admite instância superior precisa declarar o prazo da 2ª instância")),
        new("RegraRecursoFase.PrazoSegundaInstanciaSemInstanciaSuperior", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_recurso_da_fase.prazo_segunda_instancia_sem_instancia_superior", "Sem instância superior, não há prazo de 2ª instância")),
        new("RegraRecursoFase.NaoGuardaComInstanciaSuperior", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_recurso_da_fase.nao_guarda_com_instancia_superior", "Com instância superior, a vaga fica guardada até o resultado definitivo")),
        new("RegraRecursoFase.GuardaDaVagaNaoDeclaravel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_do_recurso_da_fase.guarda_da_vaga_nao_declaravel", "A guarda da vaga declarada não é uma das opções aceitas")),
    ];
}
