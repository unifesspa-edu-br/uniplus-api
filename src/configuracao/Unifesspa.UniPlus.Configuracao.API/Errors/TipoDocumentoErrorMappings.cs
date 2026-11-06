namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Tipo de documento (UNI-REQ-0013).</summary>
internal static class TipoDocumentoErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(TipoDocumentoErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.codigo_obrigatorio",
                "Código do tipo de documento é obrigatório")),

        new(TipoDocumentoErrorCodes.CodigoFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.codigo_formato_invalido",
                "Formato do código do tipo de documento inválido")),

        new(TipoDocumentoErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.tipo_documento.codigo_ja_existe",
                "Já existe um tipo de documento ativo com este código")),

        new(TipoDocumentoErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.nome_obrigatorio",
                "Nome do tipo de documento é obrigatório")),

        new(TipoDocumentoErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.nome_tamanho",
                "Tamanho do nome do tipo de documento inválido")),

        new(TipoDocumentoErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.descricao_tamanho",
                "Tamanho da descrição do tipo de documento inválido")),

        new(TipoDocumentoErrorCodes.CategoriaObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.categoria_obrigatoria",
                "Categoria do tipo de documento é obrigatória")),

        new(TipoDocumentoErrorCodes.CategoriaFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.categoria_formato_invalido",
                "Código da categoria do tipo de documento em formato inválido")),

        new(TipoDocumentoErrorCodes.CategoriaNaoEncontrada,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.categoria_nao_encontrada",
                "Categoria de documento não encontrada entre as vivas do cadastro")),

        new(TipoDocumentoErrorCodes.FormatosAceitosTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.formatos_aceitos_tamanho",
                "Tamanho dos formatos aceitos do tipo de documento inválido")),

        new(TipoDocumentoErrorCodes.TamanhoMaximoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.tamanho_maximo_invalido",
                "Tamanho máximo em MB do tipo de documento deve ser positivo")),

        new(TipoDocumentoErrorCodes.TipoEquivalenteTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.tipo_equivalente_tamanho",
                "Tamanho do tipo equivalente inválido")),

        new(TipoDocumentoErrorCodes.TipoEquivalenteIgualCodigo,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.tipo_equivalente_igual_codigo",
                "Um tipo de documento não pode ser equivalente a si mesmo")),

        new(TipoDocumentoErrorCodes.ProtecaoObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.protecao_obrigatoria",
                "Classificação, finalidade e hipótese legal do tipo de documento são obrigatórias")),

        new(TipoDocumentoErrorCodes.ClassificacaoProtecaoInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.classificacao_protecao_invalida",
                "Classificação de proteção de dados do tipo de documento fora do vocabulário fechado")),

        new(TipoDocumentoErrorCodes.FinalidadeTratamentoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.finalidade_tratamento_tamanho",
                "Finalidade do tratamento do tipo de documento acima do tamanho máximo")),

        new(TipoDocumentoErrorCodes.HipoteseLegalInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.hipotese_legal_invalida",
                "Hipótese legal de tratamento do tipo de documento fora do vocabulário fechado")),

        new(TipoDocumentoErrorCodes.HipoteseLegalIncompativelComClassificacao,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_documento.hipotese_legal_incompativel_com_classificacao",
                "A hipótese legal não cabe na classificação de proteção do tipo de documento")),

        new(TipoDocumentoErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.tipo_documento.nao_encontrado",
                "Tipo de documento não encontrado")),
    ];
}
