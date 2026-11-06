namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Fato do candidato (UNI-REQ-0077, ADR-0111, ADR-0116).</summary>
internal static class FatoCandidatoErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(FatoCandidatoErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.codigo_obrigatorio",
                "Código do fato é obrigatório")),

        new(FatoCandidatoErrorCodes.CodigoFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.codigo_formato_invalido",
                "Código do fato em formato inválido")),

        new(FatoCandidatoErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.nome_obrigatorio",
                "Nome do fato é obrigatório")),

        new(FatoCandidatoErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.nome_tamanho",
                "Tamanho do nome do fato inválido")),

        new(FatoCandidatoErrorCodes.DescricaoObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.descricao_obrigatoria",
                "Descrição do fato é obrigatória")),

        new(FatoCandidatoErrorCodes.ComponenteExibicaoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.componente_exibicao_invalido",
                "Componente de exibição do fato fora do vocabulário fechado")),

        new(FatoCandidatoErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.descricao_tamanho",
                "Tamanho da descrição do fato inválido")),

        new(FatoCandidatoErrorCodes.DominioObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.dominio_obrigatorio",
                "Domínio do fato é obrigatório")),

        new(FatoCandidatoErrorCodes.DominioInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.dominio_invalido",
                "Domínio do fato fora do vocabulário fechado")),

        new(FatoCandidatoErrorCodes.OrigemObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.origem_obrigatoria",
                "Origem do fato é obrigatória")),

        new(FatoCandidatoErrorCodes.OrigemInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.origem_invalida",
                "Origem do fato fora do vocabulário fechado")),

        new(FatoCandidatoErrorCodes.CardinalidadeObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.cardinalidade_obrigatoria",
                "Cardinalidade do fato é obrigatória")),

        new(FatoCandidatoErrorCodes.CardinalidadeInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.cardinalidade_invalida",
                "Cardinalidade do fato fora do vocabulário fechado")),

        new(FatoCandidatoErrorCodes.FonteValoresObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.fonte_valores_obrigatoria",
                "Fato categórico precisa declarar a fonte dos seus valores")),

        new(FatoCandidatoErrorCodes.FonteValoresForaDeCategorico,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.fonte_valores_fora_de_categorico",
                "Só fato categórico declara a fonte dos seus valores")),

        new(FatoCandidatoErrorCodes.FormatoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.formato_obrigatorio",
                "Fato de domínio texto precisa declarar o formato")),

        new(FatoCandidatoErrorCodes.FormatoForaDeTexto,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.formato_fora_de_texto",
                "Só fato de domínio texto declara formato")),

        new(FatoCandidatoErrorCodes.ClassificacaoAbaixoDoMinimoDoDominio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.classificacao_abaixo_do_minimo_do_dominio",
                "Classificação de proteção de dados incompatível com o domínio do fato")),

        new(FatoCandidatoErrorCodes.EscopoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.escopo_obrigatorio",
                "Escopo do fato é obrigatório")),

        new(FatoCandidatoErrorCodes.ClassificacaoProtecaoObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.classificacao_protecao_obrigatoria",
                "Classificação de proteção de dados do fato é obrigatória")),

        new(FatoCandidatoErrorCodes.ClassificacaoProtecaoInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.classificacao_protecao_invalida",
                "Classificação de proteção de dados do fato fora do vocabulário fechado")),

        new(FatoCandidatoErrorCodes.FinalidadeTratamentoObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.finalidade_tratamento_obrigatoria",
                "Finalidade do tratamento do fato é obrigatória")),

        new(FatoCandidatoErrorCodes.FinalidadeTratamentoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.finalidade_tratamento_tamanho",
                "Finalidade do tratamento acima do tamanho máximo")),

        new(FatoCandidatoErrorCodes.HipoteseLegalObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.hipotese_legal_obrigatoria",
                "Hipótese legal de tratamento do fato é obrigatória")),

        new(FatoCandidatoErrorCodes.HipoteseLegalInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.hipotese_legal_invalida",
                "Hipótese legal de tratamento do fato fora do vocabulário fechado")),

        new(FatoCandidatoErrorCodes.HipoteseLegalIncompativelComClassificacao,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.hipotese_legal_incompativel_com_classificacao",
                "A hipótese legal não cabe na classificação de proteção do fato")),

        new(FatoCandidatoErrorCodes.DefinicaoDoFatoDeSistemaTravada,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.definicao_do_fato_de_sistema_travada",
                "A definição do fato de sistema é travada: só o nome, a descrição e a proteção de dados se editam")),

        new(FatoCandidatoErrorCodes.VinculoExclusivoDeFatoDeSistema,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.vinculo_exclusivo_de_fato_de_sistema",
                "Fato do administrador é declarado ou derivado por regra")),

        new(FatoCandidatoErrorCodes.JaAtivo,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.ja_ativo",
                "O fato já está ativo")),

        new(FatoCandidatoErrorCodes.JaDesativado,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.ja_desativado",
                "O fato já está desativado")),

        new(FatoCandidatoErrorCodes.PontoResolucaoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.ponto_resolucao_obrigatorio",
                "Ponto de resolução do fato é obrigatório")),

        new(FatoCandidatoErrorCodes.PontoResolucaoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.ponto_resolucao_invalido",
                "Ponto de resolução do fato fora do conjunto canônico de fases")),

        new(FatoCandidatoErrorCodes.BindingObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.binding_obrigatorio",
                "Binding do fato é obrigatório")),

        new(FatoCandidatoErrorCodes.BindingFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.binding_formato_invalido",
                "Binding do fato em formato inválido")),

        new(FatoCandidatoErrorCodes.BindingPrefixoIncoerenteComOrigem,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.binding_prefixo_incoerente_com_origem",
                "Prefixo do binding incoerente com a origem do fato")),

        new(FatoCandidatoErrorCodes.BindingReferenciaRegraIncoerente,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.binding_referencia_regra_incoerente",
                "Binding de regra de derivação referencia outro fato, não o próprio")),

        new(FatoCandidatoErrorCodes.DerivadoPorRegraSoBooleanoOuCategorico,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.derivado_por_regra_so_booleano_ou_categorico",
                "Fato derivado por regra é booleano ou categórico")),

        new(FatoCandidatoErrorCodes.RegrasPadraoSoEmDerivadoPorRegra,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.regras_padrao_so_em_derivado_por_regra",
                "Só o fato derivado por regra tem regras padrão")),

        new(FatoCandidatoErrorCodes.RegraCitaFatoNaoCitavel,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.regra_cita_fato_nao_citavel",
                "A regra cita fato que não entra em predicado")),

        new(FatoCandidatoErrorCodes.RegraCitaFatoDesativado,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.regra_cita_fato_desativado",
                "A regra passa a citar fato desativado")),

        new(FatoCandidatoErrorCodes.RegraCitaValorDesativado,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.regra_cita_valor_desativado",
                "A regra passa a citar valor desativado")),

        new(FatoCandidatoErrorCodes.ClassificacaoAbaixoDaDependencia,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.classificacao_abaixo_da_dependencia",
                "O derivado protege menos que um fato que ele cita")),

        new(FatoCandidatoErrorCodes.AgregadoSemFatoDeMembro,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.agregado_sem_fato_de_membro",
                "O fato de membro do agregado não pertence ao catálogo")),

        new(FatoCandidatoErrorCodes.AgregadoSobreFatoQueNaoEhCampoDeGrupo,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.agregado_sobre_fato_que_nao_eh_campo_de_grupo",
                "O agregado resume fato declarado de membro de grupo")),

        new(FatoCandidatoErrorCodes.AgregadoSobreDominioQueNaoAgrega,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.agregado_sobre_dominio_que_nao_agrega",
                "O agregado resume fato de membro booleano ou categórico")),

        new(FatoCandidatoErrorCodes.AgregadoNaoTemValoresProprios,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.agregado_nao_tem_valores_proprios",
                "Os valores do agregado são os do fato de membro")),

        new(FatoCandidatoErrorCodes.PontoResolucaoAnteriorADependencia,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.ponto_resolucao_anterior_a_dependencia",
                "O derivado resolve antes de um fato que ele cita")),

        new(FatoCandidatoErrorCodes.CicloEntreDerivados,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.ciclo_entre_derivados",
                "As regras fecham ciclo entre derivados")),

        new(FatoCandidatoErrorCodes.EixoNaoEditavelNoTipoDoFato,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.eixo_nao_editavel_no_tipo_do_fato",
                "Eixo que o tipo do fato não permite alterar")),

        new(FatoCandidatoErrorCodes.DominioNaoComportaOsValores,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.dominio_nao_comporta_os_valores",
                "O domínio novo não comporta os valores do fato")),

        new(FatoCandidatoErrorCodes.FonteNaoComportaOsValores,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.fonte_nao_comporta_os_valores",
                "A fonte nova não comporta os valores do fato")),

        new(FatoCandidatoErrorCodes.AlteracaoQuebraRegraPadraoDeOutroFato,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.alteracao_quebra_regra_padrao_de_outro_fato",
                "A alteração deixaria de valer a regra padrão de outro fato")),

        new(FatoCandidatoErrorCodes.AlteracaoQuebraAgregadoDoFato,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.alteracao_quebra_agregado_do_fato",
                "A alteração deixaria de valer o agregado do fato")),

        new(FatoCandidatoErrorCodes.AlteracaoQuebraModeloDeFormulario,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.alteracao_quebra_modelo_de_formulario",
                "A alteração deixaria de valer um modelo de formulário")),

        new(FatoCandidatoErrorCodes.AlteracaoQuebraRegraDeAplicabilidade,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.alteracao_quebra_regra_de_aplicabilidade",
                "A alteração deixaria de valer a regra de aplicabilidade de uma modalidade")),

        new(FatoCandidatoErrorCodes.RegraPadraoMalformada,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.fato_candidato.regra_padrao_malformada",
                "A lista de regras, uma regra ou uma condição veio nula")),

        new(FatoCandidatoErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.fato_candidato.codigo_ja_existe",
                "Já existe um fato com o código informado")),

        new(FatoCandidatoErrorCodes.ConflitoDeConcorrencia,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.fato_candidato.conflito_de_concorrencia",
                "O fato foi alterado concorrentemente")),

        new(FatoCandidatoErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.fato_candidato.nao_encontrado",
                "Fato do candidato não encontrado")),
    ];
}
