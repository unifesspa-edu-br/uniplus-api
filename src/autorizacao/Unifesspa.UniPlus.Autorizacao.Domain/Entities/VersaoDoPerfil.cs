namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Versão de um perfil de acesso comum: nome, descrição e permissões que valem a partir do momento em que a aprovação
/// se completa, ou na hora, quando nasce de uma remoção. Depois de criada não se edita; só muda de situação. Mudar o
/// perfil é criar outra versão.
/// </summary>
public sealed partial class VersaoDoPerfil : EntityBase
{
    /// <summary>Quanto a versão proposta espera a aprovação, em dias corridos, contados da criação.</summary>
    public const int PrazoDaAprovacaoEmDias = 15;

    private readonly List<PermissaoDaVersao> _permissoes = [];
    private readonly List<DocumentoDaVersao> _documentos = [];
    private readonly List<DecisaoDaVersao> _decisoes = [];
    private readonly List<RecalculoDaAprovacao> _recalculos = [];

    public Guid PerfilId { get; private set; }

    /// <summary>Ordem da versão no perfil, a partir de 1.</summary>
    public int Numero { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public string Descricao { get; private set; } = string.Empty;

    /// <summary>Número do processo ou protocolo do documento que institui a versão.</summary>
    public string Protocolo { get; private set; } = string.Empty;

    public SituacaoDaVersao Situacao { get; private set; }

    public OrigemDaVersao Origem { get; private set; }

    /// <summary>Unidade ou instituição, como o catálogo dava às permissões da proposta na criação; a remoção o copia. Não muda.</summary>
    public TipoDeEscopo Alcance { get; private set; }

    /// <summary>O fim do prazo da aprovação; nulo na remoção, que não espera nada. Não muda na troca de etapa.</summary>
    public DateTimeOffset? ExpiraEm { get; private set; }

    /// <summary>A classe da mudança na última conferência: o acréscimo exige a etapa da proteção de dados.</summary>
    public bool ExigeProtecaoDeDados { get; private set; }

    /// <summary>Sobe quando a mudança de classe descarta as aprovações; começa em 1.</summary>
    public int CicloDeAprovacao { get; private set; } = 1;

    public bool CriadorTemConcessao { get; private set; }

    public string CriadaPorEmissor { get; private set; } = string.Empty;

    public string CriadaPorSub { get; private set; } = string.Empty;

    public string? CriadaPorNome { get; private set; }

    public string? CriadaPorEmail { get; private set; }

    public DateTimeOffset CriadaEm { get; private set; }

    public Identidade CriadaPor => new(CriadaPorEmissor, CriadaPorSub, CriadaPorNome);

    public string? MotivoDaRemocao { get; private set; }

    public CategoriaDaRemocao? CategoriaDaRemocao { get; private set; }

    /// <summary>
    /// Na remoção, a versão proposta que tem o documento instituidor, nunca outra remoção; nula na proposta, que tem o
    /// seu.
    /// </summary>
    public Guid? VersaoDoDocumento { get; private set; }

    public IReadOnlyCollection<PermissaoDaVersao> Permissoes => _permissoes;

    public IReadOnlyList<DocumentoDaVersao> Documentos => _documentos;

    public IReadOnlyList<DecisaoDaVersao> Decisoes => _decisoes;

    public IReadOnlyList<RecalculoDaAprovacao> Recalculos => _recalculos;

    /// <summary>O documento vivo da versão; sem ele, o último que o verificador recusou.</summary>
    public DocumentoDaVersao? Documento =>
        _documentos.Find(static d => d.Situacao != SituacaoDoAnexo.Recusado)
        ?? _documentos.MaxBy(static d => d.RecusadoEm);

    public bool Pendente => Situacao == SituacaoDaVersao.Pendente;

    public bool Expirada => Situacao == SituacaoDaVersao.Expirada;

    // Construtor de materialização do EF Core.
    private VersaoDoPerfil()
    {
    }

    internal static Result<VersaoDoPerfil> Criar(
        Guid perfilId, int numero, ConteudoDaVersao conteudo, AutoriaDaVersao autoria, ClasseDaVersao classe, DateTimeOffset instante)
    {
        VersaoDoPerfil versao = Nova(perfilId, numero, conteudo, classe);
        versao.RegistrarCriacao(autoria, instante);
        versao.ExpiraEm = instante.AddDays(PrazoDaAprovacaoEmDias);
        Result<DocumentoDaVersao> documento = versao.NovoDocumento(conteudo.TamanhoDoDocumento);

        return documento.IsFailure
            ? Result<VersaoDoPerfil>.ValidationFailure(documento.Errors)
            : Result<VersaoDoPerfil>.Success(versao);
    }

    /// <summary>
    /// A versão que a remoção cria: a vigente menos as permissões retiradas, com o mesmo nome, descrição, alcance e
    /// protocolo, e o documento da versão proposta que o instituiu.
    /// </summary>
    internal static VersaoDoPerfil DaRemocao(VersaoDoPerfil vigente, int numero, PedidoDeRemocao pedido)
    {
        VersaoDoPerfil versao = CopiaDe(vigente, numero);
        versao.Origem = OrigemDaVersao.Remocao;
        versao.MotivoDaRemocao = pedido.Motivo;
        versao.CategoriaDaRemocao = pedido.Categoria;
        versao.VersaoDoDocumento = vigente.VersaoDoDocumento ?? vigente.Id;
        versao._permissoes.AddRange(vigente.PermissoesSem(pedido.Codigos));
        versao.RegistrarCriacao(pedido.Autoria, pedido.Instante);

        return versao;
    }

    /// <summary>Códigos das permissões que a versão reúne, ordenados.</summary>
    public IReadOnlyList<string> Codigos() =>
        [.. _permissoes.Select(static p => p.Codigo).Order(StringComparer.Ordinal)];

    /// <summary>Códigos que a versão reúne e <paramref name="anterior"/> não reunia, ordenados.</summary>
    public IReadOnlyList<string> AcrescimosSobre(VersaoDoPerfil anterior)
    {
        ArgumentNullException.ThrowIfNull(anterior);

        return [.. Codigos().Except(anterior.Codigos(), StringComparer.Ordinal)];
    }

    /// <summary>Novo documento instituidor; vale só enquanto o vivo foi recusado ou a versão ainda não tem documento.</summary>
    internal Result<DocumentoDaVersao> NovoDocumento(long tamanhoDeclarado)
    {
        Result<DocumentoDaVersao> criado = DocumentoDaVersao.Criar(PerfilId, Id, tamanhoDeclarado);
        if (criado.IsSuccess)
        {
            _documentos.Add(criado.Value!);
        }

        return criado;
    }

    private static VersaoDoPerfil CopiaDe(VersaoDoPerfil vigente, int numero) =>
        new()
        {
            PerfilId = vigente.PerfilId,
            Numero = numero,
            Nome = vigente.Nome,
            Descricao = vigente.Descricao,
            Protocolo = vigente.Protocolo,
            Alcance = vigente.Alcance,
        };

    private IEnumerable<PermissaoDaVersao> PermissoesSem(IReadOnlyCollection<string> codigos) =>
        _permissoes
            .Where(p => !codigos.Contains(p.Codigo, StringComparer.Ordinal))
            .Select(static p => new PermissaoDaVersao(p.Codigo, p.Classificacao, p.DeUnidade));

    private static VersaoDoPerfil Nova(Guid perfilId, int numero, ConteudoDaVersao conteudo, ClasseDaVersao classe)
    {
        VersaoDoPerfil versao = new()
        {
            PerfilId = perfilId,
            Numero = numero,
            Nome = conteudo.Nome,
            Descricao = conteudo.Descricao,
            Protocolo = conteudo.Protocolo,
            Origem = OrigemDaVersao.Proposta,
            Alcance = classe.Alcance,
            ExigeProtecaoDeDados = classe.ExigeProtecaoDeDados,
        };
        versao._permissoes.AddRange(conteudo.Permissoes.Select(static p => new PermissaoDaVersao(p.Codigo, p.Classificacao, p.DeUnidade)));

        return versao;
    }

    private void RegistrarCriacao(AutoriaDaVersao autoria, DateTimeOffset instante)
    {
        Situacao = SituacaoDaVersao.Pendente;
        CriadaPorEmissor = autoria.Pessoa.Emissor;
        CriadaPorSub = autoria.Pessoa.Sub;
        CriadaPorNome = autoria.Pessoa.Nome;
        CriadaPorEmail = autoria.Email;
        CriadorTemConcessao = autoria.TemConcessao;
        CriadaEm = instante;
    }
}
