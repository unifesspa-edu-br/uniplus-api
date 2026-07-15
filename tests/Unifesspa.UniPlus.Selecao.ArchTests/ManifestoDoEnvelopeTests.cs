namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O <b>manifesto</b> do envelope: toda propriedade de negócio das entidades de
/// configuração está declarada como <b>dentro</b> ou <b>fora</b> dele — e a omissão é
/// impossível (Story #859).
/// </summary>
/// <remarks>
/// <para>
/// <b>O ponto cego que este teste fecha.</b> Encoder, decoder e round-trip são três listas
/// escritas à mão. Se alguém acrescentar uma propriedade de negócio a
/// <c>ModalidadeSelecionada</c> — digamos, um novo critério de elegibilidade — e esquecer
/// dela <b>nos dois</b>, o envelope continua idêntico, a golden fixture continua batendo,
/// o round-trip continua verde e a guarda de produção aprova. O campo simplesmente
/// <b>não existe</b> para o congelamento: ele é configurado, publicado, e some no primeiro
/// descarte. <b>Nada acusa</b> — a simetria do esquecimento o torna invisível.
/// </para>
/// <para>
/// A única defesa é uma lista que o compilador force a manter. Aqui, acrescentar uma
/// propriedade a qualquer entidade do grafo <b>quebra o build</b> até que alguém declare,
/// por escrito, se ela entra no documento que tem peso jurídico — ou por que não entra.
/// </para>
/// </remarks>
public sealed class ManifestoDoEnvelopeTests
{
    /// <summary>Herdadas de <c>EntityBase</c> — identidade e auditoria, nunca conteúdo de negócio.</summary>
    private static readonly HashSet<string> DaEntidadeBase =
        ["Id", "CreatedAt", "UpdatedAt", "DomainEvents", "IsDeleted", "DeletedAt", "DeletedBy"];

    /// <summary>
    /// O manifesto. Cada propriedade de negócio de cada entidade do grafo aparece aqui —
    /// como <b>congelada</b> (entra no envelope) ou <b>excluída</b> (com o motivo).
    /// </summary>
    private static readonly Dictionary<Type, (string[] Congeladas, (string Propriedade, string Motivo)[] Excluidas)> Manifesto = new()
    {
        [typeof(EtapaProcesso)] = (
            ["Nome", "Carater", "Peso", "NotaMinima", "Ordem"],
            [
                ("ProcessoSeletivoId", "FK interna — reconstruída junto com o grafo, nunca congelada (ADR-0110 D2)."),
                ("ComponeNota", "Derivada de Carater + Peso — congelá-la duplicaria a fonte de verdade."),
            ]),

        [typeof(OfertaAtendimentoEspecializado)] = (
            ["Condicoes", "Recursos", "TiposDeficiencia"],
            [("ProcessoSeletivoId", "FK interna.")]),

        [typeof(OfertaCondicao)] = (
            ["CondicaoOrigemId", "CondicaoCodigo", "CondicaoNome"],
            [("OfertaAtendimentoEspecializadoId", "FK interna.")]),

        [typeof(OfertaRecurso)] = (
            ["RecursoOrigemId", "RecursoNome"],
            [("OfertaAtendimentoEspecializadoId", "FK interna.")]),

        [typeof(OfertaTipoDeficiencia)] = (
            ["TipoDeficienciaOrigemId", "TipoDeficienciaNome"],
            [("OfertaAtendimentoEspecializadoId", "FK interna.")]),

        [typeof(ConfiguracaoDistribuicaoVagas)] = (
            ["OfertaCursoOrigemId", "VoBase", "Pr", "RegraDistribuicao", "ReferenciaDemografica", "Modalidades"],
            [("ProcessoSeletivoId", "FK interna.")]),

        [typeof(ModalidadeSelecionada)] = (
            [
                "ModalidadeOrigemId", "Codigo", "Descricao", "NaturezaLegal", "ComposicaoVagas",
                "ComposicaoOrigemCodigo", "RegraRemanejamento", "RemanejamentoDestino", "RemanejamentoPar",
                "RemanejamentoFallback", "CriteriosCumulativos", "AcaoQuandoIndeferido", "BaseLegal",
            ],
            [("ConfiguracaoDistribuicaoVagasId", "FK interna.")]),

        [typeof(ConfiguracaoBonusRegional)] = (
            ["Regra", "Fator", "Teto", "MunicipioConvenio", "BaseLegal"],
            [("ProcessoSeletivoId", "FK interna.")]),

        [typeof(CriterioDesempate)] = (
            ["Ordem", "Regra", "Args"],
            [("ProcessoSeletivoId", "FK interna.")]),

        [typeof(ConfiguracaoClassificacao)] = (
            [
                "RegraCalculo", "RegraArredondamento", "CasasArredondamento",
                "RegraOrdemAlocacao", "NOpcoesAlocacao", "RegrasEliminacao",
            ],
            [("ProcessoSeletivoId", "FK interna.")]),

        [typeof(RegraEliminacao)] = (
            ["Regra", "Args"],
            [("ConfiguracaoClassificacaoId", "FK interna.")]),

        [typeof(DadosEdital)] = (
            ["Numero", "PeriodoInscricaoInicio", "PeriodoInscricaoFim", "DocumentoEditalId"],
            []),

        [typeof(ReferenciaRegra)] = (
            ["Codigo", "Versao", "Hash"],
            []),

        [typeof(ReferenciaReservaDemograficaSnapshot)] = (
            ["OrigemId", "CensoReferencia", "PpiPercentual", "QuilombolaPercentual", "PcdPercentual", "BaseLegal"],
            []),

        // As variantes polimórficas. Elas são persistidas INTEIRAS como JSON, e o codec as
        // lê e escreve em dois `switch` sobre o código da regra — duas listas manuais, num
        // ponto em que o envelope nem sequer carrega discriminador (MAIOR-IDADE e
        // ZERO-EM-AREA serializam como `{}`). Uma propriedade nova aqui, esquecida nos dois
        // switches, é invisível: o envelope não muda, a fixture não muda, o round-trip fica
        // verde — e o campo some no primeiro descarte.
        [typeof(ArgsDesempateMaiorNotaEtapa)] = (["EtapaRef"], []),
        [typeof(ArgsDesempateMaiorIdade)] = ([], []),
        [typeof(ArgsDesempateIdoso)] = (["IdadeMinima"], []),
        [typeof(ArgsDesempatePredicadoFato)] = (["Condicao"], []),
        // CondicaoDnf (ADR-0111, Story #847): átomo tipado { Fato, Operador, Valor }
        // reusado literalmente pela variante acima — mesmo cuidado de propriedade
        // esquecida se aplica aqui.
        [typeof(CondicaoDnf)] = (["Fato", "Operador", "Valor"], []),
        [typeof(ArgsElimNotaMinimaEtapa)] = (["EtapaRef", "NotaMinima"], []),
        [typeof(ArgsElimCorteRedacao)] = (["Minimo"], []),
        [typeof(ArgsElimZeroEmArea)] = ([], []),
    };

