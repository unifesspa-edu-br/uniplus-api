namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Tipo de etapa (UNI-REQ-0015, UNI-REQ-0087).</summary>
internal static class TipoEtapaErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(TipoEtapaErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_etapa.codigo_obrigatorio", "Código do tipo de etapa é obrigatório")),
        new(TipoEtapaErrorCodes.CodigoTamanho,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_etapa.codigo_tamanho", "Tamanho do código do tipo de etapa inválido")),
        new(TipoEtapaErrorCodes.CodigoComCaractereNulo,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_etapa.codigo_com_caractere_nulo", "Código do tipo de etapa contém U+0000")),
        new(TipoEtapaErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(StatusCodes.Status409Conflict,
                "uniplus.configuracao.tipo_etapa.codigo_ja_existe", "Código do tipo de etapa já está reservado")),
        new(TipoEtapaErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_etapa.nome_obrigatorio", "Nome do tipo de etapa é obrigatório")),
        new(TipoEtapaErrorCodes.NomeTamanho,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_etapa.nome_tamanho", "Tamanho do nome do tipo de etapa inválido")),
        new(TipoEtapaErrorCodes.NomeComCaractereNulo,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_etapa.nome_com_caractere_nulo", "Nome do tipo de etapa contém U+0000")),
        new(TipoEtapaErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_etapa.descricao_tamanho", "Tamanho da descrição do tipo de etapa inválido")),
        new(TipoEtapaErrorCodes.DescricaoComCaractereNulo,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_etapa.descricao_com_caractere_nulo", "Descrição do tipo de etapa contém U+0000")),
        new(TipoEtapaErrorCodes.AdmitePontuacaoObrigatorio,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_etapa.admite_pontuacao_obrigatorio",
                "Declaração de que o tipo de etapa compõe a nota final é obrigatória")),
        new(TipoEtapaErrorCodes.AdmiteEliminacaoObrigatorio,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_etapa.admite_eliminacao_obrigatorio",
                "Declaração de que o tipo de etapa elimina candidato é obrigatória")),
        new(TipoEtapaErrorCodes.SemCaraterAdmitido,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_etapa.sem_carater_admitido",
                "Tipo de etapa não admite nenhum caráter de etapa")),
        new(TipoEtapaErrorCodes.NotaDeOrigemNoEnemExigePontuacao,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_etapa.nota_de_origem_no_enem_exige_pontuacao",
                "Tipo de etapa com nota de origem no ENEM deve admitir compor a nota final")),
        new(TipoEtapaErrorCodes.NotaDeOrigemNoEnemNaoDesativa,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_etapa.nota_de_origem_no_enem_nao_desativa",
                "Tipo de etapa com nota de origem no ENEM não pode ser removido")),
        new(TipoEtapaErrorCodes.NaoEncontrado,
            new DomainErrorMapping(StatusCodes.Status404NotFound,
                "uniplus.configuracao.tipo_etapa.nao_encontrado", "Tipo de etapa não encontrado")),
        new(TipoEtapaErrorCodes.JaDesativado,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.tipo_etapa.ja_desativado", "Tipo de etapa já foi removido")),
    ];
}
