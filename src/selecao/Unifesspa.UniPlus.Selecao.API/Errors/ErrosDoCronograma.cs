namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Recusas do cronograma de fases, das etapas e dos recursos.
/// </summary>
internal static class ErrosDoCronograma
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        // Cronograma de fases (§3.6/§3.7): estrutura, janelas, precedência,
        // piso mínimo derivado da origem dos candidatos/vagas ofertadas e o gate de
        // recurso. Todos 422 (ADR-0102) — mais 3 "NaoEncontrada"/"NaoEncontrado", no MESMO molde de
        // ConfiguracaoDistribuicaoVagas.OfertaCursoNaoEncontrada/RegraDistribuicaoNaoEncontrada
        // acima, para a resolução cross-módulo de FaseCanonica/TipoBanca/tipo de ato
        // produzido — necessários para o handler recusar um id que não resolve.
        new("ProcessoSeletivo.CronogramaFasesVazio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.cronograma_fases_vazio", "O processo deve ter ao menos uma fase no cronograma")),
        new("ProcessoSeletivo.OrdemFaseDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.ordem_fase_duplicada", "Cada fase deve ter uma ordem única dentro do cronograma")),
        new("ProcessoSeletivo.FaseCanonicaDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fase_canonica_duplicada", "A mesma fase canônica não pode aparecer duas vezes no cronograma")),
        new("ProcessoSeletivo.PrecedenciaFaseViolada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.precedencia_fase_violada", "A ordem declarada viola a precedência entre fases do cadastro")),
        new("ProcessoSeletivo.SobreposicaoDeJanelasNaoPermitida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.sobreposicao_de_janelas_nao_permitida", "As janelas de duas fases dependentes se sobrepõem, e o cadastro não permite sobreposição")),
        new("ProcessoSeletivo.AvaliacaoSemEtapa", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.avaliacao_sem_etapa", "Há fase que agrupa etapas, mas o processo não tem etapa pontuada")),
        new("ProcessoSeletivo.EtapaSemFaseDeAvaliacao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_sem_fase_de_avaliacao", "Há etapa pontuada, mas nenhuma fase do cronograma agrupa etapas")),
        new("ProcessoSeletivo.EtapaSemFaseDeclarada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_sem_fase_declarada", "A etapa não declara a fase em que acontece")),
        new("FaseCronograma.UnidadeDeFaseIniciadaNaoMuda", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.unidade_de_fase_iniciada_nao_muda", "A unidade responsável por uma fase iniciada não muda")),
        new("EtapaProcesso.BancaDuplicadaNaEtapa", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.banca_duplicada_na_etapa", "O mesmo tipo de banca foi requerido duas vezes na etapa")),
        new("EtapaProcesso.JanelaInvertida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.janela_invertida", "O fim da janela da etapa é anterior ao início")),
        new("EtapaProcesso.TipoBancaNaoEncontradoOuInativo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.tipo_banca_nao_encontrado_ou_inativo", "O tipo de banca não existe ou está inativo no cadastro")),
        new("EtapaProcesso.AncoraNaoEhProdutoPreliminarDaEtapa", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.ancora_nao_eh_produto_preliminar_da_etapa", "O recurso contra ato publicado ancora num resultado preliminar da própria etapa")),
        new("EtapaProcesso.CienciaSemParecerIndividual", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.ciencia_sem_parecer_individual", "O prazo contado da ciência exige que a etapa prometa parecer individual")),
        new("EtapaProcesso.RecursoDuplicadoNaEtapa", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.recurso_duplicado_na_etapa", "Cada fato recorrível abre uma única janela de interposição")),
        new("RecursoDaEtapa.RegraCatalogoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.regra_catalogo_invalida", "A regra declarada não é uma regra de prazo de recurso do catálogo")),
        new("RecursoDaEtapa.AncoraEmAtoCongelante", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.ancora_em_ato_congelante", "A âncora do recurso da etapa não pode ser o ato que congela a configuração")),
        new("RecursoDaEtapa.AncoraEmAtoIrreversivel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.ancora_em_ato_irreversivel", "A âncora do recurso da etapa não pode ser ato de efeito irreversível")),
        new("RecursoDaEtapa.AncoraObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.ancora_obrigatoria", "Declare de que instante o prazo corre: da publicação do ato ou da ciência do candidato")),
        // A janela da etapa prova as mesmas invariantes de prazo e suspensividade que a regra
        // da fase: o value object é o mesmo, e a recusa precisa nomear quem recusou. Sem estes
        // registros, a mesma configuração recusada com 422 na fase virava 500 na etapa.
        new("RecursoDaEtapa.PrazoNaoPositivo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.prazo_nao_positivo", "O prazo de interposição deve ser estritamente positivo")),
        new("RecursoDaEtapa.PrazoEmDiasCorridos", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.prazo_em_dias_corridos", "Prazo de interposição em dias corridos não é aceito")),
        new("RecursoDaEtapa.PrazoSemUnidadeDeclaravel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.prazo_sem_unidade_declaravel", "O prazo de interposição precisa declarar dias úteis ou horas")),
        new("RecursoDaEtapa.PrazoEmFracaoDeDiaUtil", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.prazo_em_fracao_de_dia_util", "Prazo de interposição em dias úteis exige valor inteiro")),
        new("RecursoDaEtapa.SuspensividadeIncompleta", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.suspensividade_incompleta", "A suspensividade exige valor e unidade juntos, ou nenhum dos dois")),
        new("RecursoDaEtapa.SuspensividadeUnidadeNaoDeclaravel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.suspensividade_unidade_nao_declaravel", "A unidade da suspensividade não é declarável")),
        new("RecursoDaEtapa.SuspensividadeNaoPositiva", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.suspensividade_nao_positiva", "O valor da suspensividade deve ser estritamente positivo")),
        new("RecursoDaEtapa.SuspensividadeSuperiorSemInstanciaSuperior", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.suspensividade_superior_sem_instancia_superior", "A janela que não admite instância superior não declara tempo de bloqueio superior")),
        new("EtapaProcesso.AtoDuplicadoNaEtapa", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.ato_duplicado_na_etapa", "A etapa declara o mesmo tipo de ato com o mesmo papel mais de uma vez")),
        new("ProcessoSeletivo.EtapaSemFaseNoCronograma", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_sem_fase_no_cronograma", "A etapa declara uma fase que não está no cronograma do processo")),
        new("ProcessoSeletivo.EtapaComecaAntesDaFase", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_comeca_antes_da_fase", "A etapa começa antes da fase que a abriga")),
        new("ProcessoSeletivo.EtapaTerminaDepoisDaFase", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_termina_depois_da_fase", "A etapa termina depois da fase que a abriga")),
        new("ProcessoSeletivo.EtapaForaDaJanelaDaFase", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_fora_da_janela_da_fase", "A etapa acontece inteira fora da janela da fase que a abriga")),
        new("ProcessoSeletivo.EtapaReferenciadaPorExigenciaDocumental", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_referenciada_por_exigencia_documental", "A etapa a remover é onde um documento exigido é coletado")),
        new("DocumentoExigido.EtapaNaoPertenceAoProcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.etapa_nao_pertence_ao_processo", "A etapa que coleta o documento não é uma etapa deste processo")),
        new("DocumentoExigido.EtapaNaoPertenceAFase", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.etapa_nao_pertence_a_fase", "A etapa que coleta o documento não acontece na fase em que ele é exigido")),
        new("ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.inscricao_propria_sem_fase_de_coleta", "A origem dos candidatos é inscrição própria, e nenhuma fase coleta inscrição")),
        new("ProcessoSeletivo.VagasSemFaseQueProduzResultado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.vagas_sem_fase_que_produz_resultado", "Há vagas ofertadas, e nenhuma fase do cronograma produz resultado")),
        new(ProcessoSeletivoErrorCodes.TipoDeAtoUnicoDeclaradoMaisDeUmaVez, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.tipo_de_ato_unico_declarado_mais_de_uma_vez", "O tipo de ato que só pode ser publicado uma vez por processo está declarado em mais de um produto do cronograma")),
        // A conclusão do ciclo recursal por matéria. Cada estado tem código próprio porque a
        // remediação difere: declarar a fase que conclui, remover a declaração que não cabe,
        // ou mover a concluinte para depois da fase que ela encerra.
        new("ProcessoSeletivo.ConclusaoNaoDeclarada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.conclusao_nao_declarada", "A fase publica resultado preliminar sem a definitiva da matéria e não declara qual fase a conclui")),
        // A fase que já fecha o ciclo de um tipo com o próprio definitivo não pode apontar uma
        // concluinte que o publica também: o ciclo teria dois definitivos.
        new(ProcessoSeletivoErrorCodes.ConclusaoDuplicada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.conclusao_duplicada", "A fase que fecha o próprio ciclo de um tipo de ato aponta uma concluinte que publica o definitivo do mesmo tipo")),
        // O ciclo da etapa fecha dentro dela: a etapa não tem fase concluinte.
        new(ProcessoSeletivoErrorCodes.EtapaComPreliminarSemDefinitivo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_com_preliminar_sem_definitivo", "A etapa publica resultado preliminar sem o definitivo do mesmo tipo de ato")),
        // A retificação confere a configuração nova contra o que já foi publicado: o ciclo que o
        // preliminar publicado abriu não pode ficar sem o definitivo que o fecha.
        new(ProcessoSeletivoErrorCodes.RetificacaoDesfazCicloRecursalAberto, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.retificacao_desfaz_ciclo_recursal_aberto", "A retificação retira ou muda de ciclo o preliminar publicado ou o definitivo pendente de uma matéria")),
        new("ProcessoSeletivo.ConclusaoDeclaradaSemPreliminar", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.conclusao_declarada_sem_preliminar", "A fase declara uma fase concluinte sem publicar resultado preliminar")),
        new("ProcessoSeletivo.ConclusaoDeclaradaEmFaseQueSeConclui", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.conclusao_declarada_em_fase_que_se_conclui", "A fase declara a si mesma como concluinte do próprio ciclo recursal")),
        new("ProcessoSeletivo.FaseConcluinteForaDoCronograma", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fase_concluinte_fora_do_cronograma", "A fase declarada como concluinte não está no cronograma")),
        new("ProcessoSeletivo.FaseConcluinteSemResultadoDefinitivo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fase_concluinte_sem_resultado_definitivo", "A fase declarada como concluinte não publica resultado definitivo")),
        new("ProcessoSeletivo.FaseConcluinteAntecedeAConcluida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fase_concluinte_antecede_a_concluida", "A fase concluinte não pode anteceder na ordem a fase que ela encerra")),
        new("ProcessoSeletivo.FaseConcluinteComecaAntesDaConcluida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fase_concluinte_comeca_antes_da_concluida", "A janela da fase concluinte começa antes da janela da fase que ela encerra")),
        new("FaseCronograma.JanelaObrigatoriaEmDataNoEdital", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.janela_obrigatoria_em_data_no_edital", "Fase com a data fixada no edital exige início e fim da janela")),
        new("FaseCronograma.JanelaInvertida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.janela_invertida", "O fim da janela da fase não pode anteceder o início")),
        new("FaseCronograma.AtoDuplicadoNaFase", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.ato_duplicado_na_fase", "A fase declara o mesmo tipo de ato com o mesmo papel mais de uma vez")),
        new("FaseCronograma.ParecerIndividualSemResultado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.parecer_individual_sem_resultado", "A fase promete parecer individual e não publica nenhum resultado")),
        new("FaseCronograma.FaseCanonicaNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.fase_canonica_nao_encontrada", "Fase canônica não encontrada ou não está mais viva")),
        new("FaseCronograma.TipoBancaNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.tipo_banca_nao_encontrado", "Tipo de banca não encontrado ou não está mais vivo")),
        new("FaseCronograma.CategoriaDocumentoNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.categoria_documento_nao_encontrada", "Categoria de documento do recorte de competência não encontrada ou não está mais viva")),
        // O recorte de competência é o que separa duas bancas do MESMO tipo na mesma fase.
        // Onde o tipo já identifica sozinho, o recorte é dispensável — e por isso a
        // obrigatoriedade só existe no arranjo ambíguo. Recortes iguais entre duas dessas
        // bancas caem na recusa irmã: cumprem a letra da exigência sem distinguir nada.
        new("BancaRequerida.CategoriaDuplicadaNoRecorte", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca_requerida.categoria_duplicada_no_recorte", "A banca declara a mesma categoria de documento mais de uma vez no recorte de competência")),
        new("BancaRequerida.RecorteDeCompetenciaObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca_requerida.recorte_de_competencia_obrigatorio", "A fase requer mais de uma banca do mesmo tipo e cada uma precisa declarar as categorias de documento que julga")),
        new("BancaRequerida.RecorteDeCompetenciaDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca_requerida.recorte_de_competencia_duplicado", "Duas bancas do mesmo tipo na mesma fase declaram o mesmo recorte de competência")),
        // Cada produto declarado pela fase é resolvido contra o catálogo de Publicações: o
        // tipo existe e está vigente, e o papel só cabe onde o catálogo diz que o ato É
        // resultado — classificação intrínseca do tipo, que nunca varia por edital.
        new("ProdutoDaFase.AtoNaoEncontradoNoCatalogo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.produto_da_fase.ato_nao_encontrado_no_catalogo", "O tipo de ato declarado pela fase não tem versão vigente no catálogo de Publicações")),
        new("ProdutoDaFase.PapelDesconhecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.produto_da_fase.papel_desconhecido", "O papel do produto não é declarável")),
        new("ProdutoDaFase.PapelEmAtoQueNaoEhResultado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.produto_da_fase.papel_em_ato_que_nao_eh_resultado", "Só ato que é resultado no catálogo recebe papel preliminar ou definitivo")),
        // O catálogo também diz o que o tipo NÃO pode ser: o resultado sem preliminar é declarado
        // definitivo, e o tipo que congela a configuração não é item de resultado, porque a
        // correção de um resultado não cria versão nova.
        new(ProcessoSeletivoErrorCodes.ResultadoDeclaradoSemPapel, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.resultado_declarado_sem_papel", "O tipo de ato classificado como resultado precisa declarar papel preliminar ou definitivo")),
        new(ProcessoSeletivoErrorCodes.TipoQueCongelaDeclaradoComoResultado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.tipo_que_congela_declarado_como_resultado", "O tipo de ato que congela a configuração não pode ser declarado como item de resultado")),
        // O produto da etapa é conferido contra o MESMO catálogo, pelas mesmas duas perguntas:
        // a etapa declara o que publicará, e o código declarado ali vira o nome do produto no
        // cronograma público e a chave por onde a restauração da configuração congelada
        // reencontra o produto.
        new("ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.produto_da_etapa.ato_nao_encontrado_no_catalogo", "O tipo de ato declarado pela etapa não tem versão vigente no catálogo de Publicações")),
        new("ProdutoDaEtapa.PapelDesconhecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.produto_da_etapa.papel_desconhecido", "O papel do produto da etapa não é declarável")),
        new("ProdutoDaEtapa.PapelEmAtoQueNaoEhResultado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.produto_da_etapa.papel_em_ato_que_nao_eh_resultado", "Só ato que é resultado no catálogo recebe papel preliminar ou definitivo")),
        // A identidade da regra é o par (codigo, versao): pedir uma versão que não existe de um
        // código que existe é o mesmo 404 de pedir um código inventado, e distinguir os dois
        // revelaria quais códigos há no catálogo a quem só tentou adivinhar.
        new("RegraCatalogo.NaoEncontrada", new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.regra_catalogo.nao_encontrada", "Regra não encontrada no catálogo na versão pedida")),
        // A âncora do prazo é um produto PRELIMINAR da própria fase, referenciado pela
        // identidade da linha. Sem nenhum preliminar não há publicação a recorrer; havendo,
        // a âncora tem de ser um deles.
        new("RegraRecursoFase.FaseSemProdutoPreliminar", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.fase_sem_produto_preliminar", "A fase admite recurso e não publica nenhum produto com papel preliminar")),
        new("RegraRecursoFase.AncoraNaoEhProdutoPreliminarDaFase", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.ancora_nao_eh_produto_preliminar_da_fase", "A âncora do recurso tem de ser um dos produtos preliminares publicados pela própria fase")),
        new("RegraRecursoFase.AncoraEmAtoCongelante", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.ancora_em_ato_congelante", "A âncora do recurso nunca é o ato que congela a configuração")),
        new("RegraRecursoFase.AncoraEmAtoIrreversivel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.ancora_em_ato_irreversivel", "Não cabe recurso contra ato de efeito irreversível")),
        // As duas unidades recusadas na interposição têm causas distintas e remediações
        // distintas (UNI-REQ-0113), então cada uma tem código próprio: quem declarou dia
        // corrido reescreve na unidade do edital; quem declarou fração de dia útil passa
        // a declarar em horas.
        new("RegraRecursoFase.PrazoEmDiasCorridos", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.prazo_em_dias_corridos", "Prazo de interposição em dias corridos não é aceito")),
        new("RegraRecursoFase.PrazoEmFracaoDeDiaUtil", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.prazo_em_fracao_de_dia_util", "Prazo de interposição em dias úteis exige valor inteiro")),
        // Estado que o requisito proíbe e que a construção da entidade recusa, venha de
        // onde vier — inclusive da reidratação do envelope, que não passa pelo validator
        // HTTP. Cada recusa tem código próprio porque a remediação difere: declarar a
        // unidade que falta, corrigir a magnitude, ou completar o par.
        new("RegraRecursoFase.PrazoSemUnidadeDeclaravel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.prazo_sem_unidade_declaravel", "O prazo de interposição precisa declarar dias úteis ou horas")),
        new("RegraRecursoFase.PrazoNaoPositivo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.prazo_nao_positivo", "O prazo de interposição deve ser estritamente positivo")),
        new("RegraRecursoFase.SuspensividadeIncompleta", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.suspensividade_incompleta", "A suspensividade exige valor e unidade juntos, ou nenhum dos dois")),
        new("RegraRecursoFase.SuspensividadeUnidadeNaoDeclaravel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.suspensividade_unidade_nao_declaravel", "A unidade da suspensividade não é declarável")),
        new("RegraRecursoFase.SuspensividadeNaoPositiva", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.suspensividade_nao_positiva", "O valor da suspensividade deve ser estritamente positivo")),
        new("RegraRecursoFase.SuspensividadeSuperiorSemInstanciaSuperior", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.suspensividade_superior_sem_instancia_superior", "A janela que não admite instância superior não declara tempo de bloqueio superior")),
        new("RegraRecursoFase.RegraCatalogoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.regra_catalogo_invalida", "A regra referenciada não é RECURSO-PRAZO-ANCORADO-EM-ATO, ou diverge do rol_de_regras")),
    ];
}
