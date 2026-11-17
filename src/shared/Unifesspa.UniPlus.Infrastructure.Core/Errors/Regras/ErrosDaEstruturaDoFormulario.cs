namespace Unifesspa.UniPlus.Infrastructure.Core.Errors.Regras;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>Códigos da estrutura do formulário: etapas, seções, blocos, finalidade, título e posição dos itens nas seções.</summary>
internal static class ErrosDaEstruturaDoFormulario
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Mapeamentos =
    [
        new("EstruturaFormulario.EtapaCodigoDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.etapa_codigo_duplicado", "O código de etapa se repete no formulário")),
        new("EstruturaFormulario.EtapaOrdemDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.etapa_ordem_duplicada", "Duas etapas do formulário têm a mesma ordem")),
        new("EstruturaFormulario.TipoDeEtapaObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.tipo_de_etapa_obrigatorio", "A etapa é uma seção ou um bloco de sistema")),
        new("EstruturaFormulario.SecaoReservada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.secao_reservada", "A seção dos dados básicos do candidato é reservada")),
        new("EstruturaFormulario.SecaoReservadaAlterada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.secao_reservada_alterada", "A seção dos dados básicos do candidato não pode ser alterada")),
        new("EstruturaFormulario.DadoBasicoAlterado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.dado_basico_alterado", "O item do conjunto básico de dados do candidato não pode ser alterado")),
        new("EstruturaFormulario.SecaoComBloco", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.secao_com_bloco", "Uma seção não declara bloco de sistema")),
        new("EstruturaFormulario.BlocoNaoAdmitido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.bloco_nao_admitido", "O bloco não é admitido pela finalidade do formulário")),
        new("EstruturaFormulario.BlocoRepetido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.bloco_repetido", "O bloco aparece mais de uma vez no formulário")),
        new("EstruturaFormulario.BlocoExigidoAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.bloco_exigido_ausente", "Falta um bloco que a finalidade exige")),
        new("EstruturaFormulario.RevisaoEAceiteForaDoFim", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.revisao_e_aceite_fora_do_fim", "A revisão e aceite é sempre a última etapa")),
        new("EstruturaFormulario.ItemForaDeSecao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.item_fora_de_secao", "O item precisa estar numa seção do formulário")),
        new("EstruturaFormulario.ItemForaDaOrdemDasSecoes", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.item_fora_da_ordem_das_secoes", "A ordem dos itens não acompanha a ordem das seções")),
        new(EstruturaFormularioErrorCodes.FinalidadeInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.finalidade_invalida", "A finalidade do formulário é inscrição, isenção de taxa ou habilitação")),
        new(EstruturaFormularioErrorCodes.TituloTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.titulo_tamanho", "O título do formulário excede o tamanho máximo")),
        new(EstruturaFormularioErrorCodes.EtapaCodigoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.etapa_codigo_invalido", "O código da etapa é obrigatório e tem tamanho limitado")),
        new(EstruturaFormularioErrorCodes.EtapaOrdemInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.etapa_ordem_invalida", "A ordem da etapa não pode ser negativa")),
        new(EstruturaFormularioErrorCodes.EtapaTituloInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.etapa_titulo_invalido", "O título da etapa é obrigatório e tem tamanho limitado")),
        new(EstruturaFormularioErrorCodes.EtapaTextoTamanho, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.etapa_texto_tamanho", "A descrição ou o aviso da etapa excede o tamanho máximo")),
        new(EstruturaFormularioErrorCodes.ExibicaoForaDeSecao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.estrutura_formulario.exibicao_fora_de_secao", "Só a seção do formulário tem exibição condicional")),
    ];
}
