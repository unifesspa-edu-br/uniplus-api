namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Versão de um perfil de acesso comum: nome, descrição e permissões que valem a partir do momento em que a aprovação
/// se completa, ou na hora, quando nasce de uma remoção. Depois de criada não se edita; só muda de situação. Mudar o
/// perfil é criar outra versão.
/// </summary>
public sealed class VersaoDoPerfil : EntityBase
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

    public string CriadaPorContaId { get; private set; } = string.Empty;

    public string? CriadaPorNome { get; private set; }

    public string? CriadaPorEmail { get; private set; }

    public DateTimeOffset CriadaEm { get; private set; }

    public Identidade CriadaPor => new(CriadaPorEmissor, CriadaPorContaId, CriadaPorNome);

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
        versao.MotivoDaRemocao = pedido.Motivo?.Valor;
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
        CriadaPorContaId = autoria.Pessoa.ContaId;
        CriadaPorNome = autoria.Pessoa.Nome;
        CriadaPorEmail = autoria.Email;
        CriadorTemConcessao = autoria.TemConcessao;
        CriadaEm = instante;
    }

    public FormaDoCancelamento? FormaDoCancelamento { get; private set; }

    public string? MotivoDoCancelamento { get; private set; }

    public string? CanceladaPorEmissor { get; private set; }

    public string? CanceladaPorContaId { get; private set; }

    public string? CanceladaPorNome { get; private set; }

    public DateTimeOffset? CanceladaEm { get; private set; }

    public DateTimeOffset? ExpiradaEm { get; private set; }

    /// <summary>A gestão de acessos já aprovou a versão no ciclo atual.</summary>
    public bool AprovadaPelaGestao =>
        DecisaoDaEtapa(EtapaDaVersao.GestaoDeAcessos) is { Resultado: ResultadoDaDecisao.Aprovado };

    /// <summary>A decisão dada à etapa no ciclo atual; nula enquanto a etapa não foi decidida.</summary>
    public DecisaoDaVersao? DecisaoDaEtapa(EtapaDaVersao etapa) =>
        _decisoes.Find(d => d.Ciclo == CicloDeAprovacao && d.Etapa == etapa);

    /// <summary>As decisões do ciclo atual, na ordem das etapas: cada etapa decide uma vez por ciclo, e a gestão vem antes.</summary>
    public IReadOnlyList<DecisaoDaVersao> DecisoesDoCiclo() =>
        [.. _decisoes.Where(d => d.Ciclo == CicloDeAprovacao).OrderBy(static d => d.Etapa)];

    /// <summary>A pendente que passou do prazo: de <see cref="ExpiraEm"/> em diante a versão não aprova mais.</summary>
    public bool Expirou(DateTimeOffset agora) => Pendente && ExpiraEm <= agora;

    /// <summary>A recusa de operar uma versão que não está pendente: o prazo, se ela expirou; senão, a situação dela.</summary>
    internal DomainError RecusaPorNaoPendente() =>
        Expirada ? PerfilErros.VersaoExpirada() : PerfilErros.VersaoNaoPendente();

    internal void RegistrarDecisao(DecisaoDaVersao decisao) => _decisoes.Add(decisao);

    /// <summary>Descarta as aprovações do ciclo: a classe mudou, e o ciclo novo recomeça pela gestão de acessos.</summary>
    internal RecalculoDaAprovacao SubirCiclo(bool exigeAgora, DateTimeOffset instante)
    {
        RecalculoDaAprovacao recalculo = RecalculoDaAprovacao.Registrar(Id, CicloDeAprovacao, ExigeProtecaoDeDados, exigeAgora, instante);
        _recalculos.Add(recalculo);
        ExigeProtecaoDeDados = exigeAgora;
        CicloDeAprovacao++;

        return recalculo;
    }

    /// <summary>Retira a versão pendente. Quem chama confere que ela está pendente e, na forma pessoa, o motivo.</summary>
    internal void Cancelar(Identidade autor, string? motivo, FormaDoCancelamento forma, DateTimeOffset instante)
    {
        Situacao = SituacaoDaVersao.Cancelada;
        FormaDoCancelamento = forma;
        MotivoDoCancelamento = motivo;
        CanceladaPorEmissor = autor.Emissor;
        CanceladaPorContaId = autor.ContaId;
        CanceladaPorNome = autor.Nome;
        CanceladaEm = instante;
    }

    /// <summary>Materializa o fim do prazo, no instante em que o prazo terminou, e não no da operação que o notou.</summary>
    internal void Expirar()
    {
        Situacao = SituacaoDaVersao.Expirada;
        ExpiradaEm = ExpiraEm;
    }

    internal void Recusar() => Situacao = SituacaoDaVersao.Recusada;

    internal void EntrarEmVigor() => Situacao = SituacaoDaVersao.Vigente;

    internal void Encerrar() => Situacao = SituacaoDaVersao.Encerrada;
}
