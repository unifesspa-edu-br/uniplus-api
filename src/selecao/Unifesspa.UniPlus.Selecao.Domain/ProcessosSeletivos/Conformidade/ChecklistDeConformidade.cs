namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class ChecklistDeConformidade
{
    /// <summary>
    /// Os avisos da conformidade: o que o administrador deve confirmar e que nunca impede a
    /// publicação. Lista própria, separada de <see cref="AvaliarConformidade"/>, porque aquela é bicondicional
    /// com os gates e um aviso não tem gate: nenhum aviso recusa uma publicação.
    /// </summary>
    public static IReadOnlyList<AvisoConformidade> AvaliarAvisos(EstadoDaConfiguracao estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        return [
            .. CicloRecursal.DefinitivosEmMaisDeUmLugar(TiposDeAtoDoCronograma.DeclaracoesDeAtos(estado))
                .Select(static repetido => new AvisoConformidade(
                    CodigosDeAvisoDeConformidade.CronogramaTipoDefinitivoEmMaisDeUmLugar,
                    DimensaoConformidade.Cronograma,
                    $"O tipo de ato '{repetido.AtoCodigo}' está declarado como resultado definitivo em mais de uma fase ou etapa ({JuntarLugares(repetido.Lugares)}). Confirme que o edital quer fechar mais de um ciclo com o mesmo tipo.")),
        ];
    }

    internal static string JuntarLugares(IReadOnlyList<string> lugares) =>
        lugares.Count == 1
            ? lugares[0]
            : $"{string.Join(", ", lugares.Take(lugares.Count - 1))} e {lugares[^1]}";

    /// <summary>
    /// Checklist de conformidade ESTRUTURAL do agregado, bicondicional com os gates de congelamento
    /// (<see cref="ProntidaoParaCongelar"/>): todos os itens ficam <see langword="true"/> se e somente se os
    /// gates não têm pendência, então não existe estado em que o checklist declare tudo <c>Ok</c> e a
    /// publicação recuse por razão estrutural.
    /// </summary>
    /// <remarks>
    /// "Estrutural" não é "publicável": com os gates verdes, a publicação ainda pode recusar por
    /// conformidade legal (<c>GET /conformidade-legal</c>), documento confirmado, tipo de ato e outras
    /// leituras que só o handler de publicação avalia (ADR-0109).
    /// <para>
    /// Não há segunda lista de predicados: cada item vem do predicado nomeado que o gate correspondente
    /// também chama. O gate escolhe a primeira falha na precedência fixada pela prontidão; o checklist
    /// projeta todos os vereditos, na ordem dos gates, e uma razão nova de um predicado compartilhado
    /// aparece nos dois ao mesmo tempo.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<ItemConformidade> AvaliarConformidade(
        EstadoDaConfiguracao estado,
        ContextoDeContagemDePrazos contexto, FatosDeModalidade fatosDeModalidade, TiposDeAtoUnicosPorObjeto tiposDeAtoUnicos,
        ProtecaoVigenteDasExigencias? protecao = null)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(tiposDeAtoUnicos);
        ArgumentNullException.ThrowIfNull(fatosDeModalidade);

        return
        [
            .. ItensDaContagemDePrazos.Avaliar(estado, contexto),
            .. ItensEstruturaisDeConformidade.Avaliar(estado),
            .. ItensDoCronograma.Avaliar(estado, contexto, tiposDeAtoUnicos),
            .. ItensDaCascata.Avaliar(estado),
            .. ItensDaPreCanonicalizacao.Avaliar(estado, fatosDeModalidade, protecao),
        ];
    }

    /// <summary>
    /// Pendência de conformidade do processo, ou <see langword="null"/> quando ele está conforme. É a fonte
    /// única do gate: publicar e retificar recusam com o mesmo <c>DomainError</c>.
    /// </summary>
    /// <remarks>
    /// As duas transições abrem uma <see cref="VersaoConfiguracao"/> append-only e juridicamente
    /// vinculante: uma versão congelada a partir de configuração incompleta é irreparável, porque o
    /// passado não se muta.
    /// </remarks>
    public static DomainError? PendenciaDeConformidade(EstadoDaConfiguracao estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        // Só os itens estruturais entram no agregador genérico: a cascata de remanejamento tem erro
        // nomeado próprio e nunca passa por aqui.
        IReadOnlyList<ItemConformidade> pendencias = [.. ItensEstruturaisDeConformidade.Avaliar(estado).Where(static item => !item.Ok)];
        if (pendencias.Count == 0)
        {
            return null;
        }

        return new DomainError(
            "ProcessoSeletivo.ConformidadeInsuficiente",
            $"Processo não conforme para publicação — pendente: {string.Join(", ", pendencias.Select(static p => p.Mensagem))}.");
    }
}
