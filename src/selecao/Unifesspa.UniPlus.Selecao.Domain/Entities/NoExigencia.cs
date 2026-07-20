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
/// <b>Cardinalidade nesta Story</b>: <see cref="QuantidadeMinima"/> só existe em
/// <see cref="TipoNo.GrupoOu"/> (quantos FILHOS satisfeitos) — a cardinalidade PRÓPRIA de uma
/// folha (contar múltiplas apresentações da MESMA exigência, <c>chaveDistincao</c>) é a PR
/// seguinte (§2, Story #921); nesta Story uma folha continua satisfeita por presença de UMA
/// apresentação, igual ao modelo anterior.
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

    /// <summary>Só <see cref="TipoNo.GrupoOu"/> — mínimo de filhos satisfeitos (default <see cref="QuantidadeMinimaPadrao"/>). <see langword="null"/> em folha/E.</summary>
    public int? QuantidadeMinima { get; private set; }

    /// <summary>
    /// Só <see cref="TipoNo.GrupoOu"/>, opcional — ∈ {ELIMINA, RECLASSIFICA_AC, REMOVE_VANTAGEM,
    /// PENDENCIA_REENVIO} (mesmo vocabulário fechado de <see cref="DocumentoExigido.ConsequenciaIndeferimento"/>).
    /// <see cref="TipoNo.GrupoE"/> é transparente e NUNCA carrega consequência própria.
    /// </summary>
    public string? Consequencia { get; private set; }

    private readonly List<NoExigencia> _filhos = [];

    /// <summary>Filhos ordenados por <see cref="Ordem"/> — vazio em folha, não-vazio em grupo (invariante SHALL: grupo não vazio).</summary>
    public IReadOnlyList<NoExigencia> Filhos => _filhos.AsReadOnly();

    private readonly List<NoExigenciaBaseLegal> _basesLegais = [];

    /// <summary>Base legal 1:N PRÓPRIA do grupo (só quando <see cref="Consequencia"/> presente) — não derivada dos filhos.</summary>
    public IReadOnlyCollection<NoExigenciaBaseLegal> BasesLegais => _basesLegais.AsReadOnly();

    private NoExigencia() { }

    /// <summary>Cria um nó folha — envolve <paramref name="documentoExigido"/> 1:1.</summary>
    public static Result<NoExigencia> CriarFolha(DocumentoExigido documentoExigido, int ordem)
    {
        ArgumentNullException.ThrowIfNull(documentoExigido);

        if (ordem < 0)
        {
            return Result<NoExigencia>.Failure(new DomainError(
                "NoExigencia.OrdemInvalida", "A ordem do nó não pode ser negativa."));
        }

        return Result<NoExigencia>.Success(new NoExigencia
        {
            Tipo = TipoNo.Folha,
            Ordem = ordem,
            DocumentoExigidoId = documentoExigido.Id,
            DocumentoExigido = documentoExigido,
        });
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
        IReadOnlyList<NoExigencia> filhos)
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

    /// <summary>Reidrata um nó a partir de uma <see cref="VersaoConfiguracao"/> congelada, preservando o Id (mesma razão de <see cref="DocumentoExigido.Reidratar"/>).</summary>
    public static NoExigencia Reidratar(
        Guid id,
        TipoNo tipo,
        int ordem,
        Guid? documentoExigidoId,
        DocumentoExigido? documentoExigido,
        int? quantidadeMinima,
        string? consequencia,
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
