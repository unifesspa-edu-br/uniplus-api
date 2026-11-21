namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Tipo de banca (UNI-REQ-0139).</summary>
internal static class TipoBancaErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(TipoBancaErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_banca.codigo_obrigatorio",
                "Código do tipo de banca é obrigatório")),

        new(TipoBancaErrorCodes.CodigoForaDoConjuntoCanonico,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_banca.codigo_fora_do_conjunto_canonico",
                "Código fora do vocabulário de tipos de banca")),

        new(TipoBancaErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.tipo_banca.codigo_ja_existe",
                "Já existe um tipo de banca ativo com este código")),

        new(TipoBancaErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_banca.nome_obrigatorio",
                "Nome do tipo de banca é obrigatório")),

        new(TipoBancaErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_banca.nome_tamanho",
                "Tamanho do nome do tipo de banca inválido")),

        new(TipoBancaErrorCodes.FaseTipicaTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_banca.fase_tipica_tamanho",
                "Tamanho da fase típica do tipo de banca inválido")),

        new(TipoBancaErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_banca.descricao_tamanho",
                "Tamanho da descrição do tipo de banca inválido")),

        new(TipoBancaErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.tipo_banca.nao_encontrado",
                "Tipo de banca não encontrado")),
        new(TipoBancaErrorCodes.PapelObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_banca.papel_obrigatorio",
                "O papel do tipo de banca é obrigatório")),

        new(TipoBancaErrorCodes.PapelForaDoVocabulario,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_banca.papel_fora_do_vocabulario",
                "O papel não pertence ao vocabulário de papéis")),

        new(TipoBancaErrorCodes.TipoDeDecisaoForaDoVocabulario,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_banca.tipo_de_decisao_fora_do_vocabulario",
                "Um tipo de decisão julgado não pertence ao vocabulário")),

        new(TipoBancaErrorCodes.TipoDeDecisaoRepetido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_banca.tipo_de_decisao_repetido",
                "Um tipo de decisão aparece mais de uma vez")),

        new(TipoBancaErrorCodes.TiposDeDecisaoIncoerentesComOPapel,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_banca.tipos_de_decisao_incoerentes_com_o_papel",
                "Só o tipo que avalia o mérito declara tipos de decisão, e declara ao menos um")),
    ];
}
