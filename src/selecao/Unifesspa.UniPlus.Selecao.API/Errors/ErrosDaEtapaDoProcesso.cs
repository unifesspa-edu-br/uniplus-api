namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Recusas da etapa do processo: declaração, avaliação, itens e escala de atendimento, caráter e composição da
/// nota. A ordem vale: o registro global guarda o último mapeamento de um código repetido.
/// </summary>
internal static class ErrosDaEtapaDoProcesso
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        .. DeclaracaoDaEtapa(),
        .. AvaliacaoDaEtapa(),
        .. ItensEEscalaDeAtendimento(),
        .. CaraterEComposicaoDaNota(),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DeclaracaoDaEtapa() =>
    [
        new("ProcessoSeletivo.EtapasVazias", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapas_vazias", "O processo deve ter ao menos uma etapa pontuada")),
        new("ProcessoSeletivo.OrdemEtapaDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.ordem_etapa_duplicada", "Ordem de etapa duplicada no processo")),
        new("ProcessoSeletivo.IdEtapaDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.id_etapa_duplicado", "O mesmo Id de etapa foi informado mais de uma vez no payload")),
        new("EtapaProcesso.NomeObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.nome_obrigatorio", "O nome da etapa é obrigatório")),
        new("EtapaProcesso.NomeTamanho", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.nome_tamanho", "O nome da etapa excede o tamanho máximo permitido")),
        new("EtapaProcesso.CaraterObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.carater_obrigatorio", "Caráter da etapa é obrigatório (classificatória, eliminatória ou ambas)")),
        new("EtapaProcesso.PesoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.peso_invalido", "O peso da etapa, quando informado, deve ser maior que zero")),
        new("EtapaProcesso.NotaMinimaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.nota_minima_invalida", "A nota mínima, quando informada, não pode ser negativa")),
        new("EtapaProcesso.NotaMaximaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.nota_maxima_invalida", "A nota máxima, quando informada, deve ser maior que zero")),
        new("EtapaProcesso.PassoDoLancamentoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.passo_do_lancamento_invalido", "O passo do lançamento, quando informado, deve ser maior que zero")),
        new("EtapaProcesso.NotaMinimaAcimaDaNotaMaxima", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.nota_minima_acima_da_nota_maxima", "A nota mínima não pode passar da nota máxima da etapa")),
        new("EtapaProcesso.NotaMaximaForaDoPasso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.nota_maxima_fora_do_passo", "A nota máxima deve ser múltipla do passo do lançamento")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> AvaliacaoDaEtapa() =>
    [
        new("EtapaProcesso.FormaDeAvaliacaoDesconhecida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.forma_de_avaliacao_desconhecida", "A forma de avaliação declarada não existe: use OBJETIVA ou HUMANA")),
        new("EtapaProcesso.AvaliacaoNaEtapaDoEnem", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.avaliacao_na_etapa_do_enem", "A etapa de nota do ENEM não declara forma de avaliação nem regra de consolidação")),
        new("EtapaProcesso.RegraDeConsolidacaoForaDaFormaHumana", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.regra_de_consolidacao_fora_da_forma_humana", "A regra de consolidação só se aplica à etapa de avaliação humana")),
        new("EtapaProcesso.RegraDeConsolidacaoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.regra_de_consolidacao_invalida", "A regra declarada não é uma regra de consolidação do catálogo")),
        new("EtapaProcesso.PassoNaFormaObjetiva", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.passo_na_forma_objetiva", "O passo do lançamento só se aplica à etapa de avaliação humana")),
        new("EtapaProcesso.FormaObjetivaEmTipoQueExigeCorrecaoIndependente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.forma_objetiva_em_tipo_que_exige_correcao_independente", "O tipo da etapa exige correção independente, que só existe na avaliação humana")),
        new("EtapaProcesso.CorrecaoIndependenteComMenosDeDoisCorretores", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.correcao_independente_com_menos_de_dois_corretores", "O tipo da etapa exige correção independente, e a regra de consolidação congelada tem menos de dois corretores distintos")),
        new("EtapaProcesso.RegraDeConsolidacaoNaoResolvida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.regra_de_consolidacao_nao_resolvida", "A regra de consolidação de uma etapa não pôde ser confirmada no catálogo")),
        new("EtapaProcesso.ItensNaEtapaDoEnem", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.itens_na_etapa_do_enem", "A etapa de nota do ENEM não declara itens de avaliação nem escala de atendimento")),
        new("EtapaProcesso.EscalaSemItens", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.escala_sem_itens", "A escala de atendimento só vale em etapa com itens de avaliação")),
        new("EtapaProcesso.EscalaNaFormaObjetiva", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.escala_na_forma_objetiva", "A escala de atendimento só se aplica à etapa de avaliação humana")),
        new("EtapaProcesso.PassoComEscalaDeAtendimento", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.passo_com_escala_de_atendimento", "A etapa com escala de atendimento não declara passo do lançamento")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> ItensEEscalaDeAtendimento() =>
    [
        new("ItemDeAvaliacao.RotuloInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.item_de_avaliacao.rotulo_invalido", "O rótulo do item de avaliação é obrigatório e tem tamanho máximo")),
        new("ItemDeAvaliacao.RotuloDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.item_de_avaliacao.rotulo_duplicado", "O rótulo do item de avaliação não pode se repetir na etapa")),
        new("ItemDeAvaliacao.IdDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.item_de_avaliacao.id_duplicado", "O mesmo id de item de avaliação não pode ser informado duas vezes na etapa")),
        new("ItemDeAvaliacao.PontuacaoMaximaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.item_de_avaliacao.pontuacao_maxima_invalida", "A pontuação máxima do item de avaliação deve ser maior que zero")),
        new("ItemDeAvaliacao.PontuacaoForaDoPasso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.item_de_avaliacao.pontuacao_fora_do_passo", "A pontuação máxima do item deve ser múltipla do passo do lançamento")),
        new("NivelDeAtendimento.RotuloInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.nivel_de_atendimento.rotulo_invalido", "O rótulo do nível de atendimento é obrigatório e tem tamanho máximo")),
        new("NivelDeAtendimento.FracaoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.nivel_de_atendimento.fracao_invalida", "A fração do nível de atendimento deve estar entre 0 e 1")),
        new("NivelDeAtendimento.Duplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.nivel_de_atendimento.duplicado", "O nível de atendimento não pode se repetir na escala")),
        new("NivelDeAtendimento.SemNivelCompleto", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.nivel_de_atendimento.sem_nivel_completo", "A escala de atendimento precisa de um nível que valha a pontuação inteira")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> CaraterEComposicaoDaNota() =>
    [
        new("EtapaProcesso.OrdemInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.ordem_invalida", "A ordem da etapa, quando informada, deve ser maior que zero")),
        new("EtapaProcesso.PesoSemCaraterQuePontua", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.peso_sem_carater_que_pontua", "Peso só se aplica a etapa cujo caráter compõe a nota final")),
        new("EtapaProcesso.NotaMinimaSemCaraterQueElimina", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.nota_minima_sem_carater_que_elimina", "Nota mínima só se aplica a etapa cujo caráter elimina")),
        new("EtapaProcesso.CaraterNaoAdmitidoPeloTipo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.etapa_processo.carater_nao_admitido_pelo_tipo", "O tipo de etapa não admite o caráter declarado")),
        new("ProcessoSeletivo.NenhumaEtapaComponeNota", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.nenhuma_etapa_compoe_nota", "Ao menos uma etapa deve compor a nota final")),
        new("ProcessoSeletivo.EtapaNotaEnemDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_nota_enem_duplicada", "O processo admite uma única etapa de nota do ENEM")),
        new("ProcessoSeletivo.EtapaNotaEnemNaoCompoeNota", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_nota_enem_nao_compoe_nota", "Etapa de nota do ENEM deve compor a média, com caráter classificatória ou ambas e peso")),
        new("ProcessoSeletivo.EtapaNotaEnemComBanca", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_nota_enem_com_banca", "Etapa de nota do ENEM não admite banca")),
        new("ProcessoSeletivo.EtapaNotaEnemComProdutoOuRecursoEmAto", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_nota_enem_com_produto_ou_recurso_em_ato", "Etapa de nota do ENEM não declara produto nem recurso ancorado em ato; o recurso coletivo é o da regra de recurso da fase")),
        new("ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_nota_enem_sem_classificacao_enem", "Etapa de nota do ENEM exige classificação baseada em ENEM pela média ponderada")),
        new("ProcessoSeletivo.EtapaReferenciadaPorClassificacao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_referenciada_por_classificacao", "Etapa referenciada por regra de eliminação da classificação")),
    ];
}
