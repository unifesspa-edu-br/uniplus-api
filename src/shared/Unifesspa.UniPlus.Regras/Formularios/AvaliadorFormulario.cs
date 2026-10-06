namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Avalia um formulário contra as respostas de um candidato: resolve cada fato em ordem topológica,
/// com os fatos derivados e os agregados sobre grupos intercalados assim que as suas dependências
/// estão resolvidas, e diz, por
/// item, se ele aparece, se é obrigatório e quais restrições a resposta viola; por termo, se aparece e
/// se é obrigatório; e, por grupo repetível, o mesmo para cada ocorrência (UNI-REQ-0074,
/// UNI-REQ-0145, UNI-REQ-0146, ADR-0135).
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
        List<(DefinicaoEtapa Etapa, DefinicaoGrupo Grupo)> grupos =
            [.. definicao.Etapas.SelectMany(static etapa => etapa.Grupos.Select(grupo => (etapa, grupo)))];
        Dictionary<string, (DefinicaoEtapa Etapa, DefinicaoGrupo Grupo)> grupoPorCodigo =
            grupos.ToDictionary(static par => par.Grupo.Codigo, StringComparer.Ordinal);
        Dictionary<string, DefinicaoAgregado> agregadoPorFato =
            definicao.Agregados.ToDictionary(static a => a.Codigo, StringComparer.Ordinal);

        List<NoDoGrafo> nos =
        [
            .. itens.Select(static (par, posicao) => new NoDoGrafo(
                par.Item.FatoCodigo,
                [.. (par.Etapa.Exibicao?.FatosCitados ?? []).Concat(par.Item.FatosCitados)],
                posicao)),
            .. grupos.Select((par, posicao) => new NoDoGrafo(
                par.Grupo.Codigo,
                [.. (par.Etapa.Exibicao?.FatosCitados ?? []).Concat(par.Grupo.FatosDoCandidatoCitados)],
                itens.Count + posicao)),
            .. definicao.Derivacoes.Select(static d => new NoDoGrafo(d.CodigoFato, d.DependenciasDeclaradas, PrioridadeDerivado)),
            .. definicao.Agregados.Select(static a => new NoDoGrafo(a.Codigo, [a.GrupoCodigo], PrioridadeDerivado)),
        ];
        OrdemTopologica ordem = GrafoDeFatos.Ordenar(nos);

        Dictionary<string, FatoResolvido> fatos = new(entrada.FatosConhecidos, StringComparer.Ordinal);
        Dictionary<string, AvaliacaoItem> avaliacaoPorFato = new(StringComparer.Ordinal);
        Dictionary<string, AvaliacaoGrupo> avaliacaoPorGrupo = new(StringComparer.Ordinal);

        foreach (string codigo in ordem.Ordem)
        {
            if (derivacaoPorFato.TryGetValue(codigo, out RegrasDerivacaoFato? derivacao))
            {
                fatos[codigo] = Derivar(derivacao, fatos);
                continue;
            }

            if (agregadoPorFato.TryGetValue(codigo, out DefinicaoAgregado? agregado))
            {
                fatos[codigo] = AgregadoDeGrupo.Calcular(avaliacaoPorGrupo[agregado.GrupoCodigo], agregado.FatoDeMembro, agregado.Operacao);
                continue;
            }

            if (grupoPorCodigo.TryGetValue(codigo, out (DefinicaoEtapa Etapa, DefinicaoGrupo Grupo) doGrupo))
            {
                avaliacaoPorGrupo[codigo] = AvaliarGrupo(doGrupo.Etapa, doGrupo.Grupo, entrada, fatos);
                continue;
            }

            (DefinicaoEtapa etapa, DefinicaoItem item) = itemPorFato[codigo];
            (FatoResolvido fato, AvaliacaoItem avaliacao) = AvaliarItem(
                etapa.Codigo,
                etapa.Exibicao?.Avaliar(fatos) ?? Ternario.Verdadeiro,
                item,
                entrada.Respostas.TryGetValue(item.FatoCodigo, out JsonElement resposta) ? resposta : null,
                entrada.EtapasConcluidas.Contains(etapa.Codigo),
                fatos);
            fatos[codigo] = fato;
            avaliacaoPorFato[codigo] = avaliacao;
        }

        // O que cai fora da ordem, por ciclo, fica indeterminado: o avaliador não decide sobre o que
        // não consegue ordenar.
        foreach (string codigo in ordem.ForaDeOrdem)
        {
            if (grupoPorCodigo.TryGetValue(codigo, out (DefinicaoEtapa Etapa, DefinicaoGrupo Grupo) doGrupo))
            {
                avaliacaoPorGrupo[codigo] = new AvaliacaoGrupo(
                    codigo, doGrupo.Etapa.Codigo, Ternario.Indeterminado, Ternario.Indeterminado, EstadoFato.Indeterminado,
                    ContagemValida: true, OcorrenciaDoCandidatoValida: true, []);
                continue;
            }

            fatos[codigo] = FatoResolvido.Indeterminado();
            if (itemPorFato.TryGetValue(codigo, out (DefinicaoEtapa Etapa, DefinicaoItem Item) par))
            {
                avaliacaoPorFato[codigo] = new AvaliacaoItem(
                    codigo, par.Etapa.Codigo, Ternario.Indeterminado, Ternario.Indeterminado, [],
                    par.Item.Impedimento is null ? Ternario.Falso : Ternario.Indeterminado);
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
            termos,
            [.. grupos.Select(par => avaliacaoPorGrupo[par.Grupo.Codigo])],
            [.. definicao.Etapas.Select(etapa => new AvaliacaoEtapa(etapa.Codigo, etapa.Exibicao?.Avaliar(fatos) ?? Ternario.Verdadeiro))]);
    }

    private static FatoResolvido Derivar(RegrasDerivacaoFato derivacao, IReadOnlyDictionary<string, FatoResolvido> fatos)
    {
        ResultadoDerivacao resultado = MotorDerivacao.Derivar(derivacao, fatos);
        if (resultado.Estado != EstadoFato.Resolvido)
        {
            return FatoResolvido.Indeterminado();
        }

        return resultado.ValorBooleano is { } booleano
            ? FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(booleano))
            : FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(resultado.Valores.Order(StringComparer.Ordinal)));
    }

    /// <summary>
    /// O grupo no estado do candidato e, se aparece e foi respondido, cada ocorrência com os fatos do
    /// candidato e os subitens anteriores dela. A contagem fora do mínimo e do máximo, e a lista do
    /// grupo que inclui o candidato sem exatamente uma ocorrência dele, não valem como resposta, do
    /// mesmo modo que a resposta que viola restrição.
    /// </summary>
    private static AvaliacaoGrupo AvaliarGrupo(
        DefinicaoEtapa etapa, DefinicaoGrupo grupo, EntradaAvaliacaoFormulario entrada, IReadOnlyDictionary<string, FatoResolvido> fatos)
    {
        Ternario visivel = E(etapa.Exibicao?.Avaliar(fatos) ?? Ternario.Verdadeiro, grupo.Exibicao?.Avaliar(fatos) ?? Ternario.Verdadeiro);
        Ternario obrigatorio = ObrigatorioSeVisivel(visivel, grupo.Obrigatoriedade, fatos);

        AvaliacaoGrupo Avaliacao(EstadoFato estado, IReadOnlyList<AvaliacaoOcorrencia> ocorrencias, bool contagemValida = true, bool candidatoValido = true) =>
            new(grupo.Codigo, etapa.Codigo, visivel, obrigatorio, estado, contagemValida, candidatoValido, ocorrencias);

        switch (visivel)
        {
            case Ternario.Falso:
                return Avaliacao(EstadoFato.NaoAplicavel, []);
            case Ternario.Indeterminado:
                return Avaliacao(EstadoFato.Indeterminado, []);
            case Ternario.Verdadeiro:
            default:
                break;
        }

        bool etapaConcluida = entrada.EtapasConcluidas.Contains(etapa.Codigo);
        EstadoFato semResposta = obrigatorio == Ternario.Falso && etapaConcluida ? EstadoFato.NaoInformado : EstadoFato.Indeterminado;
        if (entrada.RespostasDosGrupos is null || !entrada.RespostasDosGrupos.TryGetValue(grupo.Codigo, out IReadOnlyList<OcorrenciaRespondida>? respondidas))
        {
            return Avaliacao(semResposta, []);
        }

        if (respondidas.GroupBy(static o => o.Id, StringComparer.Ordinal).FirstOrDefault(static g => g.Count() > 1) is { } repetida)
        {
            throw new ArgumentException(
                $"A identidade '{repetida.Key}' aparece em mais de uma ocorrência do grupo '{grupo.Codigo}'.", nameof(entrada));
        }

        AvaliacaoOcorrencia[] ocorrencias = [.. respondidas.Select(o => AvaliarOcorrencia(etapa.Codigo, grupo, o, etapaConcluida, fatos))];
        int quantas = ocorrencias.Length;
        bool contagemValida = (grupo.Maximo is not { } maximo || quantas <= maximo) && (quantas >= grupo.Minimo || (quantas == 0 && obrigatorio == Ternario.Falso));
        // A lista vazia, que só vale no grupo opcional, não tem ocorrência do candidato a conferir.
        bool candidatoValido = !grupo.IncluiCandidato || quantas == 0 || CandidatoComoMembro.TemUmaOcorrenciaDoCandidato(respondidas);
        if (!contagemValida || !candidatoValido)
        {
            return Avaliacao(semResposta, ocorrencias, contagemValida, candidatoValido);
        }

        // A lista vazia é resposta: no opcional, não informado; no obrigatório de mínimo zero, a
        // declaração de que não há ocorrência.
        EstadoFato estado = quantas == 0
            ? obrigatorio switch
            {
                Ternario.Falso => EstadoFato.NaoInformado,
                Ternario.Verdadeiro => EstadoFato.Resolvido,
                _ => EstadoFato.Indeterminado,
            }
            : ocorrencias.All(static o => o.Estado == EstadoFato.Resolvido) ? EstadoFato.Resolvido : EstadoFato.Indeterminado;
        return Avaliacao(estado, ocorrencias);
    }

    /// <summary>
    /// Os subitens de uma ocorrência, em ordem, cada um com os fatos do candidato e os subitens
    /// anteriores da mesma ocorrência; a ocorrência resolve quando nenhum subitem ficou pendente.
    /// </summary>
    private static AvaliacaoOcorrencia AvaliarOcorrencia(
        string etapaCodigo, DefinicaoGrupo grupo, OcorrenciaRespondida ocorrencia, bool etapaConcluida, IReadOnlyDictionary<string, FatoResolvido> fatos)
    {
        Dictionary<string, FatoResolvido> contexto = new(fatos, StringComparer.Ordinal);
        Dictionary<string, FatoResolvido> daOcorrencia = new(StringComparer.Ordinal);
        List<AvaliacaoItem> itens = [];
        foreach (DefinicaoItem subitem in grupo.Subitens)
        {
            (FatoResolvido fato, AvaliacaoItem avaliacao) = AvaliarItem(
                etapaCodigo,
                Ternario.Verdadeiro,
                subitem,
                ocorrencia.Respostas.TryGetValue(subitem.FatoCodigo, out JsonElement resposta) ? resposta : null,
                etapaConcluida,
                contexto);
            contexto[subitem.FatoCodigo] = fato;
            daOcorrencia[subitem.FatoCodigo] = fato;
            itens.Add(avaliacao);
        }

        EstadoFato estado = daOcorrencia.Values.Any(static f => f.Estado == EstadoFato.Indeterminado) ? EstadoFato.Indeterminado : EstadoFato.Resolvido;
        return new AvaliacaoOcorrencia(ocorrencia.Id, itens, daOcorrencia, estado);
    }

    /// <summary>
    /// Um campo — item da etapa ou subitem da ocorrência —, visível quando quem o contém aparece e a
    /// exibição dele é verdadeira.
    /// </summary>
    private static (FatoResolvido Fato, AvaliacaoItem Avaliacao) AvaliarItem(
        string etapaCodigo,
        Ternario visivelDoContentor,
        DefinicaoItem item,
        JsonElement? resposta,
        bool etapaConcluida,
        IReadOnlyDictionary<string, FatoResolvido> fatos)
    {
        Ternario visivel = E(visivelDoContentor, item.Exibicao?.Avaliar(fatos) ?? Ternario.Verdadeiro);
        Ternario obrigatorio = ObrigatorioSeVisivel(visivel, item.Obrigatoriedade, fatos);

        // As opções do campo que aparece ou pode aparecer: a interseção das restrições que limitam a escolha.
        OpcoesVigentes? opcoes = visivel == Ternario.Falso
            ? null
            : item.RestricoesDaResposta.Select(r => r.Opcoes(fatos)).OfType<OpcoesVigentes>().Aggregate((OpcoesVigentes?)null, static (juntas, uma) => juntas?.E(uma) ?? uma);

        // O impedimento se avalia com a resposta do próprio campo já resolvida: o campo oculto ou sem
        // resposta não impede, porque nenhuma condição se cumpre sobre ele (UNI-REQ-0074).
        (FatoResolvido Fato, AvaliacaoItem Avaliacao) Resultado(FatoResolvido fato, IReadOnlyList<RestricaoValor> violadas) =>
            (fato, new(item.FatoCodigo, etapaCodigo, visivel, obrigatorio, violadas,
                item.Impedimento?.Avaliar(new Dictionary<string, FatoResolvido>(fatos, StringComparer.Ordinal) { [item.FatoCodigo] = fato })
                    ?? Ternario.Falso,
                opcoes));

        switch (visivel)
        {
            case Ternario.Falso:
                return Resultado(FatoResolvido.NaoAplicavel(), []);
            case Ternario.Indeterminado:
                return Resultado(FatoResolvido.Indeterminado(), []);
            case Ternario.Verdadeiro:
            default:
                break;
        }

        List<RestricaoValor> violadas = [];
        if (resposta is { } respondida && !RespostaDeCampo.EstaVazia(respondida))
        {
            bool algumaIndeterminada = false;
            foreach (RestricaoValor restricao in item.RestricoesDaResposta)
            {
                switch (restricao.Avaliar(respondida, fatos))
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
                return Resultado(algumaIndeterminada ? FatoResolvido.Indeterminado() : FatoResolvido.Resolvido(respondida), []);
            }
        }

        // Sem resposta que valha: o candidato ainda deve a resposta, salvo o opcional numa etapa já
        // concluída, que resolve como não informado e não trava as regras seguintes.
        FatoResolvido semResposta = obrigatorio == Ternario.Falso && etapaConcluida
            ? FatoResolvido.NaoInformado()
            : FatoResolvido.Indeterminado();
        return Resultado(semResposta, violadas);
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
