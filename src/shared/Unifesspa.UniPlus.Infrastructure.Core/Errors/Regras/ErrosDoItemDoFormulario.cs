namespace Unifesspa.UniPlus.Infrastructure.Core.Errors.Regras;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Regras.Errors;

/// <summary>Códigos dos campos do formulário: município, impedimento, dados do fato, coerência, quantidade e opções.</summary>
internal static class ErrosDoItemDoFormulario
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Mapeamentos =
    [
        new(ItemFormularioErrorCodes.MunicipioSemUf, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.municipio_sem_uf", "O campo de município cita a UF respondida antes")),
        new(ItemFormularioErrorCodes.UfDoMunicipioInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.uf_do_municipio_invalida", "Os municípios da UF citam um campo de UF do Geo")),
        new(ItemFormularioErrorCodes.ImpedimentoSemCondicao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.impedimento_sem_condicao", "O impedimento do campo não tem condição")),
        new(ItemFormularioErrorCodes.ImpedimentoSemOProprioCampo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.impedimento_sem_o_proprio_campo", "Cada cláusula do impedimento cita a resposta do próprio campo")),
        new(ItemFormularioErrorCodes.ImpedimentoMensagemInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.impedimento_mensagem_invalida", "A mensagem do impedimento é obrigatória e limitada")),
        new(ItemFormularioErrorCodes.ImpedimentoNaoCabeNoCampo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.impedimento_nao_cabe_no_campo", "O tipo de campo não aceita impedimento")),
        new(ItemFormularioErrorCodes.ImpedimentoForaDaInscricao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.impedimento_fora_da_inscricao", "Só o formulário de inscrição tem impedimento")),
        new(ItemFormularioErrorCodes.FatoCodigoObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.fato_codigo_obrigatorio", "O código do fato do item é obrigatório")),
        new(ItemFormularioErrorCodes.FatoCodigoTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.fato_codigo_tamanho", "O código do fato do item excede o tamanho máximo")),
        new(ItemFormularioErrorCodes.OrdemInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.ordem_invalida", "A ordem do item não pode ser negativa")),
        new(ItemFormularioErrorCodes.RotuloObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.rotulo_obrigatorio", "O rótulo do item é obrigatório")),
        new(ItemFormularioErrorCodes.RotuloTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.rotulo_tamanho", "O rótulo do item excede o tamanho máximo")),
        new(ItemFormularioErrorCodes.TipoDeCampoObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.tipo_de_campo_obrigatorio", "O tipo de campo do item é obrigatório")),
        new(ItemFormularioErrorCodes.TipoDeCampoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.tipo_de_campo_invalido", "O tipo de campo do item não é reconhecido")),
        new(ItemFormularioErrorCodes.AjudaTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.ajuda_tamanho", "Ajuda do campo acima do tamanho máximo")),
        new(ItemFormularioErrorCodes.FormatoIncoerente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.formato_incoerente", "Formato do campo incoerente com o tipo de campo")),
        new(ItemFormularioErrorCodes.RegraAutorreferente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.regra_autorreferente", "Uma regra do item cita o próprio fato")),
        new(ItemFormularioErrorCodes.RestricaoIncoerente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.restricao_incoerente", "A restrição de valor não se aplica ao tipo do campo")),
        new(ItemFormularioErrorCodes.OpcionalQueAlimentaRegra, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.opcional_que_alimenta_regra", "Campo que alimenta derivação, agregado ou negação precisa ser obrigatório sempre que exibido")),
        new(ItemFormularioErrorCodes.TipoDeCampoIncoerenteComDominio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.tipo_de_campo_incoerente_com_dominio", "O tipo de campo não é coerente com o domínio do dado")),
        new(ItemFormularioErrorCodes.FatoDesconhecido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.fato_desconhecido", "O dado do candidato do item não existe no catálogo")),
        new(ItemFormularioErrorCodes.FatoNaoColetavel, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.fato_nao_coletavel", "O dado do candidato do item não é coletável num campo")),
        new(ItemFormularioErrorCodes.ItensEmExcesso, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.itens_em_excesso", "O formulário excede o teto de itens")),
        new(ItemFormularioErrorCodes.OpcoesDeOutroDominio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.item_formulario.opcoes_de_outro_dominio", "As opções vêm de um campo com outra fonte ou outro domínio de valores")),
    ];
}
