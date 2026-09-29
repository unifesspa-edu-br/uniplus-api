namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Avalia um formulário contra as respostas de um candidato: resolve cada fato em ordem topológica,
/// com os fatos derivados intercalados assim que as suas dependências estão resolvidas, e diz, por
/// item, se ele aparece, se é obrigatório e quais restrições a resposta viola; por termo, se aparece e
/// se é obrigatório (UNI-REQ-0074, UNI-REQ-0145, ADR-0135).
/// </summary>
/// <remarks>
/// <para>
/// Função pura: a mesma entrada produz sempre a mesma saída. A mesma avaliação serve à
/// pré-visualização, com respostas simuladas, e à execução da inscrição.
/// </para>
/// <para>
/// O estado do fato de um item segue esta ordem:
/// </para>
/// <list type="number">
/// <item>etapa ou item oculto: <see cref="EstadoFato.NaoAplicavel"/>, mesmo que haja resposta gravada
/// — a resposta anterior deixa de valer sem precisar ser apagada;</item>
/// <item>exibição ainda indeterminada: <see cref="EstadoFato.Indeterminado"/>;</item>
/// <item>resposta que atende às restrições: <see cref="EstadoFato.Resolvido"/>;</item>
/// <item>resposta cuja validade depende de resposta anterior ainda desconhecida:
/// <see cref="EstadoFato.Indeterminado"/>;</item>
/// <item>sem resposta, ou com resposta que viola restrição e por isso não vale: pendente
/// (<see cref="EstadoFato.Indeterminado"/>) se o item é obrigatório ou se a etapa ainda não foi
/// concluída; <see cref="EstadoFato.NaoInformado"/> se o item é opcional e a etapa foi
/// concluída.</item>
/// </list>
/// <para>
/// Um nó que não entra na ordem, por pertencer a um ciclo ou depender de um, fica indeterminado: o
/// avaliador não decide sobre o que não consegue ordenar. A recusa do ciclo, com o caminho, é do
/// cadastro.
/// </para>
/// </remarks>
public static class AvaliadorFormulario
{
    /// <summary>Prioridade dos derivados na ordenação: entram assim que as dependências estão prontas.</summary>
    private const int PrioridadeDerivado = -1;

    public static AvaliacaoFormulario Avaliar(DefinicaoFormulario definicao, EntradaAvaliacaoFormulario entrada)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        ArgumentNullException.ThrowIfNull(entrada);

        List<(DefinicaoEtapa Etapa, DefinicaoItem Item)> itens =
            [.. definicao.Etapas.SelectMany(static etapa => etapa.Itens.Select(item => (etapa, item)))];
        Dictionary<string, (DefinicaoEtapa Etapa, DefinicaoItem Item)> itemPorFato =
            itens.ToDictionary(static par => par.Item.FatoCodigo, StringComparer.Ordinal);
        Dictionary<string, RegrasDerivacaoFato> derivacaoPorFato =
            definicao.Derivacoes.ToDictionary(static d => d.CodigoFato, StringComparer.Ordinal);

        List<NoDoGrafo> nos =
        [
            .. itens.Select(static (par, posicao) => new NoDoGrafo(
                par.Item.FatoCodigo,
                [.. (par.Etapa.Exibicao?.FatosCitados ?? []).Concat(par.Item.FatosCitados)],
                posicao)),
            .. definicao.Derivacoes.Select(static d => new NoDoGrafo(d.CodigoFato, d.DependenciasDeclaradas, PrioridadeDerivado)),
        ];
        OrdemTopologica ordem = GrafoDeFatos.Ordenar(nos);

        Dictionary<string, FatoResolvido> fatos = new(entrada.FatosConhecidos, StringComparer.Ordinal);
        Dictionary<string, AvaliacaoItem> avaliacaoPorFato = new(StringComparer.Ordinal);

        foreach (string codigo in ordem.Ordem)
        {
            if (derivacaoPorFato.TryGetValue(codigo, out RegrasDerivacaoFato? derivacao))
            {
                fatos[codigo] = Derivar(derivacao, fatos);
                continue;
            }

            (DefinicaoEtapa etapa, DefinicaoItem item) = itemPorFato[codigo];
            (FatoResolvido fato, AvaliacaoItem avaliacao) = AvaliarItem(etapa, item, entrada, fatos);
            fatos[codigo] = fato;
            avaliacaoPorFato[codigo] = avaliacao;
        }

