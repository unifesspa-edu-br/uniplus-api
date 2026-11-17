namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
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
    /// As dimensões estruturalmente obrigatórias do agregado, que <see cref="ChecklistDeConformidade.CausasDeConformidade"/>
    /// projeta numa causa por item pendente: atendimento especializado, distribuição de vagas, classificação, taxa, base
    /// legal das exigências e as declarações do bônus regional e dos critérios de desempate (obrigatórios só com
    /// inscrição própria; com resultado importado a lista já vem classificada).
    /// </summary>
    /// <remarks>
    /// Etapa não é item incondicional: o processo sem prova (SiSU, classificação importada) publica sem
    /// etapa, e a coincidência entre a fase que agrupa etapas e a etapa pontuada é regra de completude do cronograma
    /// (<see cref="CronogramaDoProcesso"/>). Sob a média ponderada, o divisor
    /// (<see cref="EstadoDaConfiguracao.CalcularDivisorMedia"/>) tem de ser maior que zero; sob a classificação
    /// importada, etapa, fórmula e precisão locais são dispensadas. A cascata de remanejamento tem erro
    /// nomeado próprio (<see cref="CoberturaDaCascata.CausasDaCascata"/>) e fica fora desta lista: a conformidade
    /// insuficiente interceptaria o erro específico.
    /// </remarks>
    internal static List<ItemConformidade> Avaliar(EstadoDaConfiguracao estado) =>
    [
        .. ItensDoAtendimentoEDasVagas(estado),
        .. ItensDaClassificacao(estado),
        .. ItensDaTaxaDeInscricao(estado),
        .. ItensDasExigenciasDocumentais(estado),
        .. ItensDoBonusRegional(estado),
        .. ItensDoDesempate(estado),
        .. ItensDasEtapas(estado),
        .. ItensDaFormulaDaClassificacao(estado),
        .. ItensDaClassificacaoBaseadaEmEnem(estado),
    ];

    // O candidato escolhe a oferta pelo nome congelado, então a distribuição definida antes de a oferta ser nomeada
    // precisa ser redefinida. O número de opções de curso é declaração obrigatória (a ausência nunca vale "1 opção") e
    // depende da distribuição: com mais opções que cursos o candidato não teria a 2ª para escolher.
    private static List<ItemConformidade> ItensDoAtendimentoEDasVagas(EstadoDaConfiguracao estado) =>
    [
        new ItemConformidade("atendimento_especializado_ausente", DimensaoConformidade.AtendimentoEspecializado, "Atendimento especializado", estado.AtendimentoEspecializado is not null),
        new ItemConformidade("distribuicao_vagas_ausente", DimensaoConformidade.DistribuicaoVagas, "Distribuição de vagas", estado.DistribuicaoVagas.Count > 0),
        ItemDasOfertas(
            "distribuicao_vagas_oferta_sem_nome",
            DimensaoConformidade.DistribuicaoVagas,
            "Nome de cada oferta congelado na distribuição de vagas",
            [.. estado.DistribuicaoVagas.Where(static oferta => oferta.OfertaCurso is null)],
            static oferta => $"{oferta.OfertaCursoOrigemId}"),
        new ItemConformidade("opcoes_de_curso_nao_declaradas", DimensaoConformidade.DistribuicaoVagas, "Número de opções de curso", estado.NumeroDeOpcoesDeCurso is not null),
        new ItemConformidade("opcoes_de_curso_acima_das_ofertas", DimensaoConformidade.DistribuicaoVagas, "Opções de curso: a distribuição de vagas tem ao menos tantos cursos quanto o número de opções", !RegrasDaDistribuicaoDeVagas.OpcoesDeCursoAcimaDasOfertas(estado.NumeroDeOpcoesDeCurso, estado.DistribuicaoVagas.Count)),
    ];

    private static List<ItemConformidade> ItensDaClassificacao(EstadoDaConfiguracao estado) =>
    [
        new ItemConformidade("classificacao_ausente", DimensaoConformidade.Classificacao, "Classificação", estado.Classificacao is not null),
    ];

    // Publicar sem declarar a cobrança de taxa é recusado: a ausência nunca vale "não cobra". Diferente da divulgação, aqui
    // a ausência não é estado publicável. O processo que cobra reconhece ao menos um fundamento de isenção: a fábrica já
    // recusa a combinação, mas o EF hidrata a linha sem passar por ela, então configuração gravada antes da regra volta ao
    // agregado nesse estado e publicaria sem este item. O segundo item é verdadeiro quando a configuração é nula: a
    // ausência já é o primeiro, e dois vermelhos pela mesma causa fariam o operador procurar dois problemas.
    private static List<ItemConformidade> ItensDaTaxaDeInscricao(EstadoDaConfiguracao estado) =>
    [
        new ItemConformidade("taxa_inscricao_nao_declarada", DimensaoConformidade.TaxaInscricao, "Taxa de inscrição e isenção", estado.ConfiguracaoTaxaInscricao is not null),
        new ItemConformidade("taxa_inscricao_sem_fundamento_de_isencao", DimensaoConformidade.TaxaInscricao, "Fundamentos de isenção do processo que cobra taxa", TemFundamentoDeIsencaoQuandoCobra(estado)),
    ];

    // Toda exigência que determina resultado precisa de ao menos uma base legal resolvida, e o grupo OU/N-de com
    // consequência própria também: a base legal dele não vem dos filhos.
    private static List<ItemConformidade> ItensDasExigenciasDocumentais(EstadoDaConfiguracao estado) =>
    [
        new ItemConformidade(
            "exigencias_base_legal_nao_resolvida",
            DimensaoConformidade.ExigenciasDocumentais,
            "Base legal das exigências documentais",
            Services.ValidadorBaseLegalExigencias.TodasResolvidas(estado.DocumentosExigidos)
                && GruposComConsequenciaTemBaseLegalResolvida(estado)),
    ];

    // O processo declara se aplica o bônus regional; a ausência da configuração não significa mais "sem bônus".
    // Declaração e configuração também precisam concordar: o EF hidrata a linha sem passar por DefinirBonusRegional.
    private static List<ItemConformidade> ItensDoBonusRegional(EstadoDaConfiguracao estado) =>
    [
        new ItemConformidade("bonus_regional_nao_declarado", DimensaoConformidade.BonusRegional, "Bônus regional: aplica ou não aplica", BonusRegionalDeclarado(estado)),
        new ItemConformidade("bonus_regional_fato_de_elegibilidade", DimensaoConformidade.BonusRegional, "Bônus regional: com inscrição própria, cita o fato booleano que decide quem recebe o acréscimo, conhecido na inscrição", PendenciasDosFatos.PendenciaDoFatoDeElegibilidade(estado) is null),
    ];

    // Com inscrição própria o desempate é obrigatório; com resultado importado a lista já vem classificada e o desempate
    // não se aplica.
    private static List<ItemConformidade> ItensDoDesempate(EstadoDaConfiguracao estado) =>
    [
        new ItemConformidade("criterios_desempate_ausentes", DimensaoConformidade.Desempate, "Critérios de desempate (inscrição própria)", estado.OrigemCandidatos != OrigemCandidatos.InscricaoPropria || estado.CriteriosDesempate.Count > 0),
    ];

    /// <summary>
    /// A escala em que a nota de cada etapa é lançada e como a nota é obtida: itens que dependem do grafo (o que compõe a
    /// média, o que a classificação e o desempate citam), por isso sãa conferência de publicação e não recusa de gravação.
    /// </summary>
    private static List<ItemConformidade> ItensDasEtapas(EstadoDaConfiguracao estado)
    {
        EtapasDoProcesso etapas = EtapasDoProcesso.Das(estado.Etapas);
        CitacoesDeNota citacoes = RegrasDasEtapas.CitacoesDeNotaDaConfiguracao(estado);

        return [.. etapas.ItensDaEscala(citacoes), .. etapas.ItensDaAvaliacao(citacoes)];
    }

    private static List<ItemConformidade> ItensDaFormulaDaClassificacao(EstadoDaConfiguracao estado) =>
        estado.Classificacao is { RegraCalculo.Codigo: RegraCalculoCodigo.FormulaMediaPonderada }
            ?
            [
                new ItemConformidade("classificacao_divisor_media_invalido", DimensaoConformidade.Classificacao, "Divisor da média (fórmula local)", estado.CalcularDivisorMedia() > 0),
                new ItemConformidade("classificacao_escala_nota_final_nao_declarada", DimensaoConformidade.Classificacao, "Escala da nota final (fórmula local)", estado.Classificacao.EscalaNotaFinal is not null),
            ]
            : [];

    // O candidato escolhe na inscrição, entre as edições admitidas, a do ENEM cujas notas a classificação usa. Com o
    // resultado importado não há inscrição: a edição vem com a importação, e a declaração não teria quem a lesse.
    private static List<ItemConformidade> ItensDaClassificacaoBaseadaEmEnem(EstadoDaConfiguracao estado) =>
    [
        .. estado.Classificacao is { CalculaPelosPesosPorAreaDoEnem: true } classificacaoEnem
            ? ItensDaMediaPelosPesosPorAreaDoEnem(estado, classificacaoEnem)
            : [],
        .. EdicaoDoEnemDeclaradaComInscricaoPropria(estado),
    ];

    private static List<ItemConformidade> EdicaoDoEnemDeclaradaComInscricaoPropria(EstadoDaConfiguracao estado) =>
        estado.OrigemCandidatos == OrigemCandidatos.InscricaoPropria && estado.Classificacao is { BaseadoEmEnem: true }
            ?
            [
                new ItemConformidade(
                    "classificacao_edicao_enem_nao_declarada",
                    DimensaoConformidade.Classificacao,
                    "Edições do ENEM admitidas (classificação baseada em ENEM com inscrição própria)",
                    estado.OpcoesDeclaradas.Any(static o => string.Equals(o.FatoCodigo, EdicaoDoEnem.Fato, StringComparison.Ordinal))),
            ]
            : [];

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
        ItemDoGrupoDeAreaForaDoQuadro(estado, classificacao),
    ];

    private static ItemConformidade ItemDoGrupoDeAreaForaDoQuadro(EstadoDaConfiguracao estado, ConfiguracaoClassificacao classificacao) =>
        ItemDasOfertas(
            "classificacao_grupo_area_enem_da_oferta_fora_do_quadro",
            DimensaoConformidade.Classificacao,
            "Grupo de área do ENEM de cada oferta presente no quadro de pesos por área",
            OfertasComGrupoAreaEnemForaDoQuadro(estado, classificacao),
            static oferta => $"{oferta.OfertaCursoOrigemId} (grupo {DescreverGrupoAreaEnem(oferta.GrupoAreaEnem!)})");

    /// <summary>
    /// Item que identifica as ofertas pendentes, em ordem estável: sem elas, quem publica não
    /// sabe qual corrigir. O código do item continua sendo a identidade; a lista é só redação.
    /// </summary>
    /// <remarks>
    /// As ofertas ficam entre parênteses, separadas por ponto e vírgula e sem ponto final, como a mensagem de todo item.
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
    /// Processo que cobra taxa reconhece ao menos um fundamento de isenção.
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

    /// <summary>Grupos <c>OU</c>/<c>N-de</c> com <see cref="NoExigencia.Consequencia"/> própria — cada um precisa de ≥1 <see cref="NoExigenciaBaseLegal"/> <see cref="StatusBaseLegal.Resolvido"/>, mesma semântica de <see cref="Services.ValidadorBaseLegalExigencias"/> para folha.</summary>
    internal static bool GruposComConsequenciaTemBaseLegalResolvida(EstadoDaConfiguracao estado) =>
        estado.NosExigencia
            .Where(static no => no.DeterminaResultado())
            .All(no => no.BasesLegais.Any(static b => b.Status == StatusBaseLegal.Resolvido));
}
