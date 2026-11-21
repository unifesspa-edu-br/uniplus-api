namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Nó da árvore de satisfação: uma folha envolve uma exigência documental; um grupo combina
/// seus filhos. A cardinalidade conta apresentações na folha e filhos satisfeitos no grupo OU.
/// O grupo E não carrega cardinalidade, consequência ou base legal própria.
/// </summary>
public sealed class NoExigencia : EntityBase, IParteDoProcesso
{
    public const int QuantidadeMinimaPadrao = 1;
    private const int QuantidadeMinimaDeFolhaMaxima = 366;
    private readonly List<NoExigencia> _filhos = [];
    private readonly List<BaseLegalDaComprovacao> _basesLegais = [];

    public Guid ProcessoSeletivoId { get; private set; }
    public Guid? NoPaiId { get; private set; }
    public int Ordem { get; private set; }
    public TipoNo Tipo { get; private set; }
    public Guid? DocumentoExigidoId { get; private set; }
    public DocumentoExigido? DocumentoExigido { get; private set; }
    public int? QuantidadeMinima { get; private set; }
    public string? Consequencia { get; private set; }
    public DistincaoDasApresentacoes? Distincao { get; private set; }

    /// <summary>Grupo do formulário cujas ocorrências repetem esta subárvore (ADR-0138).</summary>
    public string? RepetePorEntidade { get; private set; }
    public IReadOnlyList<NoExigencia> Filhos => _filhos.AsReadOnly();
    public IReadOnlyCollection<BaseLegalDaComprovacao> BasesLegais => _basesLegais.AsReadOnly();

    private NoExigencia() { }

    private NoExigencia(Guid? id)
    {
        if (id is { } congelado)
        {
            Id = congelado;
        }
    }

    public static Result<NoExigencia> CriarFolha(
        DocumentoExigido documentoExigido, int ordem, int? quantidadeMinima = null,
        ChaveDistincao? chaveDistincao = null, DateOnly? dataReferencia = null,
        IReadOnlyList<string>? ocorrenciasEsperadas = null, string? repetePorEntidade = null)
    {
        ArgumentNullException.ThrowIfNull(documentoExigido);
        int quantidade = quantidadeMinima ?? QuantidadeMinimaPadrao;
        DomainError? recusa = RecusaDaFolha(ordem, quantidade);
        if (recusa is not null)
        {
            return Result<NoExigencia>.Failure(recusa);
        }
        Result<DistincaoDasApresentacoes?> distincao = DistincaoDasApresentacoes.Declarar(chaveDistincao, dataReferencia, ocorrenciasEsperadas, quantidade);
        return distincao.IsFailure ? Result<NoExigencia>.Failure(distincao.Error!)
            : Result<NoExigencia>.Success(NovaFolha(documentoExigido, ordem, quantidade, distincao.Value, repetePorEntidade?.Trim()));
    }

    private static DomainError? RecusaDaFolha(int ordem, int quantidade)
    {
        if (ordem < 0)
        {
            return OrdemInvalida();
        }
        return RecusaDaQuantidadeDaFolha(quantidade);
    }

    internal static DomainError? RecusaDaQuantidadeDaFolha(int quantidade) =>
        quantidade < 1 || quantidade > QuantidadeMinimaDeFolhaMaxima
            ? new("NoExigencia.QuantidadeMinimaDeFolhaInvalida",
                $"A quantidade mínima de apresentações deste documento está fora dos limites. Informe um valor entre 1 e {QuantidadeMinimaDeFolhaMaxima}.") : null;

    private static NoExigencia NovaFolha(
        DocumentoExigido documento, int ordem, int? quantidadeMinima, DistincaoDasApresentacoes? distincao, string? repetePorEntidade) => new()
        {
            Tipo = TipoNo.Folha,
            Ordem = ordem,
            DocumentoExigidoId = documento.Id,
            DocumentoExigido = documento,
            QuantidadeMinima = quantidadeMinima,
            Distincao = distincao,
            RepetePorEntidade = repetePorEntidade,
        };

    public static Result<NoExigencia> CriarGrupo(
        TipoNo tipo, int ordem, int? quantidadeMinima, string? consequencia,
        IReadOnlyList<BaseLegalDaComprovacao> basesLegais, IReadOnlyList<NoExigencia> filhos, string? repetePorEntidade = null)
    {
        ArgumentNullException.ThrowIfNull(basesLegais);
        ArgumentNullException.ThrowIfNull(filhos);
        ConferirTipoDeGrupo(tipo);
        DomainError? recusa = ordem < 0 ? OrdemInvalida() : ComposicaoDoGrupo.Recusa(filhos, repetePorEntidade);
        if (recusa is not null)
        {
            return Result<NoExigencia>.Failure(recusa);
        }
        Result<CombinacaoDoGrupo> combinacao = CombinacaoDoGrupo.Declarar(tipo, quantidadeMinima, consequencia, filhos.Count, basesLegais.Count > 0);
        return combinacao.IsFailure ? Result<NoExigencia>.Failure(combinacao.Error!)
            : Result<NoExigencia>.Success(NovoGrupo(ordem, combinacao.Value!, basesLegais, filhos, repetePorEntidade?.Trim()));
    }