        foreach (string codigo in ordem.ForaDeOrdem)
        {
            fatos[codigo] = FatoResolvido.Indeterminado();
            if (itemPorFato.TryGetValue(codigo, out (DefinicaoEtapa Etapa, DefinicaoItem Item) par))
            {
                avaliacaoPorFato[codigo] = new AvaliacaoItem(
                    codigo, par.Etapa.Codigo, Ternario.Indeterminado, Ternario.Indeterminado, []);
            }
        }

        List<AvaliacaoTermo> termos =
        [
            .. definicao.Termos.Select(termo =>
            {
                Ternario visivel = termo.Exibicao?.Avaliar(fatos) ?? Ternario.Verdadeiro;
                return new AvaliacaoTermo(termo.Codigo, visivel, ObrigatorioSeVisivel(visivel, termo.Obrigatoriedade, fatos));
            }),
        ];

        return new AvaliacaoFormulario(
            fatos,
            [.. itens.Select(par => avaliacaoPorFato[par.Item.FatoCodigo])],
            termos);
    }

    private static FatoResolvido Derivar(RegrasDerivacaoFato derivacao, IReadOnlyDictionary<string, FatoResolvido> fatos)
    {
        ResultadoDerivacao resultado = MotorDerivacao.Derivar(derivacao, fatos);
        return resultado.Estado == EstadoFato.Resolvido
            ? FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(resultado.Valores.Order(StringComparer.Ordinal)))
            : FatoResolvido.Indeterminado();
    }

    private static (FatoResolvido Fato, AvaliacaoItem Avaliacao) AvaliarItem(
        DefinicaoEtapa etapa,
        DefinicaoItem item,
        EntradaAvaliacaoFormulario entrada,
        IReadOnlyDictionary<string, FatoResolvido> fatos)
    {
        Ternario visivel = E(
            etapa.Exibicao?.Avaliar(fatos) ?? Ternario.Verdadeiro,
            item.Exibicao?.Avaliar(fatos) ?? Ternario.Verdadeiro);
        Ternario obrigatorio = ObrigatorioSeVisivel(visivel, item.Obrigatoriedade, fatos);

        AvaliacaoItem Avaliacao(IReadOnlyList<RestricaoValor> violadas) =>
            new(item.FatoCodigo, etapa.Codigo, visivel, obrigatorio, violadas);

        switch (visivel)
        {
            case Ternario.Falso:
                return (FatoResolvido.NaoAplicavel(), Avaliacao([]));
            case Ternario.Indeterminado:
                return (FatoResolvido.Indeterminado(), Avaliacao([]));
            case Ternario.Verdadeiro:
            default:
                break;
        }

        List<RestricaoValor> violadas = [];
        if (entrada.Respostas.TryGetValue(item.FatoCodigo, out JsonElement resposta) && !RespostaDeCampo.EstaVazia(resposta))
        {
            bool algumaIndeterminada = false;
            foreach (RestricaoValor restricao in item.Restricoes)
            {
                switch (restricao.Avaliar(resposta, fatos))
                {
                    case Ternario.Falso:
                        violadas.Add(restricao);
                        break;
                    case Ternario.Indeterminado:
                        algumaIndeterminada = true;
                        break;
                    case Ternario.Verdadeiro:
                    default:
                        break;
                }
            }

            if (violadas.Count == 0)
            {
                return (algumaIndeterminada ? FatoResolvido.Indeterminado() : FatoResolvido.Resolvido(resposta), Avaliacao([]));
            }
        }

        // Sem resposta que valha: o candidato ainda deve a resposta, salvo o opcional numa etapa já
        // concluída, que resolve como não informado e não trava as regras seguintes.
        FatoResolvido semResposta = obrigatorio == Ternario.Falso && entrada.EtapasConcluidas.Contains(etapa.Codigo)
            ? FatoResolvido.NaoInformado()
            : FatoResolvido.Indeterminado();
        return (semResposta, Avaliacao(violadas));
    }

    /// <summary>
    /// Obrigatório só o que aparece: o E ternário deixa uma obrigatoriedade já falsa decidir mesmo
    /// com a exibição ainda indeterminada — um campo que nunca é obrigatório não fica pendente.
    /// </summary>
    private static Ternario ObrigatorioSeVisivel(
        Ternario visivel, Obrigatoriedade obrigatoriedade, IReadOnlyDictionary<string, FatoResolvido> fatos) =>
        E(visivel, obrigatoriedade.Avaliar(fatos));

    private static Ternario E(Ternario a, Ternario b)
    {
        if (a == Ternario.Falso || b == Ternario.Falso)
        {
            return Ternario.Falso;
        }

        return a == Ternario.Indeterminado || b == Ternario.Indeterminado ? Ternario.Indeterminado : Ternario.Verdadeiro;
    }
}
