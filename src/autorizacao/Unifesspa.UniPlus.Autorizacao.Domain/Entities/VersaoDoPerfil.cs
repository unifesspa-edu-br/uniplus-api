namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Versão de um perfil de acesso comum: nome, descrição e permissões que valem a partir do momento em que o documento
/// instituidor é verificado. Depois de criada não se edita; só muda de situação. Mudar o perfil é criar outra versão.
/// </summary>
public sealed class VersaoDoPerfil : EntityBase
{
    private readonly List<PermissaoDaVersao> _permissoes = [];
    private readonly List<DocumentoDaVersao> _documentos = [];

    public Guid PerfilId { get; private set; }

    /// <summary>Ordem da versão no perfil, a partir de 1.</summary>
    public int Numero { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public string Descricao { get; private set; } = string.Empty;

    /// <summary>Número do processo ou protocolo do documento que institui a versão.</summary>
    public string Protocolo { get; private set; } = string.Empty;

    public SituacaoDaVersao Situacao { get; private set; }

    public MotivoDaRecusaDaVersao? MotivoDaRecusa { get; private set; }

    public DateTimeOffset? RecusadaEm { get; private set; }

    public string CriadaPorEmissor { get; private set; } = string.Empty;

    public string CriadaPorSub { get; private set; } = string.Empty;

    public string? CriadaPorNome { get; private set; }

    public DateTimeOffset CriadaEm { get; private set; }

    public string? CanceladaPorEmissor { get; private set; }

    public string? CanceladaPorSub { get; private set; }

    public string? CanceladaPorNome { get; private set; }

    public DateTimeOffset? CanceladaEm { get; private set; }

    public IReadOnlyCollection<PermissaoDaVersao> Permissoes => _permissoes;

    public IReadOnlyList<DocumentoDaVersao> Documentos => _documentos;

    /// <summary>O documento vivo da versão; sem ele, o último que o verificador recusou.</summary>
    public DocumentoDaVersao? Documento =>
        _documentos.Find(static d => d.Situacao != SituacaoDoAnexo.Recusado)
        ?? _documentos.MaxBy(static d => d.RecusadoEm);

    public bool Pendente => Situacao == SituacaoDaVersao.Pendente;

    // Construtor de materialização do EF Core.
    private VersaoDoPerfil()
    {
    }

    internal static Result<VersaoDoPerfil> Criar(
        Guid perfilId, int numero, ConteudoDaVersao conteudo, Identidade autor, DateTimeOffset instante)
    {
        VersaoDoPerfil versao = Nova(perfilId, numero, conteudo);
        versao.RegistrarCriacao(autor, instante);
        Result<DocumentoDaVersao> documento = versao.NovoDocumento(conteudo.TamanhoDoDocumento);

        return documento.IsFailure
            ? Result<VersaoDoPerfil>.ValidationFailure(documento.Errors)
            : Result<VersaoDoPerfil>.Success(versao);
    }

    /// <summary>Códigos das permissões que a versão reúne, ordenados.</summary>
    public IReadOnlyList<string> Codigos() =>
        [.. _permissoes.Select(static p => p.Codigo).Order(StringComparer.Ordinal)];

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

    private static VersaoDoPerfil Nova(Guid perfilId, int numero, ConteudoDaVersao conteudo)
    {
        VersaoDoPerfil versao = new()
        {
            PerfilId = perfilId,
            Numero = numero,
            Nome = conteudo.Nome,
            Descricao = conteudo.Descricao,
            Protocolo = conteudo.Protocolo,
        };
        versao._permissoes.AddRange(conteudo.Permissoes.Select(static p => new PermissaoDaVersao(p.Codigo, p.Classificacao, p.DeUnidade)));

        return versao;
    }

    private void RegistrarCriacao(Identidade autor, DateTimeOffset instante)
    {
        Situacao = SituacaoDaVersao.Pendente;
        CriadaPorEmissor = autor.Emissor;
        CriadaPorSub = autor.Sub;
        CriadaPorNome = autor.Nome;
        CriadaEm = instante;
    }

    internal Result Cancelar(Identidade autor, DateTimeOffset instante)
    {
        if (!Pendente)
        {
            return Result.Failure(PerfilErros.VersaoNaoPendente());
        }

        Situacao = SituacaoDaVersao.Cancelada;
        CanceladaPorEmissor = autor.Emissor;
        CanceladaPorSub = autor.Sub;
        CanceladaPorNome = autor.Nome;
        CanceladaEm = instante;
        return Result.Success();
    }

    internal void Recusar(MotivoDaRecusaDaVersao motivo, DateTimeOffset instante)
    {
        Situacao = SituacaoDaVersao.Recusada;
        MotivoDaRecusa = motivo;
        RecusadaEm = instante;
    }

    internal void EntrarEmVigor() => Situacao = SituacaoDaVersao.Vigente;

    internal void Encerrar() => Situacao = SituacaoDaVersao.Encerrada;
}