    /// <summary>
    /// As duas uniões discriminadas do grafo. A variante é escolhida pelo <b>código da
    /// regra</b>, porque o envelope não carrega discriminador — o que torna cada nova
    /// variante uma chance a mais de o codec silenciosamente não conhecê-la.
    /// </summary>
    private static readonly Type[] UnioesDiscriminadas =
        [typeof(ArgsCriterioDesempate), typeof(ArgsRegraEliminacao)];

    [Fact(DisplayName = "Toda propriedade de negócio do grafo está declarada no manifesto — dentro ou fora do envelope")]
    public void TodaPropriedade_EstaNoManifesto()
    {
        List<string> naoDeclaradas = [];

        foreach ((Type tipo, (string[] congeladas, (string Propriedade, string Motivo)[] excluidas)) in Manifesto)
        {
            HashSet<string> declaradas = [.. congeladas, .. excluidas.Select(static e => e.Propriedade)];

            IEnumerable<string> propriedades = tipo
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(static p => p.Name)
                .Where(nome => !DaEntidadeBase.Contains(nome));

            naoDeclaradas.AddRange(propriedades
                .Where(propriedade => !declaradas.Contains(propriedade))
                .Select(propriedade => $"{tipo.Name}.{propriedade}"));
        }

        naoDeclaradas.Should().BeEmpty(
            "uma propriedade de negócio que não está no manifesto é uma que ninguém decidiu se entra no documento " +
            "com peso jurídico. Se ela for esquecida no encoder E no decoder — o que é fácil, são duas listas " +
            "manuais —, o envelope não muda, a golden fixture não muda, o round-trip fica verde e a guarda de " +
            "produção aprova: o campo é configurado, publicado, e some no primeiro descarte, sem que nada acuse. " +
            "Declare-a em ManifestoDoEnvelopeTests: congelada, ou excluída com o motivo. " +
            $"Não declaradas: {string.Join(", ", naoDeclaradas)}");
    }

    /// <summary>
    /// O manifesto não pode declarar o que não existe: uma propriedade renomeada ou removida
    /// deixaria uma entrada morta, e o teste acima continuaria verde protegendo um fantasma.
    /// </summary>
    [Fact(DisplayName = "O manifesto não declara propriedade que não existe mais")]
    public void Manifesto_NaoTemEntradaMorta()
    {
        List<string> fantasmas = [];

        foreach ((Type tipo, (string[] congeladas, (string Propriedade, string Motivo)[] excluidas)) in Manifesto)
        {
            HashSet<string> reais = [.. tipo
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(static p => p.Name)];

            fantasmas.AddRange(congeladas
                .Concat(excluidas.Select(static e => e.Propriedade))
                .Where(declarada => !reais.Contains(declarada))
                .Select(declarada => $"{tipo.Name}.{declarada}"));
        }

        fantasmas.Should().BeEmpty(
            $"o manifesto declara propriedades que não existem mais no domínio: {string.Join(", ", fantasmas)}");
    }

