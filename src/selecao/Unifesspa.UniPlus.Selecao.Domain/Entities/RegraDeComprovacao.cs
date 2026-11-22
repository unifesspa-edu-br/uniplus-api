namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Regra que define como uma exigência documental é comprovada.</summary>
public sealed class RegraDeComprovacao : EntityBase, IParteDoProcesso
{
    public const int QuantidadeMinimaPadrao = 1;
    private const int QuantidadeMaximaDeApresentacoes = 366;
    private readonly List<RegraDeComprovacao> _itens = [];
    private readonly List<BaseLegalDaComprovacao> _basesLegais = [];

    public Guid ProcessoSeletivoId { get; private set; }
    public Guid? RegraQueContemId { get; private set; }
    public int Ordem { get; private set; }
    public FormaDeComprovacao Forma { get; private set; }
    public Guid? DocumentoExigidoId { get; private set; }
    public DocumentoExigido? DocumentoExigido { get; private set; }
    public int? QuantidadeMinima { get; private set; }
    public string? Consequencia { get; private set; }
    public DistincaoDasApresentacoes? Distincao { get; private set; }

    /// <summary>Grupo do formulário a cujos registros a regra se aplica (ADR-0138).</summary>
    public string? GrupoDoFormularioParaRepeticao { get; private set; }
    public IReadOnlyList<RegraDeComprovacao> Itens => _itens.AsReadOnly();
    public IReadOnlyCollection<BaseLegalDaComprovacao> BasesLegais => _basesLegais.AsReadOnly();

    private RegraDeComprovacao() { }

    private RegraDeComprovacao(Guid? id)
    {
        if (id is { } congelado)
        {
            Id = congelado;
        }
    }

    public static Result<RegraDeComprovacao> CriarRegraDeDocumento(
        DocumentoExigido documentoExigido, int ordem, int? quantidadeMinima = null,
        CriterioDeDistincaoDasApresentacoes? criterioDeDistincao = null, DateOnly? dataReferencia = null,
        IReadOnlyList<string>? ocorrenciasEsperadas = null, string? grupoDoFormularioParaRepeticao = null)
    {
        ArgumentNullException.ThrowIfNull(documentoExigido);
        int quantidade = quantidadeMinima ?? QuantidadeMinimaPadrao;
        ArgumentOutOfRangeException.ThrowIfNegative(ordem);
        DomainError? recusa = RecusaDaQuantidadeDeApresentacoes(quantidade);
        Result<DistincaoDasApresentacoes?> distincao = DistincaoDasApresentacoes.Declarar(criterioDeDistincao, dataReferencia, ocorrenciasEsperadas, quantidade);
        List<FieldError> recusas = recusa is null ? [] : [new("quantidadeMinima", recusa)];
        recusas.AddRange(distincao.Errors);
        return recusas.Count > 0 ? Result<RegraDeComprovacao>.ValidationFailure(recusas)
            : Result<RegraDeComprovacao>.Success(NovaRegraDeDocumento(documentoExigido, ordem, quantidade, distincao.Value, grupoDoFormularioParaRepeticao?.Trim()));
    }

    internal static DomainError? RecusaDaQuantidadeDeApresentacoes(int quantidade) =>
        quantidade < 1 || quantidade > QuantidadeMaximaDeApresentacoes
            ? new("RegraDeComprovacao.QuantidadeMinimaDeApresentacoesInvalida",
                $"A quantidade mínima de apresentações deste documento está fora dos limites. Informe um valor entre 1 e {QuantidadeMaximaDeApresentacoes}.") : null;

    private static RegraDeComprovacao NovaRegraDeDocumento(
        DocumentoExigido documento, int ordem, int? quantidadeMinima, DistincaoDasApresentacoes? distincao, string? grupoDoFormularioParaRepeticao) => new()
        {
            Forma = FormaDeComprovacao.Documento,
            Ordem = ordem,
            DocumentoExigidoId = documento.Id,
            DocumentoExigido = documento,
            QuantidadeMinima = quantidadeMinima,
            Distincao = distincao,
            GrupoDoFormularioParaRepeticao = grupoDoFormularioParaRepeticao,
        };

    public static Result<RegraDeComprovacao> CriarGrupo(
        FormaDeComprovacao forma, int ordem, int? quantidadeMinima, string? consequencia,
        IReadOnlyList<BaseLegalDaComprovacao> basesLegais, IReadOnlyList<RegraDeComprovacao> itens, string? grupoDoFormularioParaRepeticao = null)
    {
        ArgumentNullException.ThrowIfNull(basesLegais);
        ArgumentNullException.ThrowIfNull(itens);
        ConferirFormaDoGrupo(forma);
        ArgumentOutOfRangeException.ThrowIfNegative(ordem);
        DomainError? recusa = ComposicaoDoGrupo.Recusa(itens, grupoDoFormularioParaRepeticao);
        if (recusa is not null)
        {
            return Result<RegraDeComprovacao>.Failure(recusa);
        }
        Result<CombinacaoDoGrupo> combinacao = CombinacaoDoGrupo.Declarar(forma, quantidadeMinima, consequencia, itens.Count, basesLegais.Count > 0);
        return combinacao.IsFailure ? Result<RegraDeComprovacao>.Failure(combinacao.Error!)
            : Result<RegraDeComprovacao>.Success(NovoGrupo(ordem, combinacao.Value!, basesLegais, itens, grupoDoFormularioParaRepeticao?.Trim()));
    }

    /// <summary>Confere as relações entre os itens já montados de um grupo.</summary>
    public static IReadOnlyList<FieldError> ConferirComposicao(IReadOnlyList<RegraDeComprovacao> itens, string? grupoDoFormularioParaRepeticao)
    {
        ArgumentNullException.ThrowIfNull(itens);
        return ComposicaoDoGrupo.Recusas(itens, grupoDoFormularioParaRepeticao);
    }

