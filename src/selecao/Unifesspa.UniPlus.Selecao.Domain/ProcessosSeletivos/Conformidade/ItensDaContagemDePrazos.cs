namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel sobre identificação e contagem dos prazos: os gates que antecedem o agregador genérico.</summary>
internal static class ItensDaContagemDePrazos
{
    // Têm erro nomeado próprio e por isso não entram nos itens estruturais: lá dentro, a conformidade
    // insuficiente interceptaria a causa específica. O fuso não é gate de publicação, mas projetá-lo é o
    // que evita o preflight declarar tudo pronto e a publicação devolver um 500 sem explicação.
    internal static IReadOnlyList<ItemConformidade> Avaliar(ConfiguracaoDeclarada configuracao, ContextoDeContagemDePrazos contexto) =>
    [
        new ItemConformidade("identificador_legivel_nao_declarado", DimensaoConformidade.Identificacao, "Identificador legível do certame", ContagemDePrazos.PendenciaDoIdentificadorLegivel(configuracao) is null && !ContagemDePrazos.IdentificadorLegivelRecusado(configuracao)),
        new ItemConformidade("localidade_nao_declarada", DimensaoConformidade.ContagemDePrazos, "Localidade que rege a contagem dos prazos", ContagemDePrazos.PendenciaDaLocalidade(configuracao) is null && !ContagemDePrazos.LocalidadeRecusada(configuracao)),
        new ItemConformidade("algoritmo_contagem_prazo_nao_declarado", DimensaoConformidade.ContagemDePrazos, "Convenção de contagem dos prazos de recurso", AlgoritmoJulgadoSemPendencia(configuracao)),
        new ItemConformidade("calendario_vigente_ausente", DimensaoConformidade.ContagemDePrazos, "Calendário de dias úteis vigente", CalendarioJulgadoSemPendencia(configuracao, contexto)),
        new ItemConformidade("fuso_institucional_nao_reconhecido", DimensaoConformidade.ContagemDePrazos, "Fuso institucional reconhecido pelo runtime", contexto.FusoInstitucionalReconhecido),
    ];

    // A convenção recusada pela leitura, ou que a leitura deixa sem julgar por não saber se a contagem distingue dia útil, não aprova o item.
    private static bool AlgoritmoJulgadoSemPendencia(ConfiguracaoDeclarada configuracao) =>
        ContagemDePrazos.PendenciaDoAlgoritmoDeContagem(configuracao) is null
        && !ContagemDePrazos.AlgoritmoRecusado(configuracao)
        && !(configuracao.Cronograma.AlgoritmoContagemPrazo is null && ContagemDePrazos.ContagemIndeterminadaPelaLeitura(configuracao));

    private static bool CalendarioJulgadoSemPendencia(ConfiguracaoDeclarada configuracao, ContextoDeContagemDePrazos contexto) =>
        ContagemDePrazos.PendenciaDoCalendarioVigente(configuracao, contexto) is null
        && !(contexto.CalendarioVigente is null && ContagemDePrazos.ContagemIndeterminadaPelaLeitura(configuracao));
}