    /// <summary>
    /// Toda exclusão carrega um <b>motivo</b>. Uma propriedade excluída do envelope sem
    /// justificativa escrita é uma decisão que ninguém tomou — e é exatamente onde uma
    /// dimensão de negócio se perde por descuido.
    /// </summary>
    [Fact(DisplayName = "Toda propriedade excluída do envelope declara o motivo")]
    public void Exclusoes_TemMotivo()
    {
        foreach ((Type tipo, (string[] _, (string Propriedade, string Motivo)[] excluidas)) in Manifesto)
        {
            foreach ((string propriedade, string motivo) in excluidas)
            {
                motivo.Should().NotBeNullOrWhiteSpace(
                    $"{tipo.Name}.{propriedade} está fora do envelope — o porquê tem de estar escrito");
            }
        }
    }

    /// <summary>
    /// O manifesto não pode <b>ele mesmo</b> ser uma lista escrita à mão que alguém esquece
    /// de estender: os tipos são <b>descobertos</b> a partir do
    /// <see cref="GrafoConfiguracao"/>, e cada um que aparecer tem de estar declarado.
    /// </summary>
    /// <remarks>
    /// Sem isto, o manifesto teria o mesmo ponto cego que ele existe para fechar — uma
    /// entidade nova no grafo (ou uma <b>variante nova</b> de <c>Args</c>) simplesmente não
    /// seria percorrida, e a propriedade dela nunca precisaria ser declarada. A prova de
    /// completude tem de vir do compilador, não da memória de quem editou o teste.
    /// </remarks>
    [Fact(DisplayName = "Todo tipo alcançável a partir do grafo está no manifesto — inclusive cada variante de Args")]
    public void TodoTipoDoGrafo_EstaNoManifesto()
    {
        HashSet<Type> alcancados = [];
        foreach (PropertyInfo propriedade in typeof(GrafoConfiguracao).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Alcancar(TipoDeNegocio(propriedade.PropertyType), alcancados);
        }

        // DadosEdital não pende do grafo — ele entra na canonicalização por fora (bloco
        // `periodo` + `hashesEdital`), mas é igualmente congelado e igualmente reidratado.
        Alcancar(typeof(DadosEdital), alcancados);

        IEnumerable<Type> naoDeclarados = alcancados.Where(t => !Manifesto.ContainsKey(t));

        naoDeclarados.Should().BeEmpty(
            "todo tipo que o envelope congela tem de estar no manifesto — inclusive cada VARIANTE de " +
            "ArgsCriterioDesempate e ArgsRegraEliminacao, que são persistidas inteiras como JSON e lidas por dois " +
            "`switch` sobre o código da regra. Uma variante nova, ou uma propriedade nova numa existente, esquecida " +
            "nos dois switches, não muda o envelope, não muda a fixture, e o round-trip fica verde: o campo some no " +
            $"primeiro descarte. Não declarados: {string.Join(", ", naoDeclarados.Select(static t => t.Name))}");
    }

    /// <summary>
    /// Acrescenta o tipo ao conjunto e desce recursivamente pelas propriedades dele —
    /// abrindo as uniões discriminadas em <b>todas</b> as suas variantes.
    /// </summary>
    private static void Alcancar(Type? tipo, HashSet<Type> acumulador)
    {
        if (tipo is null || !EDoDominio(tipo) || !acumulador.Add(tipo))
        {
            return;
        }

        // A união abstrata não tem propriedades próprias; o que o envelope congela são as
        // VARIANTES dela. Abri-las aqui é o que faz uma variante nova quebrar o build.
        if (UnioesDiscriminadas.Contains(tipo))
        {
            acumulador.Remove(tipo);
            foreach (Type variante in tipo.Assembly.GetTypes().Where(t => t.IsSubclassOf(tipo) && !t.IsAbstract))
            {
                Alcancar(variante, acumulador);
            }

            return;
        }

        IEnumerable<PropertyInfo> deNegocio = tipo
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(static p => !DaEntidadeBase.Contains(p.Name));

        foreach (PropertyInfo propriedade in deNegocio)
        {
            Alcancar(TipoDeNegocio(propriedade.PropertyType), acumulador);
        }
    }

    /// <summary>Desembrulha <c>IReadOnlyCollection&lt;T&gt;</c> / <c>Nullable&lt;T&gt;</c> e devolve o tipo que interessa.</summary>
    private static Type? TipoDeNegocio(Type tipo)
    {
        Type alvo = Nullable.GetUnderlyingType(tipo) ?? tipo;

        if (alvo.IsGenericType && alvo.GetGenericArguments().Length == 1)
        {
            alvo = alvo.GetGenericArguments()[0];
        }

        return EDoDominio(alvo) ? alvo : null;
    }

    private static bool EDoDominio(Type tipo) =>
        tipo.Namespace?.StartsWith("Unifesspa.UniPlus.Selecao.Domain", StringComparison.Ordinal) == true
        && !tipo.IsEnum
        && tipo != typeof(ProcessoSeletivo);
}