    /// <summary>Confere as relações entre os itens já montados de um grupo.</summary>
    public static IReadOnlyList<FieldError> ConferirComposicao(IReadOnlyList<NoExigencia> filhos, string? repetePorEntidade)
    {
        ArgumentNullException.ThrowIfNull(filhos);
        return ComposicaoDoGrupo.Recusas(filhos, repetePorEntidade);
    }

    private static void ConferirTipoDeGrupo(TipoNo tipo)
    {
        if (tipo is not (TipoNo.GrupoE or TipoNo.GrupoOu))
        {
            throw new ArgumentOutOfRangeException(nameof(tipo), "CriarGrupo só aceita GrupoE ou GrupoOu; use CriarFolha para folha.");
        }
    }

    private static DomainError OrdemInvalida() => new("NoExigencia.OrdemInvalida", "A ordem do nó não pode ser negativa.");

    private static NoExigencia NovoGrupo(
        int ordem, CombinacaoDoGrupo combinacao, IReadOnlyList<BaseLegalDaComprovacao> basesLegais,
        IReadOnlyList<NoExigencia> filhos, string? repetePorEntidade, Guid? id = null)
    {
        NoExigencia no = new(id)
        {
            Tipo = combinacao.Tipo,
            Ordem = ordem,
            QuantidadeMinima = combinacao.QuantidadeMinima,
            Consequencia = combinacao.Consequencia,
            RepetePorEntidade = repetePorEntidade,
        };
        no.VincularFilhosEBasesLegais(filhos, basesLegais);
        return no;
    }

    private void VincularFilhosEBasesLegais(IReadOnlyList<NoExigencia> filhos, IReadOnlyList<BaseLegalDaComprovacao> basesLegais)
    {
        foreach (NoExigencia filho in filhos)
        {
            filho.VincularPai(Id);
            _filhos.Add(filho);
        }
        foreach (BaseLegalDaComprovacao baseLegal in basesLegais)
        {
            baseLegal.VincularNoExigencia(Id);
            _basesLegais.Add(baseLegal);
        }
    }

    /// <summary>Repõe uma folha com identidade e parâmetros já conferidos na leitura do envelope.</summary>
    public static NoExigencia ReidratarFolha(
        Guid id, int ordem, DocumentoExigido documentoExigido, int? quantidadeMinima,
        DistincaoDasApresentacoes? distincao, string? repetePorEntidade)
    {
        ArgumentNullException.ThrowIfNull(documentoExigido);
        ConferirIdCongelado(id);
        return new()
        {
            Id = id,
            Tipo = TipoNo.Folha,
            Ordem = ordem,
            DocumentoExigidoId = documentoExigido.Id,
            DocumentoExigido = documentoExigido,
            QuantidadeMinima = quantidadeMinima,
            Distincao = distincao,
            RepetePorEntidade = repetePorEntidade
        };
    }

    /// <summary>Repõe um grupo com identidade e parâmetros já conferidos na leitura do envelope.</summary>
    public static NoExigencia ReidratarGrupo(
        Guid id, int ordem, CombinacaoDoGrupo combinacao, IReadOnlyList<BaseLegalDaComprovacao> basesLegais,
        IReadOnlyList<NoExigencia> filhos, string? repetePorEntidade)
    {
        ArgumentNullException.ThrowIfNull(combinacao);
        ArgumentNullException.ThrowIfNull(basesLegais);
        ArgumentNullException.ThrowIfNull(filhos);
        ConferirIdCongelado(id);
        return NovoGrupo(ordem, combinacao, basesLegais, filhos, repetePorEntidade, id);
    }

    private static void ConferirIdCongelado(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O nó reidratado deve declarar o Id congelado no envelope.", nameof(id));
        }
    }

    void IParteDoProcesso.VincularProcesso(Guid processoSeletivoId)
    {
        ProcessoSeletivoId = processoSeletivoId;
        foreach (NoExigencia filho in _filhos)
        {
            ((IParteDoProcesso)filho).VincularProcesso(processoSeletivoId);
        }
    }

    internal void VincularPai(Guid noPaiId) => NoPaiId = noPaiId;

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

    public Guid? FaseComum() => Tipo == TipoNo.Folha ? DocumentoExigido?.ExigidoNaFaseId : ComposicaoDoGrupo.FaseComumDosFilhos(_filhos);

    public bool TentarFinalidadeComum(out FinalidadeFormulario? finalidade)
    {
        if (Tipo == TipoNo.Folha)
        {
            finalidade = DocumentoExigido?.Finalidade;
            return true;
        }
        return ComposicaoDoGrupo.FinalidadeComumDosFilhos(_filhos, out finalidade);
    }

    public bool DeterminaResultado() => Tipo == TipoNo.GrupoOu && Consequencia is not null;

    public IEnumerable<BaseLegalDaComprovacao> BasesLegaisResolvidas() => _basesLegais.Where(static b => b.Status == StatusBaseLegal.Resolvido);

    public bool PodeAlcancarModalidade(string modalidadeCodigo, FatosDeModalidade fatosDeModalidade)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modalidadeCodigo);
        return Tipo == TipoNo.Folha ? DocumentoExigido?.PodeAlcancarModalidade(modalidadeCodigo, fatosDeModalidade) ?? false
            : _filhos.Any(filho => filho.PodeAlcancarModalidade(modalidadeCodigo, fatosDeModalidade));
    }
}
