namespace Unifesspa.UniPlus.Infrastructure.Core.Errors.Regras;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>Códigos da estrutura do formulário: páginas, seções, blocos, finalidade, título e posição dos itens nas seções.</summary>
internal static class ErrosDaEstruturaDoFormulario
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Mapeamentos =
    [
        new("EstruturaFormulario.PaginaCodigoDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.pagina_codigo_duplicado", "O código de página se repete no formulário")),
        new("EstruturaFormulario.PaginaOrdemDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.pagina_ordem_duplicada", "Duas páginas do formulário têm a mesma ordem")),
        new("EstruturaFormulario.TipoDePaginaObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.tipo_de_pagina_obrigatorio", "A página é uma seção ou um bloco de sistema")),
        new("EstruturaFormulario.SecaoReservada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.secao_reservada", "A seção dos dados básicos do candidato é reservada")),
        new("EstruturaFormulario.SecaoReservadaAlterada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.secao_reservada_alterada", "A seção dos dados básicos do candidato não pode ser alterada")),
        new("EstruturaFormulario.DadoBasicoAlterado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.dado_basico_alterado", "O item do conjunto básico de dados do candidato não pode ser alterado")),
        new("EstruturaFormulario.SecaoComBloco", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.secao_com_bloco", "Uma seção não declara bloco de sistema")),
        new("EstruturaFormulario.BlocoNaoAdmitido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.bloco_nao_admitido", "O bloco não é admitido pela finalidade do formulário")),
        new("EstruturaFormulario.BlocoRepetido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.bloco_repetido", "O bloco aparece mais de uma vez no formulário")),
        new("EstruturaFormulario.BlocoExigidoAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.bloco_exigido_ausente", "Falta um bloco que a finalidade exige")),
        new("EstruturaFormulario.RevisaoEAceiteForaDoFim", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.revisao_e_aceite_fora_do_fim", "A revisão e aceite é sempre a última página")),
        new("EstruturaFormulario.ItemForaDeSecao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.item_fora_de_secao", "O item precisa estar numa seção do formulário")),
        new("EstruturaFormulario.ItemForaDaOrdemDasSecoes", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.item_fora_da_ordem_das_secoes", "A ordem dos itens não acompanha a ordem das seções")),
        new(EstruturaFormularioErrorCodes.FinalidadeInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.finalidade_invalida", "A finalidade do formulário é inscrição, isenção de taxa ou habilitação")),
        new(EstruturaFormularioErrorCodes.TituloTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.titulo_tamanho", "O título do formulário excede o tamanho máximo")),
        new(EstruturaFormularioErrorCodes.PaginaCodigoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.pagina_codigo_invalido", "O código da página é obrigatório e tem tamanho limitado")),
        new(EstruturaFormularioErrorCodes.PaginaOrdemInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.pagina_ordem_invalida", "A ordem da página não pode ser negativa")),
        new(EstruturaFormularioErrorCodes.PaginaTituloInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.pagina_titulo_invalido", "O título da página é obrigatório e tem tamanho limitado")),
        new(EstruturaFormularioErrorCodes.PaginaTextoTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.pagina_texto_tamanho", "A descrição ou o aviso da página excede o tamanho máximo")),
        new(EstruturaFormularioErrorCodes.ExibicaoForaDeSecao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.exibicao_fora_de_secao", "Só a seção do formulário tem exibição condicional")),
    ];
}
