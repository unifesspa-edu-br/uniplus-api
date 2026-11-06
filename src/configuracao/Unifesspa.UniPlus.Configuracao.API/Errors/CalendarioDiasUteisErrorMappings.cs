namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Calendário de dias úteis (UNI-REQ-0116).</summary>
internal static class CalendarioDiasUteisErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(CalendarioDiasUteisErrorCodes.VersaoDatasetObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.calendario_dias_uteis.versao_dataset_obrigatoria",
                "Versão do dataset é obrigatória")),

        new(CalendarioDiasUteisErrorCodes.VersaoDatasetTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.calendario_dias_uteis.versao_dataset_tamanho",
                "Tamanho da versão do dataset inválido")),

        new(CalendarioDiasUteisErrorCodes.SemDiaNaoUtil,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.calendario_dias_uteis.sem_dia_nao_util",
                "O dataset precisa de ao menos um dia não útil")),

        new(CalendarioDiasUteisErrorCodes.AbrangenciaInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.calendario_dias_uteis.abrangencia_invalida",
                "Abrangência do dia não útil fora do domínio fechado")),

        new(CalendarioDiasUteisErrorCodes.SnapshotMunicipalApenasParaMunicipal,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.calendario_dias_uteis.snapshot_municipal_apenas_para_municipal",
                "Referência do município só se aplica a abrangência municipal")),

        new(CalendarioDiasUteisErrorCodes.UfObrigatoriaParaEstadual,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.calendario_dias_uteis.uf_obrigatoria_para_estadual",
                "UF é obrigatória para abrangência estadual")),

        new(CalendarioDiasUteisErrorCodes.UfApenasParaEstadual,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.calendario_dias_uteis.uf_apenas_para_estadual",
                "UF só se aplica a abrangência estadual")),

        new(CalendarioDiasUteisErrorCodes.UfFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.calendario_dias_uteis.uf_formato_invalido",
                "UF em formato inválido")),

        new(CalendarioDiasUteisErrorCodes.DiaNaoUtilNulo,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.calendario_dias_uteis.dia_nao_util_nulo",
                "Item de dia não útil não pode ser nulo")),

        new(CalendarioDiasUteisErrorCodes.DataAusente,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.calendario_dias_uteis.data_ausente",
                "A data do dia não útil não pode ser o valor default")),

        new(CalendarioDiasUteisErrorCodes.DescricaoObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.calendario_dias_uteis.descricao_obrigatoria",
                "Descrição do dia não útil é obrigatória")),

        new(CalendarioDiasUteisErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.calendario_dias_uteis.descricao_tamanho",
                "Tamanho da descrição do dia não útil inválido")),

        new(CalendarioDiasUteisErrorCodes.DataDuplicadaNoDataset,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.calendario_dias_uteis.data_duplicada_no_dataset",
                "Data duplicada no dataset (mesma abrangência e município)")),

        new(CalendarioDiasUteisErrorCodes.JaVigente,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.calendario_dias_uteis.ja_vigente",
                "Este dataset já é o vigente")),

        new(CalendarioDiasUteisErrorCodes.NaoRemoveVigente,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.calendario_dias_uteis.nao_remove_vigente",
                "O dataset vigente não pode ser removido")),

        new(CalendarioDiasUteisErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.calendario_dias_uteis.nao_encontrado",
                "Calendário de dias úteis não encontrado")),

        new(CalendarioDiasUteisErrorCodes.ConflitoDeConcorrencia,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.calendario_dias_uteis.conflito_de_concorrencia",
                "Outra alteração concorrente modificou o mesmo dataset ou o dataset vigente",
                RetryableConflict: true)),
    ];
}