    private static void ConferirFormaDoGrupo(FormaDeComprovacao forma)
    {
        if (forma is not (FormaDeComprovacao.TodosOsItens or FormaDeComprovacao.QuantidadeMinimaDeItens))
        {
            throw new ArgumentOutOfRangeException(nameof(forma), "A forma do grupo deve exigir todos os itens ou uma quantidade mínima.");
        }
    }

    private static RegraDeComprovacao NovoGrupo(
        int ordem, CombinacaoDoGrupo combinacao, IReadOnlyList<BaseLegalDaComprovacao> basesLegais,
        IReadOnlyList<RegraDeComprovacao> itens, string? grupoDoFormularioParaRepeticao, Guid? id = null)
    {
        RegraDeComprovacao regra = new(id)
        {
            Forma = combinacao.Forma,
            Ordem = ordem,
            QuantidadeMinima = combinacao.QuantidadeMinima,
            Consequencia = combinacao.Consequencia,
            GrupoDoFormularioParaRepeticao = grupoDoFormularioParaRepeticao,
        };
        regra.VincularItensEBasesLegais(itens, basesLegais);
        return regra;
    }

    private void VincularItensEBasesLegais(IReadOnlyList<RegraDeComprovacao> itens, IReadOnlyList<BaseLegalDaComprovacao> basesLegais)
    {
        foreach (RegraDeComprovacao item in itens)
        {
            item.VincularPai(Id);
            _itens.Add(item);
        }
        foreach (BaseLegalDaComprovacao baseLegal in basesLegais)
        {
            baseLegal.VincularRegraDeComprovacao(Id);
            _basesLegais.Add(baseLegal);
        }
    }

    /// <summary>Restaura uma regra de documento com identidade e parâmetros conferidos no envelope.</summary>
    public static RegraDeComprovacao ReidratarRegraDeDocumento(
        Guid id, int ordem, DocumentoExigido documentoExigido, int? quantidadeMinima,
        DistincaoDasApresentacoes? distincao, string? grupoDoFormularioParaRepeticao)
    {
        ArgumentNullException.ThrowIfNull(documentoExigido);
        ConferirIdCongelado(id);
        return new()
        {
            Id = id,
            Forma = FormaDeComprovacao.Documento,
            Ordem = ordem,
            DocumentoExigidoId = documentoExigido.Id,
            DocumentoExigido = documentoExigido,
            QuantidadeMinima = quantidadeMinima,
            Distincao = distincao,
            GrupoDoFormularioParaRepeticao = grupoDoFormularioParaRepeticao
        };
    }

    /// <summary>Repõe um grupo com identidade e parâmetros já conferidos na leitura do envelope.</summary>
    public static RegraDeComprovacao ReidratarGrupo(
        Guid id, int ordem, CombinacaoDoGrupo combinacao, IReadOnlyList<BaseLegalDaComprovacao> basesLegais,
        IReadOnlyList<RegraDeComprovacao> itens, string? grupoDoFormularioParaRepeticao)
    {
        ArgumentNullException.ThrowIfNull(combinacao);
        ArgumentNullException.ThrowIfNull(basesLegais);
        ArgumentNullException.ThrowIfNull(itens);
        ConferirIdCongelado(id);
        return NovoGrupo(ordem, combinacao, basesLegais, itens, grupoDoFormularioParaRepeticao, id);
    }

    private static void ConferirIdCongelado(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A regra publicada deve ter um identificador.", nameof(id));
        }
    }

    void IParteDoProcesso.VincularProcesso(Guid processoSeletivoId)
    {
        ProcessoSeletivoId = processoSeletivoId;
        foreach (RegraDeComprovacao item in _itens)
        {
            ((IParteDoProcesso)item).VincularProcesso(processoSeletivoId);
        }
    }

    internal void VincularPai(Guid regraQueContemId) => RegraQueContemId = regraQueContemId;

    public IEnumerable<RegraDeComprovacao> AchatarComDescendentes()
    {
        yield return this;
        foreach (RegraDeComprovacao item in _itens)
        {
            foreach (RegraDeComprovacao descendente in item.AchatarComDescendentes())
            {
                yield return descendente;
            }
        }
    }

    public Guid? FaseComum() => Forma == FormaDeComprovacao.Documento ? DocumentoExigido?.ExigidoNaFaseId : ComposicaoDoGrupo.FaseComumDosItens(_itens);

    public bool TentarFinalidadeComum(out FinalidadeFormulario? finalidade)
    {
        if (Forma == FormaDeComprovacao.Documento)
        {
            finalidade = DocumentoExigido?.Finalidade;
            return true;
        }
        return ComposicaoDoGrupo.FinalidadeComumDosItens(_itens, out finalidade);
    }

    public bool DeterminaResultado() => Forma == FormaDeComprovacao.QuantidadeMinimaDeItens && Consequencia is not null;

    public IEnumerable<BaseLegalDaComprovacao> BasesLegaisResolvidas() => _basesLegais.Where(static b => b.Situacao == StatusBaseLegal.Resolvido);

    public bool PodeAlcancarModalidade(string modalidadeCodigo, FatosDeModalidade fatosDeModalidade)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modalidadeCodigo);
        return Forma == FormaDeComprovacao.Documento ? DocumentoExigido?.PodeAlcancarModalidade(modalidadeCodigo, fatosDeModalidade) ?? false
            : _itens.Any(item => item.PodeAlcancarModalidade(modalidadeCodigo, fatosDeModalidade));
    }
}
