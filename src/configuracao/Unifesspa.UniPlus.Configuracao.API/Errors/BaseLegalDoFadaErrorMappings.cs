namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

internal static class BaseLegalDoFadaErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(BaseLegalDoFadaErrorCodes.TipoInstrumentoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_do_fada.tipo_instrumento_invalido",
                "Tipo de instrumento normativo inválido ou não informado")),

        new(BaseLegalDoFadaErrorCodes.IdentificacaoObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_do_fada.identificacao_obrigatoria",
                "Identificação da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) é obrigatória")),

        new(BaseLegalDoFadaErrorCodes.IdentificacaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_do_fada.identificacao_tamanho",
                "Tamanho da identificação da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) inválido")),

        new(BaseLegalDoFadaErrorCodes.IdentificacaoCaractereInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_do_fada.identificacao_caractere_invalido",
                "A identificação da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) contém o caractere nulo ou caractere que não é texto")),

        new(BaseLegalDoFadaErrorCodes.DescricaoObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_do_fada.descricao_obrigatoria",
                "Descrição da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) é obrigatória")),

        new(BaseLegalDoFadaErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_do_fada.descricao_tamanho",
                "Tamanho da descrição da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) inválido")),

        new(BaseLegalDoFadaErrorCodes.DescricaoCaractereInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_do_fada.descricao_caractere_invalido",
                "A descrição da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) contém o caractere nulo, de controle ou de formatação")),

        new(BaseLegalDoFadaErrorCodes.SemMunicipios,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_do_fada.sem_municipios",
                "A base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) deve abranger pelo menos um município")),

        new(BaseLegalDoFadaErrorCodes.MunicipioInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_do_fada.municipio_invalido",
                "Município informado é inválido")),

        new(BaseLegalDoFadaErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.base_legal_do_fada.nao_encontrado",
                "Base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) não encontrada")),

        new(BaseLegalDoFadaErrorCodes.CodigoFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_do_fada.codigo_formato_invalido",
                "Código da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) fora do formato")),

        new(BaseLegalDoFadaErrorCodes.CodigoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.base_legal_do_fada.codigo_tamanho",
                "Código da base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) passa do tamanho máximo")),

        new(BaseLegalDoFadaErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.base_legal_do_fada.codigo_ja_existe",
                "Já existe uma base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) com este código")),
    ];
}
