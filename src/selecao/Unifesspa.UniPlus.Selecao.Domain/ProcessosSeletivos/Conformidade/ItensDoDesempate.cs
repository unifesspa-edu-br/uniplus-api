namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel sobre os critérios de desempate, na ordem do gate.</summary>
internal static class ItensDoDesempate
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(EstadoDaConfiguracao estado)
    {
        List<RegrasDoDesempate.ViolacaoDoDesempatePorArea> desempate = RegrasDoDesempate.ConferirDesempatePorAreaParaPublicar(estado);

        return
        [
            new ItemConformidade("criterios_desempate_em_excesso", DimensaoConformidade.Classificacao, $"Critérios de desempate: no máximo {RegrasDoDesempate.CriteriosDesempateMaximo}", RegrasDoDesempate.ConferirQuantidadeDeCriteriosDesempate(estado.CriteriosDesempate.Count).Count == 0),
            new ItemConformidade("desempate_area_enem_areas_mal_formadas", DimensaoConformidade.Classificacao, "Desempate por área do ENEM: cada critério tem ao menos uma área, sem exceder o teto, com códigos bem formados e sem repetição", Sem(desempate, DesempatePorAreaEnemErrorCodes.AreasObrigatorias, DesempatePorAreaEnemErrorCodes.AreasEmExcesso, DesempatePorAreaEnemErrorCodes.AreaInvalida, DesempatePorAreaEnemErrorCodes.AreaRepetida)),
            new ItemConformidade("desempate_area_enem_citada_por_dois_criterios", DimensaoConformidade.Classificacao, "Desempate por área do ENEM: cada área é citada por um critério só", Sem(desempate, DesempatePorAreaEnemErrorCodes.AreaCitadaPorOutroCriterio)),
            .. ItemDoDesempateSemQuadro(estado, desempate),
            new ItemConformidade("desempate_area_enem_fora_do_quadro", DimensaoConformidade.Classificacao, "Desempate por área do ENEM: as áreas citadas estão em todos os grupos do quadro de pesos por área", Sem(desempate, DesempatePorAreaEnemErrorCodes.ForaDoQuadro)),
        ];
    }

    /// <summary>
    /// Omitido quando a classificação calcula pelos pesos por área sem grupo no quadro: a falta
    /// do quadro já é o item da resolução, e aqui sairia verde com uma descrição que o estado
    /// desmente.
    /// </summary>
    internal static ItemConformidade[] ItemDoDesempateSemQuadro(EstadoDaConfiguracao estado, List<RegrasDoDesempate.ViolacaoDoDesempatePorArea> desempate) =>
        estado.Classificacao is { CalculaPelosPesosPorAreaDoEnem: true, TemGrupoNoQuadro: false }
            ? []
            : [new ItemConformidade("desempate_area_enem_sem_quadro", DimensaoConformidade.Classificacao, "Desempate por área do ENEM: a classificação é baseada em ENEM, calculada pela média ponderada, com quadro de pesos por área", Sem(desempate, DesempatePorAreaEnemErrorCodes.SemQuadro))];

    /// <summary>Sem violação dos códigos dados — os itens do checklist separam o que corrigir.</summary>
    internal static bool Sem(List<RegrasDoDesempate.ViolacaoDoDesempatePorArea> violacoes, params string[] codigosDoErro) =>
        !violacoes.Any(v => codigosDoErro.Contains(v.Erro.Code));
}
