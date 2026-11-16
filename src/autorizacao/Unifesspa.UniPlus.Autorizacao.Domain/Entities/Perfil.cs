namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Perfil de acesso: um nome e um conjunto de permissões do catálogo que a concessão atribui de uma vez a quem exerce
/// uma função.
/// </summary>
/// <remarks>
/// <para>
/// <b>O perfil comum é versionado.</b> Nome, descrição e permissões vêm da versão vigente, e uma versão proposta só
/// entra em vigor com a aprovação da gestão de acessos e, quando o acréscimo é protegido, da proteção de dados; a
/// remoção de permissões vale na hora. Versão não se edita: mudar o perfil é criar outra versão.
/// <see cref="Nome"/> e <see cref="Descricao"/> são a identificação corrente: os da versão vigente ou, enquanto não há
/// vigente, os da última pendente.
/// </para>
/// <para>
/// <b>A sensibilidade do perfil não é gravada.</b> Ela é a mais alta entre as permissões em vigor e se deriva do
/// catálogo a cada leitura.
/// </para>
/// <para>
/// <b>Perfil não se exclui, se desativa.</b> Desativar não revoga concessão alguma nem cancela versão; só impede
/// pedidos e versões novos. A contenção do perfil inteiro é a suspensão, que bloqueia todas as concessões na decisão de
/// acesso sem revogá-las.
/// </para>
/// <para>
/// <b>Perfis de governança</b> (<see cref="Governanca"/> preenchida) são de sistema: nascem da carga inicial, têm as
/// permissões próprias, sem versão, são os únicos que podem reunir permissões reservadas à governança e não se
/// alteram, suspendem, reativam nem desativam por esta API.
/// </para>
/// </remarks>
public sealed partial class Perfil : EntityBase
{
    private readonly List<PermissaoDoPerfil> _permissoes = [];
    private readonly List<VersaoDoPerfil> _versoes = [];
    private readonly List<VigenciaDoPerfil> _vigencias = [];

    public string Nome { get; private set; } = string.Empty;

    public string Descricao { get; private set; } = string.Empty;

    public bool Ativo { get; private set; } = true;

    /// <summary>Papel de governança do perfil de sistema; nulo nos perfis comuns.</summary>
    public TipoDeGovernanca? Governanca { get; private set; }

    /// <summary>
    /// Os perfis de gestor de acessos e de Representante do controlador não admitem concessão com prazo: a concessão que
    /// vencesse sozinha furaria o mínimo de dois gestores ou deixaria a instituição sem Representante vigente, e só a
    /// revogação confere esses mínimos. O Encarregado de dados pode ser designado por prazo, e a concessão vencida deixa de
    /// valer para toda regra.
    /// </summary>
    public bool AdmitePrazo => AdmitePrazoCom(Governanca);

    /// <summary>Permissões próprias do perfil de governança; o perfil comum as tem na versão.</summary>
    public IReadOnlyCollection<PermissaoDoPerfil> Permissoes => _permissoes;

    public IReadOnlyList<VersaoDoPerfil> Versoes => _versoes;

    public IReadOnlyList<VigenciaDoPerfil> Vigencias => _vigencias;

    public VersaoDoPerfil? VersaoVigente => _versoes.Find(static v => v.Situacao == SituacaoDaVersao.Vigente);

    public VersaoDoPerfil? VersaoPendente => _versoes.Find(static v => v.Pendente);

    /// <summary>A versão mais recente do perfil, quando terminou sem entrar em vigor: recusada, cancelada ou expirada.</summary>
    public VersaoDoPerfil? UltimaVersaoEncerradaSemVigor =>
        _versoes.MaxBy(static v => v.Numero) is { Situacao: SituacaoDaVersao.Recusada or SituacaoDaVersao.Cancelada or SituacaoDaVersao.Expirada } sem
            ? sem
            : null;

    /// <summary>
    /// A versão cujas permissões o perfil mostra: a vigente ou, enquanto o perfil ainda não tem vigente, a pendente.
    /// </summary>
    public VersaoDoPerfil? VersaoParaExibir => VersaoVigente ?? VersaoPendente;

    /// <summary>O perfil de governança dispensa a versão; o comum só vale com uma vigente.</summary>
    public bool TemComposicaoEmVigor => Governanca is not null || VersaoVigente is not null;

    /// <summary>
    /// Códigos das permissões em vigor, ordenados: as da versão vigente no perfil comum, as próprias no perfil de
    /// governança, nenhuma no perfil comum sem vigente. É a única leitura das permissões que decide acesso.
    /// </summary>
    public IReadOnlyList<string> ComposicaoEmVigor =>
        Governanca is not null
            ? [.. _permissoes.Select(static p => p.Codigo).Order(StringComparer.Ordinal)]
            : VersaoVigente?.Codigos() ?? [];

    /// <summary>A regra de <see cref="AdmitePrazo"/> a partir do papel de governança, para o retrato avaliado do perfil.</summary>
    public static bool AdmitePrazoCom(TipoDeGovernanca? governanca) =>
        governanca is not (TipoDeGovernanca.GestorDeAcessos or TipoDeGovernanca.RepresentanteDoControlador);

    // Construtor de materialização do EF Core.
    private Perfil()
    {
    }

    /// <summary>
    /// Cria um perfil comum, ativo e sem versão vigente, com a versão 1 pendente e o documento que a institui esperando
    /// o envio. A unicidade do nome é do handler, com proteção de corrida no índice único.
    /// </summary>
    public static Result<Perfil> Criar(
        ConteudoDaVersao conteudo, AutoriaDaVersao autoria, DateTimeOffset instante, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(catalogo);

        Perfil perfil = new();
        Result<VersaoDoPerfil> versao = VersaoDoPerfil.Criar(perfil.Id, 1, conteudo, autoria, ProposicaoDaVersao.ClasseDe(conteudo, null, catalogo), instante);
        if (versao.IsFailure)
        {
            return Result<Perfil>.ValidationFailure(versao.Errors);
        }

        perfil.Acrescentar(versao.Value!);

        return Result<Perfil>.Success(perfil);
    }

    /// <summary>
    /// O alcance em que o perfil se concede: unidade quando todas as permissões em vigor são de unidade, instituição
    /// quando nenhuma é. Nulo quando mistura os dois.
    /// </summary>
    public TipoDeEscopo? Alcance(ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        return AlcanceDasPermissoes.De(ComposicaoEmVigor, catalogo);
    }

    private Result<VersaoDoPerfil> ObterVersao(Guid versaoId) =>
        _versoes.Find(v => v.Id == versaoId) is { } versao
            ? Result<VersaoDoPerfil>.Success(versao)
            : Result<VersaoDoPerfil>.Failure(PerfilErros.VersaoNaoEncontrada());

    internal int ProximoNumero() => _versoes.Max(static v => v.Numero) + 1;

    // Sem versão vigente o perfil se identifica pela última pendente.
    internal void Acrescentar(VersaoDoPerfil versao)
    {
        _versoes.Add(versao);

        if (VersaoVigente is null)
        {
            Nome = versao.Nome;
            Descricao = versao.Descricao;
        }
    }
}
