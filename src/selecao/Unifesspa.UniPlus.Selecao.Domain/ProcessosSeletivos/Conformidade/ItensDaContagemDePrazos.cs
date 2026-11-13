namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel sobre identificação e contagem dos prazos: os gates que antecedem o agregador genérico.</summary>
internal static class ItensDaContagemDePrazos
{
    // Têm erro nomeado próprio e por isso não entram nos itens estruturais: lá dentro, a conformidade
    // insuficiente interceptaria a causa específica. O fuso não é gate de publicação, mas projetá-lo é o
    // que evita o preflight declarar tudo pronto e a publicação devolver um 500 sem explicação.
    internal static IReadOnlyList<ItemConformidade> Avaliar(EstadoDaConfiguracao estado, ContextoDeContagemDePrazos contexto) =>
    [
        new ItemConformidade("identificador_legivel_nao_declarado", DimensaoConformidade.Identificacao, "Identificador legível do certame", ContagemDePrazos.PendenciaDoIdentificadorLegivel(estado) is null),
        new ItemConformidade("localidade_nao_declarada", DimensaoConformidade.ContagemDePrazos, "Localidade que rege a contagem dos prazos", ContagemDePrazos.PendenciaDaLocalidade(estado) is null),
        new ItemConformidade("algoritmo_contagem_prazo_nao_declarado", DimensaoConformidade.ContagemDePrazos, "Convenção de contagem dos prazos de recurso", ContagemDePrazos.PendenciaDoAlgoritmoDeContagem(estado) is null),
        new ItemConformidade("calendario_vigente_ausente", DimensaoConformidade.ContagemDePrazos, "Calendário de dias úteis vigente", ContagemDePrazos.PendenciaDoCalendarioVigente(estado, contexto) is null),
        new ItemConformidade("fuso_institucional_nao_reconhecido", DimensaoConformidade.ContagemDePrazos, "Fuso institucional reconhecido pelo runtime", contexto.FusoInstitucionalReconhecido),
    ];
}
