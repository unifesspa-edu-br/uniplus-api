namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Collections.Generic;
using System.Linq;

using Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Nó da árvore de satisfação de documentos exigidos (Story #920) — substitui o antigo
/// <c>DocumentoExigido.GrupoSatisfacaoId</c> (grupo plano). Um nó é <b>folha</b>
/// (<see cref="TipoNo.Folha"/>, envolve um <see cref="DocumentoExigido"/> 1:1) ou <b>grupo</b>
/// (<see cref="TipoNo.GrupoE"/>/<see cref="TipoNo.GrupoOu"/>, conector + filhos). Toda exigência,
/// mesmo "solteira", é raiz de uma árvore de 1 nó — não existe <c>DocumentoExigido</c> fora da
/// árvore.
/// </summary>
/// <remarks>
/// <para>
/// <b>Um único tipo de nó, discriminado por <see cref="Tipo"/></b> — não subclasses EF
/// (TPH/TPT): a árvore é homogênea o bastante (todo nó tem <see cref="NoPaiId"/>/<see cref="Ordem"/>/
/// <see cref="Filhos"/>) que um discriminador com campos opcionais por tipo é mais barato que
/// polimorfismo real. Mesmo estilo de <see cref="DocumentoExigido"/> (campos condicionados por
/// <see cref="Enums.Aplicabilidade"/>).
/// </para>
/// <para>
/// <b>Cardinalidade</b> (Story #920 + #921): <see cref="QuantidadeMinima"/> vale para os dois
/// tipos que a carregam — em <see cref="TipoNo.GrupoOu"/>, quantos FILHOS satisfeitos; em
/// <see cref="TipoNo.Folha"/>, quantas APRESENTAÇÕES distintas da mesma exigência (não arquivos —
/// frente/verso é uma apresentação só). <see cref="ChaveDistincao"/> qualifica COMO essas
/// apresentações se distinguem (calendário derivável ou ocorrências congeladas).
/// </para>
/// <para>
/// <b>Consequência/base legal de grupo</b>: só <see cref="TipoNo.GrupoOu"/> pode carregar
/// <see cref="Consequencia"/> (opcional) + <see cref="BasesLegais"/> própria — um grupo
/// <c>OU</c>/<c>N-de</c> com consequência é exigência de 1ª classe (base legal NÃO derivada dos
/// filhos). <see cref="TipoNo.GrupoE"/> é transparente: nunca carrega consequência nem base legal
/// própria (o requirement <c>Consequência por nó e fronteira ativa de emissão</c> da spec).
/// </para>
/// </remarks>
public sealed class NoExigencia : EntityBase
{
    private static readonly string[] ConsequenciasValidas =
    [
        "ELIMINA",
        "RECLASSIFICA_AC",
        "REMOVE_VANTAGEM",
        "PENDENCIA_REENVIO",
    ];

    public const int QuantidadeMinimaPadrao = 1;

    /// <summary>
    /// Teto operacional de <see cref="QuantidadeMinima"/> em folha — não é regra de satisfação
    /// (o domínio não limita quantas apresentações uma exigência pode pedir), mas defende
    /// <see cref="ComputarCompetencias"/>/<see cref="ComputarExercicios"/> de loops/alocações
    /// desproporcionais e de <c>quantidadeMinima</c> maior que o calendário representável.
    /// </summary>
    private const int QuantidadeMinimaDeFolhaMaxima = 366;

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>Nó pai na árvore — <see langword="null"/> significa raiz (Story #920, árvore recursiva sem limite semântico de profundidade).</summary>
    public Guid? NoPaiId { get; private set; }

    /// <summary>Posição entre irmãos — determinismo de avaliação/serialização (não afeta a álgebra de satisfação, que é comutativa em E/OU).</summary>
    public int Ordem { get; private set; }

    public TipoNo Tipo { get; private set; }

    /// <summary>Só <see cref="TipoNo.Folha"/> — o <see cref="DocumentoExigido"/> que este nó envolve.</summary>
    public Guid? DocumentoExigidoId { get; private set; }

    /// <summary>Navegação da folha — carregada junto pelo repositório (mesmo agregado, mesma transação).</summary>
    public DocumentoExigido? DocumentoExigido { get; private set; }

    /// <summary>
    /// Cardinalidade do nó (default <see cref="QuantidadeMinimaPadrao"/>) — em <see cref="TipoNo.Folha"/>
    /// (Story #921), número mínimo de APRESENTAÇÕES distintas (não arquivos: frente/verso = 1
    /// apresentação); em <see cref="TipoNo.GrupoOu"/> (Story #920), quantos filhos satisfeitos.
    /// <see langword="null"/> só em <see cref="TipoNo.GrupoE"/> (sem cardinalidade própria).
    /// </summary>
    public int? QuantidadeMinima { get; private set; }

    /// <summary>
    /// Só <see cref="TipoNo.GrupoOu"/>, opcional — ∈ {ELIMINA, RECLASSIFICA_AC, REMOVE_VANTAGEM,
    /// PENDENCIA_REENVIO} (mesmo vocabulário fechado de <see cref="DocumentoExigido.ConsequenciaIndeferimento"/>).
    /// <see cref="TipoNo.GrupoE"/> é transparente e NUNCA carrega consequência própria.
    /// </summary>
    public string? Consequencia { get; private set; }

    /// <summary>
    /// Só <see cref="TipoNo.Folha"/> (Story #921) — catálogo fechado de 3 valores para distinguir
    /// cardinalidade de apresentações. <see langword="null"/> = sem qualificação (contagem bruta de
    /// <see cref="QuantidadeMinima"/> apresentações, sem exigir slot específico).
    /// </summary>
    public ChaveDistincao? ChaveDistincao { get; private set; }

    /// <summary>
    /// Âncora para <see cref="Enums.ChaveDistincao.CompetenciaMensal"/>/<see cref="Enums.ChaveDistincao.ExercicioAnual"/>
    /// — obrigatória sse uma dessas duas chaves, proibida caso contrário. "Últimos N" = as N
    /// unidades regulares imediatamente ≤ esta data (config congelada; ver <see cref="SlotsEsperados"/>).
    /// </summary>
    public DateOnly? DataReferencia { get; private set; }

    /// <summary>
    /// Só <see cref="Enums.ChaveDistincao.Ocorrencia"/>, opcional — a lista de slots esperados
    /// (identificadores congelados, ex.: as duas eleições determinadas pelo edital). Presente ⇒
    /// <see cref="QuantidadeMinima"/> deve igualar o tamanho da lista (cada slot é uma apresentação
    /// distinta e obrigatória). Ausente ⇒ modo "distinção pura": bastam N ocorrências diferentes,
    /// sem recência.
    /// </summary>
    public IReadOnlyList<string>? OcorrenciasEsperadas { get; private set; }

    /// <summary>
    /// Story #922 — marca esta subárvore (este nó + todos os descendentes) como repetível por
    /// instância de <see cref="Enums.TipoEntidade"/>: em runtime, avaliada UMA VEZ POR INSTÂNCIA
    /// que o candidato declarar desse tipo, com gatilhos internos resolvidos contra os ATRIBUTOS
    /// da instância (sujeito trocado — mesmo motor de gatilho da folha). Repetição NÃO aninha —
    /// nenhum descendente pode também ser <see cref="RepetePorEntidade"/> (validado em
    /// <see cref="CriarGrupo"/>, único ponto onde um nó marcado pode ter descendentes).
    /// </summary>
    public TipoEntidade? RepetePorEntidade { get; private set; }

    private readonly List<NoExigencia> _filhos = [];

    /// <summary>Filhos ordenados por <see cref="Ordem"/> — vazio em folha, não-vazio em grupo (invariante SHALL: grupo não vazio).</summary>
    public IReadOnlyList<NoExigencia> Filhos => _filhos.AsReadOnly();

    private readonly List<NoExigenciaBaseLegal> _basesLegais = [];

    /// <summary>Base legal 1:N PRÓPRIA do grupo (só quando <see cref="Consequencia"/> presente) — não derivada dos filhos.</summary>
    public IReadOnlyCollection<NoExigenciaBaseLegal> BasesLegais => _basesLegais.AsReadOnly();

    private NoExigencia() { }

    /// <summary>
    /// Cria um nó folha — envolve <paramref name="documentoExigido"/> 1:1. Cardinalidade
    /// qualificada (Story #921): <paramref name="quantidadeMinima"/> conta APRESENTAÇÕES (não
    /// arquivos); <paramref name="chaveDistincao"/>/<paramref name="dataReferencia"/>/
    /// <paramref name="ocorrenciasEsperadas"/> só fazem sentido juntos, por chave (ver spec
    /// "Cardinalidade qualificada" — <c>documentos-exigidos-composicao</c>).
    /// </summary>
    public static Result<NoExigencia> CriarFolha(
        DocumentoExigido documentoExigido,
        int ordem,
        int? quantidadeMinima = null,
        ChaveDistincao? chaveDistincao = null,
        DateOnly? dataReferencia = null,
        IReadOnlyList<string>? ocorrenciasEsperadas = null,
        TipoEntidade? repetePorEntidade = null)
    {
        ArgumentNullException.ThrowIfNull(documentoExigido);

        if (ordem < 0)
        {
            return Result<NoExigencia>.Failure(new DomainError(
                "NoExigencia.OrdemInvalida", "A ordem do nó não pode ser negativa."));
        }

        int n = quantidadeMinima ?? QuantidadeMinimaPadrao;
        if (n < 1 || n > QuantidadeMinimaDeFolhaMaxima)
        {
            return Result<NoExigencia>.Failure(new DomainError(
                "NoExigencia.QuantidadeMinimaDeFolhaInvalida",
                $"quantidadeMinima de uma folha, quando presente, deve estar entre 1 e {QuantidadeMinimaDeFolhaMaxima}."));
        }

        ChaveDistincao chaveNormalizada = chaveDistincao ?? Enums.ChaveDistincao.Nenhuma;
        if (!Enum.IsDefined(chaveNormalizada))
        {
            return Result<NoExigencia>.Failure(new DomainError(
                "NoExigencia.ChaveDistincaoInvalida",
                "chaveDistincao fora do catálogo fechado (COMPETENCIA_MENSAL, EXERCICIO_ANUAL, OCORRENCIA)."));
        }

        DomainError? erroDeChave = chaveNormalizada switch
        {
            Enums.ChaveDistincao.Nenhuma => ValidarSemChave(dataReferencia, ocorrenciasEsperadas),
            Enums.ChaveDistincao.CompetenciaMensal or Enums.ChaveDistincao.ExercicioAnual =>
                ValidarChaveDeCalendario(chaveNormalizada, dataReferencia, ocorrenciasEsperadas, n),
            Enums.ChaveDistincao.Ocorrencia => ValidarChaveDeOcorrencia(dataReferencia, ocorrenciasEsperadas, n),
            _ => throw new ArgumentOutOfRangeException(nameof(chaveDistincao), chaveNormalizada, "ChaveDistincao desconhecida."),
        };
        if (erroDeChave is not null)
        {
            return Result<NoExigencia>.Failure(erroDeChave);
        }

        TipoEntidade tipoEntidadeNormalizado = repetePorEntidade ?? TipoEntidade.Nenhuma;
        if (ValidarCatalogoTipoEntidade(tipoEntidadeNormalizado) is { } erroTipoEntidade)
        {
            return Result<NoExigencia>.Failure(erroTipoEntidade);
        }

        return Result<NoExigencia>.Success(new NoExigencia
        {
            Tipo = TipoNo.Folha,
            Ordem = ordem,
            DocumentoExigidoId = documentoExigido.Id,
            DocumentoExigido = documentoExigido,
            QuantidadeMinima = n,
            ChaveDistincao = chaveNormalizada == Enums.ChaveDistincao.Nenhuma ? null : chaveNormalizada,
            DataReferencia = dataReferencia,
            OcorrenciasEsperadas = ocorrenciasEsperadas is null ? null : [.. ocorrenciasEsperadas],
            RepetePorEntidade = tipoEntidadeNormalizado == TipoEntidade.Nenhuma ? null : tipoEntidadeNormalizado,
        });
    }

    /// <summary>Story #922 — catálogo fechado de <see cref="TipoEntidade"/> (defesa em profundidade: o wire já valida via <c>TipoEntidadeCodigo</c>, mas um <c>Reidratar</c> com dado corrompido não passa pelo validator).</summary>
    private static DomainError? ValidarCatalogoTipoEntidade(TipoEntidade tipo) =>
        Enum.IsDefined(tipo)
            ? null
            : new DomainError(
                "NoExigencia.TipoEntidadeInvalido",
                "repetePorEntidade fora do catálogo fechado (MEMBRO_NUCLEO_FAMILIAR, PESSOA_JURIDICA_VINCULADA).");

    private static DomainError? ValidarSemChave(DateOnly? dataReferencia, IReadOnlyList<string>? ocorrenciasEsperadas)
    {
        if (dataReferencia is not null)
        {
            return new DomainError(
                "NoExigencia.DataReferenciaIndevidaParaChave",
                "dataReferencia só é aceita quando chaveDistincao é COMPETENCIA_MENSAL ou EXERCICIO_ANUAL.");
        }

        if (ocorrenciasEsperadas is not null)
        {
            return new DomainError(
                "NoExigencia.OcorrenciasEsperadasIndevidasParaChave",
                "ocorrenciasEsperadas só é aceita quando chaveDistincao é OCORRENCIA.");
        }

        return null;
    }

    private static DomainError? ValidarChaveDeCalendario(
        ChaveDistincao chave, DateOnly? dataReferencia, IReadOnlyList<string>? ocorrenciasEsperadas, int quantidadeMinima)
    {
        if (dataReferencia is null)
        {
            return new DomainError(
                "NoExigencia.DataReferenciaObrigatoriaParaChaveCalendario",
                "dataReferencia é obrigatória quando chaveDistincao é COMPETENCIA_MENSAL ou EXERCICIO_ANUAL.");
        }

        if (ocorrenciasEsperadas is not null)
        {
            return new DomainError(
                "NoExigencia.OcorrenciasEsperadasIndevidasParaChave",
                "ocorrenciasEsperadas só é aceita quando chaveDistincao é OCORRENCIA.");
        }

        // Defende ComputarCompetencias/ComputarExercicios de subtrair além do calendário
        // representável (ano 1, mês 1) — "últimos N" com N maior que a distância de
        // dataReferencia até o início do calendário não tem N slots regulares para derivar.
        DateOnly ancora = dataReferencia.Value;
        int unidadeMaisAntigaAlcancavel = chave == Enums.ChaveDistincao.CompetenciaMensal
            ? ((ancora.Year - 1) * 12) + ancora.Month - (quantidadeMinima - 1)
            : ancora.Year - (quantidadeMinima - 1);
        if (unidadeMaisAntigaAlcancavel < 1)
        {
            return new DomainError(
                "NoExigencia.QuantidadeMinimaExcedeJanelaRepresentavel",
                $"quantidadeMinima ({quantidadeMinima}) exige unidades regulares anteriores ao calendário representável a partir de dataReferencia ({ancora:yyyy-MM-dd}).");
        }

        return null;
    }

    private static DomainError? ValidarChaveDeOcorrencia(DateOnly? dataReferencia, IReadOnlyList<string>? ocorrenciasEsperadas, int quantidadeMinima)
    {
        if (dataReferencia is not null)
        {
            return new DomainError(
                "NoExigencia.DataReferenciaIndevidaParaChave",
                "dataReferencia não é aceita quando chaveDistincao é OCORRENCIA.");
        }

        if (ocorrenciasEsperadas is null)
        {
            return null;
        }

        if (ocorrenciasEsperadas.Count == 0)
        {
            return new DomainError(
                "NoExigencia.OcorrenciasEsperadasVazia",
                "ocorrenciasEsperadas, quando presente, não pode ser vazia.");
        }

        if (ocorrenciasEsperadas.Any(static id => string.IsNullOrWhiteSpace(id)))
        {
            return new DomainError(
                "NoExigencia.OcorrenciasEsperadasComIdVazio",
                "Os identificadores de ocorrenciasEsperadas não podem ser vazios ou em branco.");
        }

        if (ocorrenciasEsperadas.Distinct(StringComparer.Ordinal).Count() != ocorrenciasEsperadas.Count)
        {
            return new DomainError(
                "NoExigencia.OcorrenciasEsperadasComIdsDuplicados",
                "Os identificadores de ocorrenciasEsperadas devem ser únicos.");
        }

        if (quantidadeMinima != ocorrenciasEsperadas.Count)
        {
            return new DomainError(
                "NoExigencia.OcorrenciasEsperadasQuantidadeMinimaDivergente",
                $"quantidadeMinima ({quantidadeMinima}) deve ser igual ao número de ocorrenciasEsperadas ({ocorrenciasEsperadas.Count}).");
        }

        return null;
    }

    /// <summary>
    /// Os slots esperados desta folha, derivados da config congelada — <see langword="null"/>
    /// quando não há qualificação (<see cref="ChaveDistincao"/> ausente) OU quando é
    /// <see cref="Enums.ChaveDistincao.Ocorrencia"/> sem <see cref="OcorrenciasEsperadas"/> (modo
    /// "distinção pura": conta bruta de apresentações distintas, sem slot concreto a cobrir).
    /// Quando não-nulo, cada slot precisa de ≥1 apresentação própria — omitir um deixa a folha
    /// pendente, mesmo com outras apresentações sobrando (Story #921).
    /// </summary>
    public IReadOnlyList<string>? SlotsEsperados() => ChaveDistincao switch
    {
        Enums.ChaveDistincao.CompetenciaMensal => ComputarCompetencias(DataReferencia!.Value, QuantidadeMinima ?? QuantidadeMinimaPadrao),
        Enums.ChaveDistincao.ExercicioAnual => ComputarExercicios(DataReferencia!.Value, QuantidadeMinima ?? QuantidadeMinimaPadrao),
        Enums.ChaveDistincao.Ocorrencia => OcorrenciasEsperadas,
        _ => null,
    };

    /// <summary>As N competências mensais ("AAAA-MM") regulares imediatamente ≤ <paramref name="ancora"/>, do mais recente ao mais antigo — <see cref="DateOnly.AddMonths"/> já resolve virada de ano.</summary>
    private static List<string> ComputarCompetencias(DateOnly ancora, int quantidade)
    {
        List<string> competencias = new(quantidade);
        DateOnly cursor = ancora;
        for (int i = 0; i < quantidade; i++)
        {
            competencias.Add($"{cursor.Year:D4}-{cursor.Month:D2}");
            if (i < quantidade - 1)
            {
                cursor = cursor.AddMonths(-1);
            }
        }

        return competencias;
    }

    /// <summary>Os N exercícios anuais ("AAAA") regulares imediatamente ≤ <paramref name="ancora"/>, do mais recente ao mais antigo.</summary>
    private static List<string> ComputarExercicios(DateOnly ancora, int quantidade)
    {
        List<string> exercicios = new(quantidade);
        for (int i = 0; i < quantidade; i++)
        {
            exercicios.Add($"{ancora.Year - i:D4}");
        }

        return exercicios;
    }

    /// <summary>
    /// Cria um nó grupo (<see cref="TipoNo.GrupoE"/> ou <see cref="TipoNo.GrupoOu"/>), validando
    /// os invariantes SHALL do cadastro (Story #920, tasks §1.2): grupo não vazio, árvore sem
    /// ciclo (defensivo — ver remarks), mesma fase entre todos os descendentes,
    /// <c>quantidadeMinima</c> por tipo de nó, consequência/base legal só em <c>GrupoOu</c>.
    /// </summary>
    /// <remarks>
    /// <b>Ciclo</b>: o wire (comando HTTP) é uma árvore <b>por valor</b> — estruturalmente não
    /// consegue expressar um ciclo (cada nó aninhado é uma instância nova na desserialização).
    /// A checagem por identidade de referência aqui é defesa em profundidade para CHAMADORES
    /// INTERNOS que eventualmente reusem a mesma instância de <see cref="NoExigencia"/> como
    /// filho em duas posições — não alcançável via HTTP, mas o invariante SHALL da spec é de
    /// domínio, não só de contrato.
    /// </remarks>
    public static Result<NoExigencia> CriarGrupo(
        TipoNo tipo,
        int ordem,
        int? quantidadeMinima,
        string? consequencia,
        IReadOnlyList<NoExigenciaBaseLegal> basesLegais,
        IReadOnlyList<NoExigencia> filhos,
        TipoEntidade? repetePorEntidade = null)
    {
        ArgumentNullException.ThrowIfNull(basesLegais);
        ArgumentNullException.ThrowIfNull(filhos);

        if (tipo is not (TipoNo.GrupoE or TipoNo.GrupoOu))
        {
            throw new ArgumentOutOfRangeException(
                nameof(tipo), tipo, "CriarGrupo só aceita GrupoE ou GrupoOu — use CriarFolha para folha.");
        }

        if (ordem < 0)
        {
            return Result<NoExigencia>.Failure(new DomainError(
                "NoExigencia.OrdemInvalida", "A ordem do nó não pode ser negativa."));
        }

        if (filhos.Count == 0)
        {
            return Result<NoExigencia>.Failure(new DomainError(
                "NoExigencia.GrupoVazio", "Um grupo E/OU não pode ter zero filhos."));
        }

        if (ContemCiclo(filhos))
        {
            return Result<NoExigencia>.Failure(new DomainError(
                "NoExigencia.ArvoreComCiclo", "A árvore de satisfação não pode conter ciclos — a estrutura precisa ser uma árvore."));
        }

        if (FaseComumDosFilhos(filhos) is null)
        {
            return Result<NoExigencia>.Failure(new DomainError(
                "NoExigencia.GrupoComFasesDiferentes", "Todos os nós de um grupo precisam pertencer à mesma fase do cronograma."));
        }

        TipoEntidade tipoEntidadeNormalizado = repetePorEntidade ?? TipoEntidade.Nenhuma;
        if (ValidarCatalogoTipoEntidade(tipoEntidadeNormalizado) is { } erroTipoEntidade)
        {
            return Result<NoExigencia>.Failure(erroTipoEntidade);
        }

        // Story #922: repetição não aninha — construção é bottom-up (folhas/grupos filhos já
        // existem prontos aqui), então checar os descendentes JÁ CONSTRUÍDOS de `filhos` é
        // suficiente para pegar qualquer combinação de profundidade (o nó marcado mais próximo
        // da raiz é sempre construído por último).
        if (tipoEntidadeNormalizado != TipoEntidade.Nenhuma && filhos.Any(ContemRepeticaoDeEntidade))
        {
            return Result<NoExigencia>.Failure(new DomainError(
                "NoExigencia.RepeticaoDeEntidadeAninhada",
                "Uma subárvore repetePorEntidade não pode conter outra — repetição não aninha."));
        }

        int? quantidadeMinimaFinal = null;
        string? consequenciaFinal = null;

        if (tipo == TipoNo.GrupoE)
        {
            if (quantidadeMinima is not null)
            {
                return Result<NoExigencia>.Failure(new DomainError(
                    "NoExigencia.QuantidadeMinimaProibidaEmGrupoE", "Um grupo E não tem cardinalidade própria — quantidadeMinima é proibida."));
            }

            if (!string.IsNullOrWhiteSpace(consequencia))
            {
                return Result<NoExigencia>.Failure(new DomainError(
                    "NoExigencia.ConsequenciaProibidaEmGrupoE", "Um grupo E é transparente e não carrega consequência própria."));
            }

            if (basesLegais.Count > 0)
            {
                return Result<NoExigencia>.Failure(new DomainError(
                    "NoExigencia.BaseLegalProibidaEmGrupoE", "Um grupo E é transparente e não carrega base legal própria."));
            }
        }
        else
        {
            int n = quantidadeMinima ?? QuantidadeMinimaPadrao;
            if (n < 1 || n > filhos.Count)
            {
                return Result<NoExigencia>.Failure(new DomainError(
                    "NoExigencia.QuantidadeMinimaForaDosLimites",
                    $"quantidadeMinima de um grupo OU/N-de deve estar entre 1 e o número de filhos ({filhos.Count}) — recebido {n}."));
            }

            string? consequenciaNormalizada = string.IsNullOrWhiteSpace(consequencia) ? null : consequencia.Trim();
            if (consequenciaNormalizada is not null
                && !ConsequenciasValidas.Contains(consequenciaNormalizada, StringComparer.Ordinal))
            {
                return Result<NoExigencia>.Failure(new DomainError(
                    "NoExigencia.ConsequenciaInvalida",
                    $"Consequência '{consequenciaNormalizada}' inválida — esperado um de: {string.Join(", ", ConsequenciasValidas)}."));
            }

            if (basesLegais.Count > 0 && consequenciaNormalizada is null)
            {
                return Result<NoExigencia>.Failure(new DomainError(
                    "NoExigencia.BaseLegalSemConsequencia", "Base legal de grupo só é permitida quando o grupo carrega consequência."));
            }

            quantidadeMinimaFinal = n;
            consequenciaFinal = consequenciaNormalizada;
        }

        NoExigencia no = new()
        {
            Tipo = tipo,
            Ordem = ordem,
            QuantidadeMinima = quantidadeMinimaFinal,
            Consequencia = consequenciaFinal,
            RepetePorEntidade = tipoEntidadeNormalizado == TipoEntidade.Nenhuma ? null : tipoEntidadeNormalizado,
        };

        foreach (NoExigencia filho in filhos)
        {
            filho.VincularPai(no.Id);
            no._filhos.Add(filho);
        }

        foreach (NoExigenciaBaseLegal baseLegal in basesLegais)
        {
            baseLegal.VincularNoExigencia(no.Id);
            no._basesLegais.Add(baseLegal);
        }

        return Result<NoExigencia>.Success(no);
    }

    /// <summary>
    /// Story #923 — reconstrói o modelo achatado pré-Story #920 (uma raiz-folha por
    /// <see cref="DocumentoExigido"/>, sem grupo, cardinalidade padrão) a partir de uma
    /// versão congelada anterior à 1.4, cujo envelope nunca serializou a topologia da
    /// árvore. Usado só na reidratação (<see cref="Infrastructure"/>, fora deste projeto) —
    /// nunca no cadastro, onde a árvore vem sempre explícita do comando.
    /// </summary>
    /// <remarks>
    /// A raiz sintetizada usa <c>Reidratar</c>, não <c>CriarFolha</c>, para que o Id do nó
    /// seja o próprio <see cref="DocumentoExigido.Id"/> — determinístico e derivado do
    /// conteúdo congelado — em vez do Guid v7 aleatório que <c>CriarFolha</c> gera a cada
    /// chamada. Sem isso, cada descarte/restauração dessa mesma versão pré-1.4 produziria
    /// Ids de nó diferentes, e uma republicação sob a 1.4 bateria o acaso da última
    /// restauração no hash canônico, não o conteúdo da configuração.
    /// </remarks>
    public static IReadOnlyList<NoExigencia> SintetizarRaizesLegadas(IReadOnlyList<DocumentoExigido> documentosExigidos)
    {
        ArgumentNullException.ThrowIfNull(documentosExigidos);

        List<NoExigencia> raizes = new(documentosExigidos.Count);
        int ordem = 0;
        foreach (DocumentoExigido documento in documentosExigidos)
        {
            raizes.Add(Reidratar(
                documento.Id, TipoNo.Folha, ordem++, documento.Id, documento,
                QuantidadeMinimaPadrao, null, null, null, null, null, [], []));
        }

        return raizes;
    }

    /// <summary>Este nó, ou algum descendente (a qualquer profundidade), é <see cref="RepetePorEntidade"/> — usado para recusar aninhamento em <see cref="CriarGrupo"/>.</summary>
    private static bool ContemRepeticaoDeEntidade(NoExigencia no) =>
        no.RepetePorEntidade is not null || no._filhos.Any(ContemRepeticaoDeEntidade);

    /// <summary>Reidrata um nó a partir de uma <see cref="VersaoConfiguracao"/> congelada, preservando o Id (mesma razão de <see cref="DocumentoExigido.Reidratar"/>).</summary>
    public static NoExigencia Reidratar(
        Guid id,
        TipoNo tipo,
        int ordem,
        Guid? documentoExigidoId,
        DocumentoExigido? documentoExigido,
        int? quantidadeMinima,
        string? consequencia,
        ChaveDistincao? chaveDistincao,
        DateOnly? dataReferencia,
        IReadOnlyList<string>? ocorrenciasEsperadas,
        TipoEntidade? repetePorEntidade,
        IReadOnlyList<NoExigenciaBaseLegal> basesLegais,
        IReadOnlyList<NoExigencia> filhos)
    {
        ArgumentNullException.ThrowIfNull(basesLegais);
        ArgumentNullException.ThrowIfNull(filhos);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O nó reidratado deve declarar o Id congelado no envelope.", nameof(id));
        }

        NoExigencia no = new()
        {
            Id = id,
            Tipo = tipo,
            Ordem = ordem,
            DocumentoExigidoId = documentoExigidoId,
            DocumentoExigido = documentoExigido,
            QuantidadeMinima = quantidadeMinima,
            Consequencia = consequencia,
            ChaveDistincao = chaveDistincao,
            DataReferencia = dataReferencia,
            OcorrenciasEsperadas = ocorrenciasEsperadas,
            RepetePorEntidade = repetePorEntidade,
        };

        foreach (NoExigencia filho in filhos)
        {
            filho.VincularPai(no.Id);
            no._filhos.Add(filho);
        }

        foreach (NoExigenciaBaseLegal baseLegal in basesLegais)
        {
            baseLegal.VincularNoExigencia(no.Id);
            no._basesLegais.Add(baseLegal);
        }

        return no;
    }

    /// <summary>Vincula este nó (e toda a subárvore) ao processo — chamado na raiz por <see cref="ProcessoSeletivo.DefinirDocumentosExigidos"/>.</summary>
    internal void VincularProcesso(Guid processoSeletivoId)
    {
        ProcessoSeletivoId = processoSeletivoId;
        foreach (NoExigencia filho in _filhos)
        {
            filho.VincularProcesso(processoSeletivoId);
        }
    }

    internal void VincularPai(Guid noPaiId) => NoPaiId = noPaiId;

    /// <summary>
    /// Corrige a navegação de uma FOLHA para o <see cref="DocumentoExigido"/> final, após a
    /// reconciliação por Id em <see cref="ProcessoSeletivo.RestaurarConfiguracaoCongelada"/>
    /// (Story #923) escolher a instância VIVA (tracked) no lugar da congelada — mesmo
    /// espírito de <see cref="DocumentoExigido.RemapearFase"/>. <see cref="DocumentoExigidoId"/>
    /// não muda (é o mesmo Id nos dois lados); só a referência de objeto é substituída, para
    /// que <see cref="DocumentoExigido"/> nunca aponte para uma instância descartada pela
    /// reconciliação de <see cref="DocumentoExigido"/>.
    /// </summary>
    internal void RemapearDocumentoExigido(DocumentoExigido documentoExigido)
    {
        ArgumentNullException.ThrowIfNull(documentoExigido);
        DocumentoExigido = documentoExigido;
    }

    /// <summary>Achata esta subárvore (este nó + todos os descendentes) — usado para popular a coleção plana de EF do agregado.</summary>
    public IEnumerable<NoExigencia> AchatarComDescendentes()
    {
        yield return this;
        foreach (NoExigencia filho in _filhos)
        {
            foreach (NoExigencia descendente in filho.AchatarComDescendentes())
            {
                yield return descendente;
            }
        }
    }

    /// <summary>
    /// A fase do cronograma comum a TODA a subárvore (folha: a própria <see cref="DocumentoExigido.ExigidoNaFaseId"/>;
    /// grupo: a fase comum dos filhos) — <see langword="null"/> se os descendentes não convergem para uma única fase.
    /// </summary>
    public Guid? FaseComum() => Tipo == TipoNo.Folha
        ? DocumentoExigido?.ExigidoNaFaseId
        : FaseComumDosFilhos(_filhos);

    /// <summary>Determina se este nó "conta" para efeito de resultado — só <see cref="TipoNo.GrupoOu"/> com <see cref="Consequencia"/>; <see cref="TipoNo.GrupoE"/> nunca (transparente).</summary>
    public bool DeterminaResultado() => Tipo == TipoNo.GrupoOu && Consequencia is not null;

    /// <summary>Projeção "somente <see cref="StatusBaseLegal.Resolvido"/>" — mesma semântica de <see cref="DocumentoExigido.BasesLegaisResolvidas"/>.</summary>
    public IEnumerable<NoExigenciaBaseLegal> BasesLegaisResolvidas() =>
        _basesLegais.Where(static b => b.Status == StatusBaseLegal.Resolvido);

    /// <summary>
    /// A subárvore PODE alcançar candidatos desta modalidade — união (OR) entre as folhas
    /// descendentes, cada uma avaliada por <see cref="DocumentoExigido.PodeAlcancarModalidade"/>
    /// (mesma checagem estrutural do gate CA-05, estendida ao nó de grupo).
    /// </summary>
    public bool PodeAlcancarModalidade(string modalidadeCodigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modalidadeCodigo);

        return Tipo == TipoNo.Folha
            ? DocumentoExigido?.PodeAlcancarModalidade(modalidadeCodigo) ?? false
            : _filhos.Any(filho => filho.PodeAlcancarModalidade(modalidadeCodigo));
    }

    private static Guid? FaseComumDosFilhos(IReadOnlyList<NoExigencia> filhos)
    {
        HashSet<Guid> fases = [];
        foreach (Guid? fase in filhos.Select(static filho => filho.FaseComum()))
        {
            if (fase is null)
            {
                return null;
            }

            fases.Add(fase.Value);
        }

        return fases.Count == 1 ? fases.First() : null;
    }

    private static bool ContemCiclo(IReadOnlyList<NoExigencia> raizes)
    {
        HashSet<NoExigencia> visitados = new(ReferenceEqualityComparer.Instance);
        return raizes.Any(raiz => !Visitar(raiz, visitados));
    }

    private static bool Visitar(NoExigencia no, HashSet<NoExigencia> visitados) =>
        visitados.Add(no) && no._filhos.All(filho => Visitar(filho, visitados));
}
