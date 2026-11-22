namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Recusas independentes da distinção das apresentações.</summary>
internal static class ConferenciaDaDistincao
{
    internal static List<FieldError> Conferir(DistincaoDeclarada dados, int quantidade)
    {
        CriterioDeDistincaoDasApresentacoes criterio = dados.Criterio ?? CriterioDeDistincaoDasApresentacoes.Nenhuma;
        if (!dados.CriterioReconhecido || !Enum.IsDefined(criterio))
        {
            return [Recusa("criterioDeDistincao", "CriterioDeDistincaoInvalido", "O critério para distinguir as apresentações não é reconhecido. Escolha \"COMPETENCIA_MENSAL\" para mês, \"EXERCICIO_ANUAL\" para ano ou \"OCORRENCIA\" para ocorrência.")];
        }
        List<FieldError> erros = [];
        erros.AddRange(ConferirData(criterio, dados.DataReferencia));
        erros.AddRange(ConferirOcorrencias(criterio, dados.OcorrenciasEsperadas, quantidade));
        erros.AddRange(ConferirCalendario(criterio, dados.DataReferencia, quantidade));
        return erros;
    }

    private static IEnumerable<FieldError> ConferirData(CriterioDeDistincaoDasApresentacoes criterio, DateOnly? data)
    {
        if (data is not null && criterio is CriterioDeDistincaoDasApresentacoes.Nenhuma or CriterioDeDistincaoDasApresentacoes.Ocorrencia)
        {
            yield return Recusa("dataReferencia", "DataReferenciaIndevidaParaCriterio",
                "A data de referência não se aplica ao critério escolhido. Retire a data ou escolha um critério por mês ou ano.");
        }
        if (data is null && criterio is CriterioDeDistincaoDasApresentacoes.CompetenciaMensal or CriterioDeDistincaoDasApresentacoes.ExercicioAnual)
        {
            yield return Recusa("dataReferencia", "DataReferenciaObrigatoriaParaCalendario",
                "Este documento é distinguido por mês ou ano e precisa de uma data de referência. Informe a data.");
        }
    }

    private static IEnumerable<FieldError> ConferirOcorrencias(CriterioDeDistincaoDasApresentacoes criterio, IReadOnlyList<string>? ocorrencias, int quantidade)
    {
        if (ocorrencias is null)
        {
            return [];
        }
        if (criterio != CriterioDeDistincaoDasApresentacoes.Ocorrencia)
        {
            return [Recusa("ocorrenciasEsperadas", "OcorrenciasEsperadasIndevidasParaCriterio",
                "A lista de ocorrências esperadas não se aplica ao critério escolhido. Retire a lista ou escolha \"OCORRENCIA\" para distinguir por ocorrência.")];
        }
        return ConferirLista(ocorrencias, quantidade);
    }

    private static IEnumerable<FieldError> ConferirLista(IReadOnlyList<string> ocorrencias, int quantidade)
    {
        if (ocorrencias.Count == 0)
        {
            return [Recusa("ocorrenciasEsperadas", "OcorrenciasEsperadasVazias", "A lista de ocorrências esperadas está vazia. Informe ao menos uma ocorrência ou deixe a lista ausente.")];
        }
        return [.. ConferirIdentificadores(ocorrencias), .. ConferirQuantidade(ocorrencias, quantidade)];
    }

    private static IEnumerable<FieldError> ConferirIdentificadores(IReadOnlyList<string> ocorrencias)
    {
        if (ocorrencias.Any(static id => string.IsNullOrWhiteSpace(id)))
        {
            yield return Recusa("ocorrenciasEsperadas", "OcorrenciaSemIdentificador", "Uma ocorrência esperada não tem identificador. Informe um identificador para cada ocorrência.");
        }
        if (ocorrencias.Distinct(StringComparer.Ordinal).Count() != ocorrencias.Count)
        {
            yield return Recusa("ocorrenciasEsperadas", "OcorrenciaComIdentificadorRepetido", "Há ocorrências esperadas com o mesmo identificador. Informe identificadores distintos.");
        }
    }

    private static IEnumerable<FieldError> ConferirQuantidade(IReadOnlyList<string> ocorrencias, int quantidade) =>
        RegraDeComprovacao.RecusaDaQuantidadeDeApresentacoes(quantidade) is null && quantidade != ocorrencias.Count
            ? [Recusa("quantidadeMinima", "QuantidadeMinimaDiferenteDasOcorrencias",
                "A quantidade mínima não coincide com o número de ocorrências esperadas. Ajuste a quantidade ou a lista.")] : [];

    private static IEnumerable<FieldError> ConferirCalendario(CriterioDeDistincaoDasApresentacoes criterio, DateOnly? data, int quantidade)
    {
        if (data is null || RegraDeComprovacao.RecusaDaQuantidadeDeApresentacoes(quantidade) is not null)
        {
            return [];
        }
        int maisAntiga = criterio == CriterioDeDistincaoDasApresentacoes.CompetenciaMensal
            ? ((data.Value.Year - 1) * 12) + data.Value.Month - (quantidade - 1) : data.Value.Year - (quantidade - 1);
        return criterio is CriterioDeDistincaoDasApresentacoes.CompetenciaMensal or CriterioDeDistincaoDasApresentacoes.ExercicioAnual && maisAntiga < 1
            ? [Recusa("quantidadeMinima", "QuantidadeMinimaExcedeCalendario",
                "A quantidade mínima alcança datas anteriores ao início do calendário. Reduza a quantidade ou escolha outra data de referência.")] : [];
    }

    private static FieldError Recusa(string campo, string codigo, string mensagem) =>
        new(campo, new DomainError($"RegraDeComprovacao.{codigo}", mensagem));
}
