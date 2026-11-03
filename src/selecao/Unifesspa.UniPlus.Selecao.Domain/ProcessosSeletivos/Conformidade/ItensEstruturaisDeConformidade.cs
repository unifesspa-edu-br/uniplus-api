namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Opcoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ItensEstruturaisDeConformidade
{
    /// <summary>A declaração do bônus existe e concorda com a configuração.</summary>
    internal static bool BonusRegionalDeclarado(EstadoDaConfiguracao estado) =>
        estado.AplicaBonusRegional is { } aplica && aplica == (estado.BonusRegional is not null);

    /// <summary>
    /// Os itens do PRIMEIRO gate estrutural — <see cref="PendenciaDeConformidade"/> (Story #758
    /// CA-07) — as dimensões estruturalmente OBRIGATÓRIAS do agregado: Oferta de atendimento
    /// especializado (1), Distribuição de vagas (1..*), Classificação (1) e Cronograma de fases
    /// (1..*, Story #851), mais duas declarações obrigatórias: o bônus regional, que o processo
    /// declara aplicar ou não (a ausência da declaração é pendência, e a ausência da configuração só
    /// significa "sem bônus" quando a declaração é falsa), e os critérios de desempate, obrigatórios
    /// quando a inscrição é feita no sistema (com resultado importado a lista já vem classificada).
    /// </summary>
    /// <remarks>
    /// <b>"Etapas" deixou de ser item incondicional (Story #851 §3.5).</b> Um processo
    /// sem prova (SiSU, <c>CLASSIFICACAO-IMPORTADA</c>) publica sem etapa — a fase que
    /// agrupa etapas existe se e somente se há etapa, e essa bicondicional é gate à
    /// parte (<see cref="PendenciaDoCronograma"/>), projetada em <see cref="AvaliarConformidade"/>
    /// por predicados próprios, não aqui. O que sobrevive aqui é a exigência de
    /// <see cref="RegraCalculoCodigo.FormulaMediaPonderada"/>: sob essa fórmula, o divisor da
    /// média (<see cref="CalcularDivisorMedia"/>) tem de ser maior que zero. Sob
    /// <see cref="RegraCalculoCodigo.ClassificacaoImportada"/>, a classificação dispensa etapa,
    /// fórmula e precisão locais — nenhum item aqui.
    /// <para>
    /// <b>Média pelos pesos por área do ENEM.</b> Quando a classificação calcula a média assim,
    /// entram também a resolução com o quadro congelado e o grupo de área de cada oferta
    /// (<see cref="ItensDaMediaPelosPesosPorAreaDoEnem"/>). Como o divisor da média, esses
    /// itens existem só quando a exigência existe.
    /// </para>
    /// <para>
    /// Fonte que <see cref="PendenciaDeConformidade"/> agrega no <c>DomainError</c> genérico
    /// (Story #575, achado de revisão de plano): a cascata de remanejamento tem erro NOMEADO
    /// próprio (<see cref="PendenciaDaCascata"/>) e não pode entrar nesta lista, senão o
    /// agregador genérico intercepta o erro específico antes que ele seja alcançado.
    /// </para>
    /// </remarks>
    internal static List<ItemConformidade> Avaliar(EstadoDaConfiguracao estado)
    {
        List<ItemConformidade> itens =
        [
            new ItemConformidade("atendimento_especializado_ausente", DimensaoConformidade.AtendimentoEspecializado, "Atendimento especializado", estado.OfertaAtendimento is not null),
            new ItemConformidade("distribuicao_vagas_ausente", DimensaoConformidade.DistribuicaoVagas, "Distribuição de vagas", estado.DistribuicaoVagas.Count > 0),
            // O candidato escolhe a oferta pelo nome congelado; a distribuição definida antes de a
            // oferta ser nomeada precisa ser redefinida.
            ItemDasOfertas(
                "distribuicao_vagas_oferta_sem_nome",
                DimensaoConformidade.DistribuicaoVagas,
                "Nome de cada oferta congelado na distribuição de vagas",
                [.. estado.DistribuicaoVagas.Where(static oferta => oferta.OfertaCurso is null)],
                static oferta => $"{oferta.OfertaCursoOrigemId}"),
            // O número de opções de curso é declaração obrigatória (a ausência nunca vale "1 opção") e
            // depende da distribuição: com mais opções que cursos o candidato não teria a 2ª para escolher.
            new ItemConformidade("opcoes_de_curso_nao_declaradas", DimensaoConformidade.DistribuicaoVagas, "Número de opções de curso", estado.NumeroDeOpcoesDeCurso is not null),
            new ItemConformidade("opcoes_de_curso_acima_das_ofertas", DimensaoConformidade.DistribuicaoVagas, "Opções de curso: a distribuição de vagas tem ao menos tantos cursos quanto o número de opções", !RegrasDaDistribuicaoDeVagas.OpcoesDeCursoAcimaDasOfertas(estado.NumeroDeOpcoesDeCurso, estado.DistribuicaoVagas.Count)),
            new ItemConformidade("classificacao_ausente", DimensaoConformidade.Classificacao, "Classificação", estado.Classificacao is not null),
            new ItemConformidade("cronograma_fases_ausente", DimensaoConformidade.Cronograma, "Cronograma de fases", estado.CronogramaFases.Count > 0),
            // Issue #1112: publicar sem declarar cobrança de taxa é recusado — a ausência nunca
            // é interpretada como "não cobra" (CA-01). Diferente de ConfiguracaoDivulgacao, aqui
            // ausência não é estado publicável.
            new ItemConformidade("taxa_inscricao_nao_declarada", DimensaoConformidade.TaxaInscricao, "Taxa de inscrição e isenção", estado.ConfiguracaoTaxaInscricao is not null),
            // Issue #1310: processo que cobra reconhece ao menos um fundamento de isenção — a
            // possibilidade de pedir isenção se materializa nos fundamentos declarados. A fábrica
            // já recusa a combinação, mas ela é MATERIALIZÁVEL: o EF hidrata a linha direto da
            // coluna, sem passar por Criar, então configuração gravada antes da regra volta ao
            // agregado nesse estado. Sem este item, publicaria.
            // Verdadeiro (sem pendência) quando a configuração é nula: a ausência já é o item
            // acima, e dois vermelhos pela mesma causa fariam o operador procurar dois problemas.
            new ItemConformidade("taxa_inscricao_sem_fundamento_de_isencao", DimensaoConformidade.TaxaInscricao, "Fundamentos de isenção do processo que cobra taxa", TemFundamentoDeIsencaoQuandoCobra(estado)),
            // Story #554, PR #898 (issue #549, ADR-0074): toda exigência que determina
            // resultado precisa de ≥1 base legal RESOLVIDO — semântica vazia quando não há
            // exigência que determine resultado (Services.ValidadorBaseLegalExigencias).
            // Story #920: estendido a grupo OU/N-de com consequência própria (exigência de
            // 1ª classe, base legal NÃO derivada dos filhos).
            new ItemConformidade(
                "exigencias_base_legal_nao_resolvida",
                DimensaoConformidade.ExigenciasDocumentais,
                "Base legal das exigências documentais",
                Services.ValidadorBaseLegalExigencias.TodasResolvidas(estado.DocumentosExigidos)
                    && GruposComConsequenciaTemBaseLegalResolvida(estado)),
            // O processo declara se aplica o bônus regional; a ausência da configuração não
            // significa mais "sem bônus". Declaração e configuração também precisam concordar:
            // o EF hidrata a linha sem passar por DefinirBonusRegional.
            new ItemConformidade("bonus_regional_nao_declarado", DimensaoConformidade.BonusRegional, "Bônus regional: aplica ou não aplica", BonusRegionalDeclarado(estado)),
            new ItemConformidade("bonus_regional_fato_de_elegibilidade", DimensaoConformidade.BonusRegional, "Bônus regional: com inscrição própria, cita o fato booleano que decide quem recebe o acréscimo, conhecido na inscrição", PendenciasDosFatos.PendenciaDoFatoDeElegibilidade(estado) is null),
            // Com inscrição própria o desempate é obrigatório; com resultado importado a lista
            // já vem classificada e o desempate não se aplica.
            new ItemConformidade("criterios_desempate_ausentes", DimensaoConformidade.Desempate, "Critérios de desempate (inscrição própria)", estado.OrigemCandidatos != OrigemCandidatos.InscricaoPropria || estado.CriteriosDesempate.Count > 0),
        ];

        itens.AddRange(ItensDaEscalaDasEtapas(estado));
        itens.AddRange(ItensDaAvaliacaoDasEtapas(estado));

        if (estado.Classificacao is { RegraCalculo.Codigo: RegraCalculoCodigo.FormulaMediaPonderada })
        {
            itens.Add(new ItemConformidade("classificacao_divisor_media_invalido", DimensaoConformidade.Classificacao, "Divisor da média (fórmula local)", estado.CalcularDivisorMedia() > 0));
            itens.Add(new ItemConformidade("classificacao_escala_nota_final_nao_declarada", DimensaoConformidade.Classificacao, "Escala da nota final (fórmula local)", estado.Classificacao.EscalaNotaFinal is not null));
        }

        if (estado.Classificacao is { CalculaPelosPesosPorAreaDoEnem: true } classificacaoEnem)
        {
            itens.AddRange(ItensDaMediaPelosPesosPorAreaDoEnem(estado, classificacaoEnem));
        }

        if (estado.OrigemCandidatos == OrigemCandidatos.InscricaoPropria && estado.Classificacao is { BaseadoEmEnem: true })
        {
            // O candidato escolhe na inscrição, entre as edições admitidas, a do ENEM cujas notas a
            // classificação usa (issue #1620). Com o resultado importado não há inscrição: a edição
            // vem com a importação, e a declaração não teria quem a lesse.
            itens.Add(new ItemConformidade(
                "classificacao_edicao_enem_nao_declarada",
                DimensaoConformidade.Classificacao,
                "Edições do ENEM admitidas (classificação baseada em ENEM com inscrição própria)",
                estado.OpcoesDeclaradas.Any(static o => string.Equals(o.FatoCodigo, EdicaoDoEnem.Fato, StringComparison.Ordinal))));
        }

        return itens;
    }

    /// <summary>
    /// A escala em que a nota de cada etapa é lançada: a etapa com nota a declara, a que não tem
    /// nota e a do ENEM não. Depende do grafo (o que compõe a média, o que a classificação e o
    /// desempate citam), por isso é gate de publicação e não recusa de gravação.
    /// </summary>
    /// <remarks>
    /// Cada item lista todas as etapas que reprovam, e os itens são independentes: uma tentativa
    /// de publicar devolve de uma vez tudo o que falta. A etapa de nota do ENEM que declara escala
    /// aparece só no item do ENEM, para a mesma causa não render dois vermelhos.
    /// </remarks>
    internal static List<ItemConformidade> ItensDaEscalaDasEtapas(EstadoDaConfiguracao estado)
    {
        static bool DeclaraEscala(EtapaProcesso etapa) => etapa.NotaMaxima.HasValue || etapa.PassoDoLancamento.HasValue;

        // Uma avaliação por etapa: o predicado percorre a classificação e o desempate, e três
        // dos quatro itens o consultam.
        List<(EtapaProcesso Etapa, bool TemNota)> avaliadas = [.. estado.Etapas.Select(etapa => (etapa, EtapaTemNota(estado, etapa)))];

        return
        [
            ItemDasEtapas(
                "etapa_com_nota_sem_nota_maxima",
                "Nota máxima de cada etapa com nota que não é do ENEM",
                [.. avaliadas.Where(a => !a.Etapa.DeclaraNotaDoEnem && a.TemNota && !a.Etapa.NotaMaxima.HasValue).Select(static a => a.Etapa)]),
            ItemDasEtapas(
                "etapa_sem_nota_com_escala",
                "Nota máxima e passo só em etapa com nota",
                [.. avaliadas.Where(a => !a.Etapa.DeclaraNotaDoEnem && !a.TemNota && DeclaraEscala(a.Etapa)).Select(static a => a.Etapa)]),
            ItemDasEtapas(
                "etapa_do_enem_com_escala",
                "Etapa de nota do ENEM sem nota máxima nem passo",
                [.. estado.Etapas.Where(etapa => etapa.DeclaraNotaDoEnem && DeclaraEscala(etapa))]),
            ItemDasEtapas(
                "nota_minima_da_regra_acima_da_nota_maxima",
                "Nota mínima da regra de eliminação até a nota máxima da etapa",
                [.. estado.Etapas.Where(etapa => NotaMinimaDaRegraPassaDaNotaMaxima(estado, etapa))]),
        ];
    }

    /// <summary>
    /// Como a nota de cada etapa é obtida: a etapa com nota declara a forma de avaliação, a que não
    /// tem nota não declara, e a forma humana exige a regra que consolida as notas dos avaliadores,
    /// o passo do lançamento (salvo quando a banca escolhe o nível de uma escala de atendimento) e
    /// ao menos uma banca. Os itens de avaliação só existem na etapa com nota e somam a nota máxima
    /// dela. Depende do grafo (o que compõe a média, o que a
    /// classificação e o desempate citam), por isso é gate de publicação e não recusa de gravação.
    /// </summary>
    /// <remarks>
    /// A etapa de nota do ENEM fica de fora: a recusa de forma e regra nela é da gravação, que
    /// conhece o tipo congelado. Os itens são independentes e cada um lista todas as etapas que
    /// reprovam.
    /// </remarks>
    internal static List<ItemConformidade> ItensDaAvaliacaoDasEtapas(EstadoDaConfiguracao estado)
    {
        // Uma avaliação de EtapaTemNota por etapa, que percorre a classificação e o desempate. A
        // etapa humana sem nota só reprova o item da forma: sem nota não há o que consolidar, lançar
        // ou avaliar, e listá-la nos itens da forma humana mandaria o operador a outra pendência.
        List<(EtapaProcesso Etapa, bool TemNota)> avaliadas =
            [.. estado.Etapas.Where(static etapa => !etapa.DeclaraNotaDoEnem).Select(etapa => (etapa, EtapaTemNota(estado, etapa)))];
        List<EtapaProcesso> comNota = [.. avaliadas.Where(static a => a.TemNota).Select(static a => a.Etapa)];
        List<EtapaProcesso> semNota = [.. avaliadas.Where(static a => !a.TemNota).Select(static a => a.Etapa)];
        List<EtapaProcesso> humanas = [.. comNota.Where(static etapa => etapa.FormaDeAvaliacao == FormaDeAvaliacao.Humana)];

        return
        [
            ItemDasEtapas(
                "etapa_com_nota_sem_forma_de_avaliacao",
                "Forma de avaliação de cada etapa com nota que não é do ENEM",
                [.. comNota.Where(static etapa => etapa.FormaDeAvaliacao is null)]),
            ItemDasEtapas(
                "etapa_sem_nota_com_forma_de_avaliacao",
                "Forma de avaliação e regra de consolidação só em etapa com nota",
                [.. semNota.Where(static etapa => etapa.FormaDeAvaliacao is not null || etapa.RegraConsolidacao is not null)]),
            ItemDasEtapas(
                "etapa_sem_nota_com_itens_de_avaliacao",
                "Itens de avaliação e escala de atendimento só em etapa com nota",
                [.. semNota.Where(static etapa => etapa.ItensDeAvaliacao.Count > 0 || etapa.EscalaDeAtendimento is not null)]),
            ItemDasEtapas(
                "etapa_com_itens_fora_da_nota_maxima",
                "Soma das pontuações dos itens igual à nota máxima de cada etapa com itens",
                [.. comNota.Where(static etapa => etapa.ItensDeAvaliacao.Count > 0
                    && etapa.NotaMaxima is { } notaMaxima
                    && etapa.ItensDeAvaliacao.Sum(static item => item.PontuacaoMaxima) != notaMaxima)]),
            ItemDasEtapas(
                "etapa_humana_sem_regra_de_consolidacao",
                "Regra de consolidação de cada etapa de avaliação humana",
                [.. humanas.Where(static etapa => etapa.RegraConsolidacao is null)]),
            ItemDasEtapas(
                "etapa_humana_sem_passo_do_lancamento",
                "Passo do lançamento de cada etapa de avaliação humana sem escala de atendimento",
                [.. humanas.Where(static etapa => etapa.PassoDoLancamento is null && etapa.EscalaDeAtendimento is null)]),
            ItemDasEtapas(
                "etapa_humana_sem_banca",
                "Ao menos uma banca em cada etapa de avaliação humana",
                [.. humanas.Where(static etapa => etapa.Bancas.Count == 0)]),
        ];
    }

    /// <summary>
    /// A etapa tem nota quando entra na média, tem nota mínima ou é citada pela eliminação por
    /// nota mínima da etapa ou pelo desempate pela maior nota da etapa. Quem enxerga as quatro
    /// coisas é a raiz: a etapa sozinha não vê a classificação nem o desempate. A eliminação
    /// por ausência não conta: vale para etapa com ou sem nota.
    /// </summary>
    internal static bool EtapaTemNota(EstadoDaConfiguracao estado, EtapaProcesso etapa) =>
        etapa.ComponeNota
        || etapa.NotaMinima.HasValue
        || RegrasDasEtapas.EtapaReferenciadaPorDesempate(estado, etapa.Id)
        || RegrasDasEtapas.EliminacoesPorNotaMinimaDaEtapa(estado, etapa.Id).Any();

    /// <summary>
    /// Só confere quando a etapa declara a nota máxima: a falta dela já é o item próprio, e dois
    /// vermelhos pela mesma causa fariam o operador procurar dois problemas.
    /// </summary>
    internal static bool NotaMinimaDaRegraPassaDaNotaMaxima(EstadoDaConfiguracao estado, EtapaProcesso etapa) =>
        etapa.NotaMaxima is { } notaMaxima
        && RegrasDasEtapas.EliminacoesPorNotaMinimaDaEtapa(estado, etapa.Id).Any(args => args.NotaMinima > notaMaxima);

    /// <summary>
    /// Item que identifica as etapas pendentes, em ordem estável, pelo mesmo motivo de
    /// <see cref="ItemDasOfertas"/>. O nome é texto livre do operador e sai ecoado.
    /// </summary>
    internal static ItemConformidade ItemDasEtapas(string codigo, string mensagem, List<EtapaProcesso> pendentes) =>
        new(
            codigo,
            DimensaoConformidade.Etapas,
            pendentes.Count == 0
                ? mensagem
                : $"{mensagem} (etapas: {string.Join("; ", pendentes
                    .OrderBy(static etapa => etapa.Ordem ?? int.MaxValue)
                    .ThenBy(static etapa => etapa.Nome, StringComparer.Ordinal)
                    .ThenBy(static etapa => etapa.Id)
                    .Select(static etapa => $"\"{CaracteresInvisiveis.ParaEco(etapa.Nome, TextoCongelado.TamanhoMaximoEcoado)}\""))})",
            pendentes.Count == 0);

    /// <summary>
    /// A resolução de Pesos por Área, o quadro congelado dela e o grupo de área de cada oferta,
    /// que escolhe a linha de pesos da média.
    /// </summary>
    internal static List<ItemConformidade> ItensDaMediaPelosPesosPorAreaDoEnem(EstadoDaConfiguracao estado, ConfiguracaoClassificacao classificacao) =>
    [
        new ItemConformidade(
            "classificacao_resolucao_peso_area_enem_ausente",
            DimensaoConformidade.Classificacao,
            "Resolução de Pesos por Área com quadro congelado (classificação baseada em ENEM com cálculo local)",
            classificacao.ResolucaoComQuadroDeclarada),
        ItemDasOfertas(
            "distribuicao_vagas_oferta_sem_grupo_area_enem",
            DimensaoConformidade.DistribuicaoVagas,
            "Grupo de área do ENEM congelado em toda oferta",
            [.. estado.DistribuicaoVagas.Where(static oferta => oferta.GrupoAreaEnem is null)],
            static oferta => $"{oferta.OfertaCursoOrigemId}"),
        ItemDasOfertas(
            "classificacao_grupo_area_enem_da_oferta_fora_do_quadro",
            DimensaoConformidade.Classificacao,
            "Grupo de área do ENEM de cada oferta presente no quadro de pesos por área",
            OfertasComGrupoAreaEnemForaDoQuadro(estado, classificacao),
            static oferta => $"{oferta.OfertaCursoOrigemId} (grupo {DescreverGrupoAreaEnem(oferta.GrupoAreaEnem!)})"),
    ];

    /// <summary>
    /// Item que identifica as ofertas pendentes, em ordem estável: sem elas, quem publica não
    /// sabe qual corrigir. O código do item continua sendo a identidade; a lista é só redação.
    /// </summary>
    /// <remarks>
    /// As ofertas ficam entre parênteses, separadas por ponto e vírgula e sem ponto final: o
    /// agregador de <see cref="PendenciaDeConformidade"/> separa os itens por vírgula e pontua o
    /// fim.
    /// </remarks>
    internal static ItemConformidade ItemDasOfertas(
        string codigo,
        string dimensao,
        string mensagem,
        List<ConfiguracaoDistribuicaoVagas> pendentes,
        Func<ConfiguracaoDistribuicaoVagas, string> descrever) =>
        new(
            codigo,
            dimensao,
            pendentes.Count == 0
                ? mensagem
                : $"{mensagem} (ofertas: {string.Join("; ", pendentes.OrderBy(static oferta => oferta.OfertaCursoOrigemId).Select(descrever))})",
            pendentes.Count == 0);

    internal static string DescreverGrupoAreaEnem(GrupoAreaEnemSnapshot grupo) =>
        $"{CaracteresInvisiveis.ParaEco(grupo.Codigo, TextoCongelado.TamanhoMaximoEcoado)} — {CaracteresInvisiveis.ParaEco(grupo.Rotulo, TextoCongelado.TamanhoMaximoEcoado)}";

    /// <summary>
    /// Vazio quando não há quadro: a falta dele já é o item da resolução, e dois vermelhos pela
    /// mesma causa fariam o operador procurar dois problemas.
    /// </summary>
    internal static List<ConfiguracaoDistribuicaoVagas> OfertasComGrupoAreaEnemForaDoQuadro(EstadoDaConfiguracao estado, ConfiguracaoClassificacao classificacao)
    {
        if (!classificacao.TemGrupoNoQuadro)
        {
            return [];
        }

        HashSet<string> gruposDoQuadro = [.. classificacao.QuadroPesoAreaEnem.Select(static grupo => grupo.GrupoAreaEnem.Codigo)];
        return [.. estado.DistribuicaoVagas.Where(oferta => oferta.GrupoAreaEnem is { } grupo && !gruposDoQuadro.Contains(grupo.Codigo))];
    }

    /// <summary>
    /// Processo que cobra taxa reconhece ao menos um fundamento de isenção (issue #1310).
    /// Verdadeiro quando não há configuração declarada — a ausência é pendência do item anterior —
    /// e quando o processo declara não cobrar, caso em que fundamento nenhum é o estado correto
    /// (UNI-REQ-0100).
    /// </summary>
    /// <remarks>
    /// Contar elementos basta: token fora do vocabulário não chega até aqui como lista "cheia de
    /// nada". O conversor de <c>ConfiguracaoTaxaInscricaoConfiguration</c> LANÇA ao reserializar
    /// <see cref="FundamentoIsencao.Nenhum"/>, então uma coluna adulterada estoura na carga do
    /// agregado, alto e cedo, em vez de virar publicação silenciosa
    /// (<c>TaxaInscricaoSemFundamentoPersistenciaTests</c> prova as duas metades).
    /// </remarks>
    internal static bool TemFundamentoDeIsencaoQuandoCobra(EstadoDaConfiguracao estado) =>
        estado.ConfiguracaoTaxaInscricao is not { Cobra: true, Fundamentos.Count: 0 };

    /// <summary>Grupos <c>OU</c>/<c>N-de</c> com <see cref="NoExigencia.Consequencia"/> própria (Story #920) — cada um precisa de ≥1 <see cref="NoExigenciaBaseLegal"/> <see cref="StatusBaseLegal.Resolvido"/>, mesma semântica de <see cref="Services.ValidadorBaseLegalExigencias"/> para folha.</summary>
    internal static bool GruposComConsequenciaTemBaseLegalResolvida(EstadoDaConfiguracao estado) =>
        estado.NosExigencia
            .Where(static no => no.DeterminaResultado())
            .All(no => no.BasesLegais.Any(static b => b.Status == StatusBaseLegal.Resolvido));
}
