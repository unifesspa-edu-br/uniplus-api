namespace Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>O catálogo atual gravado no banco: o id, a impressão e as linhas dos códigos pedidos.</summary>
public sealed record ImplantacaoNoBanco(Guid Id, string Impressao, IReadOnlyDictionary<string, LinhaDoRetratoDoCatalogo> Linhas);

/// <summary>Uma concessão não revogada que reúne a permissão reclassificada pela composição, com a base dela.</summary>
public sealed record ConcessaoComAPermissao(Concessao Concessao, BaseDaConcessao Base);

/// <summary>Um fato da reclassificação que ainda não foi avisado: a origem e o identificador.</summary>
public sealed record FatoSemAviso(OrigemDoAviso Origem, Guid Id);

/// <summary>Repositório do catálogo gravado e das reclassificações das permissões.</summary>
public interface IReclassificacaoRepository
{
    /// <summary>A impressão do catálogo de maior sequência; nula quando nenhum catálogo foi gravado.</summary>
    Task<string?> ObterImpressaoAtualAsync(CancellationToken cancellationToken = default);

    /// <summary>O catálogo atual com as linhas dos <paramref name="codigos"/>; nulo quando nenhum catálogo foi gravado.</summary>
    Task<ImplantacaoNoBanco?> ObterImplantacaoAtualAsync(IReadOnlyCollection<string> codigos, CancellationToken cancellationToken = default);

    /// <summary>Os códigos do perfil que já têm reclassificação do catálogo.</summary>
    Task<IReadOnlySet<string>> ObterCodigosReclassificadosAsync(
        Guid implantacaoId, Guid perfilId, CancellationToken cancellationToken = default);

    /// <summary>Acrescenta a reclassificação à gravação seguinte.</summary>
    void Adicionar(ReclassificacaoDaPermissao reclassificacao);

    /// <summary>O perfil da reclassificação; nulo quando ela não existe.</summary>
    Task<Guid?> ObterPerfilIdAsync(Guid reclassificacaoId, CancellationToken cancellationToken = default);

    /// <summary>A reclassificação, sem rastreamento.</summary>
    Task<ReclassificacaoDaPermissao?> ObterAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>As reclassificações mais recentes de cada par (perfil, permissão) que sobem a classe ou mudam o escopo.</summary>
    Task<IReadOnlyList<ReclassificacaoDaPermissao>> ListarMaisRecentesQueSuspendemAsync(CancellationToken cancellationToken = default);

    /// <summary>É a mais recente do par (perfil, permissão).</summary>
    Task<bool> EhMaisRecenteAsync(ReclassificacaoDaPermissao reclassificacao, CancellationToken cancellationToken = default);

    /// <summary>As liberações já decididas na reclassificação.</summary>
    Task<IReadOnlyList<LiberacaoDaReclassificacao>> ListarLiberacoesAsync(Guid reclassificacaoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// As concessões não revogadas do perfil que reúnem a permissão pela composição, com a base de cada uma. Com
    /// <paramref name="travar"/>, trava as concessões para leitura em ordem de id, antes de lê-las.
    /// </summary>
    Task<IReadOnlyList<ConcessaoComAPermissao>> ListarConcessoesComAPermissaoAsync(
        Guid perfilId, string codigo, DateTimeOffset agora, bool travar, CancellationToken cancellationToken = default);

    /// <summary>As versões do perfil em uso: a vigente, as das concessões não revogadas e a de destino de janela aberta.</summary>
    Task<IReadOnlyList<Guid>> ListarVersoesEmUsoAsync(Guid perfilId, CancellationToken cancellationToken = default);

    /// <summary>Entre as versões, as cujo documento instituidor a pessoa leu.</summary>
    Task<IReadOnlySet<Guid>> ListarVersoesComDocumentoLidoAsync(
        Identidade leitor, IReadOnlyCollection<VersaoDoPerfil> versoes, CancellationToken cancellationToken = default);

    /// <summary>As divergências da governança, da mais recente para a mais antiga.</summary>
    Task<IReadOnlyList<DivergenciaDaGovernanca>> ListarDivergenciasAsync(CancellationToken cancellationToken = default);

    /// <summary>O nome de cada perfil pedido, pelo id.</summary>
    Task<IReadOnlyDictionary<Guid, string>> ObterNomesDosPerfisAsync(
        IReadOnlyCollection<Guid> perfilIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Os fatos que pedem aviso e ainda não têm linha em <c>avisos_da_reclassificacao</c>, do mais antigo ao mais novo: as
    /// reclassificações que sobem a classe ou mudam o escopo e as divergências da governança.
    /// </summary>
    Task<IReadOnlyList<FatoSemAviso>> ListarFatosSemAvisoAsync(int limite, CancellationToken cancellationToken = default);

    /// <summary>A divergência da governança, sem rastreamento.</summary>
    Task<DivergenciaDaGovernanca?> ObterDivergenciaAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Acrescenta o aviso à gravação seguinte.</summary>
    void Adicionar(AvisoDaReclassificacao aviso);

    /// <summary>Acrescenta a liberação à gravação seguinte.</summary>
    void Adicionar(LiberacaoDaReclassificacao liberacao);

    /// <summary>Acrescenta a base à gravação seguinte.</summary>
    void Adicionar(BaseDaPermissaoNaConcessao baseNova);
}
