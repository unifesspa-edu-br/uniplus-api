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
/// pedidos e versões novos.
/// </para>
/// <para>
/// <b>Perfis de governança</b> (<see cref="Governanca"/> preenchida) são de sistema: nascem da carga inicial, têm as
/// permissões próprias, sem versão, são os únicos que podem reunir permissões reservadas à governança e não se
/// alteram, reativam nem desativam por esta API.
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
    /// Só o perfil comum admite concessão com prazo: uma concessão de gestor que vencesse sozinha furaria o mínimo de
    /// dois gestores, que só a revogação confere, e a proteção de dados segue a mesma regra.
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
    public static bool AdmitePrazoCom(TipoDeGovernanca? governanca) => governanca is null;

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
        Result<VersaoDoPerfil> versao = VersaoDoPerfil.Criar(perfil.Id, 1, conteudo, autoria, ClasseDe(conteudo, null, catalogo), instante);
        if (versao.IsFailure)
        {
            return Result<Perfil>.ValidationFailure(versao.Errors);
        }

        perfil.Acrescentar(versao.Value!);

        return Result<Perfil>.Success(perfil);
    }

    /// <summary>
    /// Cria uma versão nova, pendente, com o documento esperando o envio. Sem versão vigente (a primeira foi cancelada,
    /// recusada ou expirou) é uma nova primeira versão: não há o que comparar e o nome pode mudar. A pendente que já
    /// passou do prazo expira antes e não bloqueia.
    /// </summary>
    public Result<VersaoDoPerfil> CriarVersao(
        ConteudoDaVersao conteudo,
        ContextoDaVersao contexto,
        AutoriaDaVersao autoria,
        DateTimeOffset instante,
        ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(catalogo);

        MaterializarExpiracao(instante);
        DomainError? recusa = RecusaDaCriacao(contexto);

        return recusa is null
            ? CriarEAcrescentar(conteudo, contexto, autoria, instante, catalogo)
            : Result<VersaoDoPerfil>.Failure(recusa);
    }

    /// <summary>Reativa o perfil. Reativar o que já está ativo não muda nada.</summary>
    public Result Reativar() => AlterarAtivo(true);

    /// <summary>Desativa o perfil. Desativar o que já está inativo não muda nada.</summary>
    public Result Desativar() => AlterarAtivo(false);

    /// <summary>
    /// O alcance em que o perfil se concede: unidade quando todas as permissões em vigor são de unidade, instituição
    /// quando nenhuma é. Nulo quando mistura os dois.
    /// </summary>
    public TipoDeEscopo? Alcance(ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        return AlcanceDasPermissoes.De(ComposicaoEmVigor, catalogo);
    }

    private Result AlterarAtivo(bool ativo)
    {
        if (Governanca is not null)
        {
            return Result.Failure(PerfilErros.GovernancaImutavel());
        }

        Ativo = ativo;

        return Result.Success();
    }

    private Result<VersaoDoPerfil> ObterVersao(Guid versaoId) =>
        _versoes.Find(v => v.Id == versaoId) is { } versao
            ? Result<VersaoDoPerfil>.Success(versao)
            : Result<VersaoDoPerfil>.Failure(PerfilErros.VersaoNaoEncontrada());

    private Result<VersaoDoPerfil> CriarEAcrescentar(
        ConteudoDaVersao conteudo,
        ContextoDaVersao contexto,
        AutoriaDaVersao autoria,
        DateTimeOffset instante,
        ICatalogoDePermissoesConsultavel catalogo)
    {
        ClasseDaVersao classe = ClasseDe(conteudo, VersaoVigente, catalogo);
        Result<VersaoDoPerfil> criada = VersaoDoPerfil.Criar(Id, ProximoNumero(), conteudo, autoria, classe, instante);
        DomainError? recusa = criada.IsSuccess ? RecusaContraAVigente(criada.Value!, contexto, catalogo) : null;
        if (recusa is not null)
        {
            return Result<VersaoDoPerfil>.Failure(recusa);
        }

        if (criada.IsSuccess)
        {
            Acrescentar(criada.Value!);
        }

        return criada;
    }

    private static ClasseDaVersao ClasseDe(
        ConteudoDaVersao conteudo, VersaoDoPerfil? vigente, ICatalogoDePermissoesConsultavel catalogo)
    {
        string[] codigos = [.. conteudo.Permissoes.Select(static p => p.Codigo).Order(StringComparer.Ordinal)];
        DiferencaDaVersao diferenca = DiferencaDaVersao.Calcular(codigos, vigente?.Codigos(), catalogo);

        return new ClasseDaVersao(AlcanceDasPermissoes.De(codigos, catalogo) ?? TipoDeEscopo.Instituicao, diferenca.ExigeProtecaoDeDados);
    }

    private int ProximoNumero() => _versoes.Max(static v => v.Numero) + 1;

    private DomainError? RecusaDaCriacao(ContextoDaVersao contexto)
    {
        if (Governanca is not null)
        {
            return PerfilErros.GovernancaImutavel();
        }

        if (!Ativo)
        {
            return PerfilErros.Inativo();
        }

        if (VersaoPendente is not null)
        {
            return PerfilErros.VersaoPendenteExistente();
        }

        return contexto.NomeEmUsoPorOutroPerfil ? PerfilErros.NomeDuplicado() : null;
    }

    private DomainError? RecusaContraAVigente(
        VersaoDoPerfil versao, ContextoDaVersao contexto, ICatalogoDePermissoesConsultavel catalogo) =>
        ConferenciasDaVersao.DaCriacao(
            versao, VersaoVigente, contexto, DiferencaDaVersao.Calcular(versao.Codigos(), VersaoVigente?.Codigos(), catalogo));

    // Sem versão vigente o perfil se identifica pela última pendente.
    private void Acrescentar(VersaoDoPerfil versao)
    {
        _versoes.Add(versao);

        if (VersaoVigente is null)
        {
            Nome = versao.Nome;
            Descricao = versao.Descricao;
        }
    }
}
