namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// O repositório de reclassificações em memória: o catálogo gravado é o da instância, sem linhas, e o que se acrescenta
/// fica nas listas para o teste conferir.
/// </summary>
internal sealed class ReclassificacoesNaMemoria : IReclassificacaoRepository
{
    public ResumoSha256? Impressao { get; init; } = new Unifesspa.UniPlus.Authorization.Catalogo.CatalogoDaAplicacao([]).Impressao;

    public List<ReclassificacaoDaPermissao> Reclassificacoes { get; } = [];

    public List<LiberacaoDaReclassificacao> Liberacoes { get; } = [];

    /// <summary>As liberações e as bases adicionadas, na ordem em que o repositório as recebeu.</summary>
    public List<object> Adicionadas { get; } = [];

    public List<BaseDaPermissaoNaConcessao> Bases { get; } = [];

    public List<AvisoDaReclassificacao> Avisos { get; } = [];

    public List<DivergenciaDaGovernanca> Divergencias { get; } = [];

    public Dictionary<Guid, string> NomesDosPerfis { get; } = [];

    public List<ConcessaoComAPermissao> ConcessoesComAPermissao { get; } = [];

    public List<Guid> VersoesEmUso { get; } = [];

    public HashSet<Guid> VersoesComDocumentoLido { get; } = [];

    public Task<ResumoSha256?> ObterImpressaoAtualAsync(CancellationToken cancellationToken = default) => Task.FromResult(Impressao);

    public ImplantacaoNoBanco? ImplantacaoAtual { get; set; }

    public Task<ImplantacaoNoBanco?> ObterImplantacaoAtualAsync(IReadOnlyCollection<CodigoDePermissao> codigos, CancellationToken cancellationToken = default) =>
        Task.FromResult(ImplantacaoAtual);

    public Task<IReadOnlySet<CodigoDePermissao>> ObterCodigosReclassificadosAsync(Guid implantacaoId, Guid perfilId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<CodigoDePermissao>>(Reclassificacoes.Where(r => r.ImplantacaoId == implantacaoId && r.PerfilId == perfilId).Select(r => r.Codigo).ToHashSet());

    public void Adicionar(ReclassificacaoDaPermissao reclassificacao) => Reclassificacoes.Add(reclassificacao);

    public Task<Guid?> ObterPerfilIdAsync(Guid reclassificacaoId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Reclassificacoes.FirstOrDefault(r => r.Id == reclassificacaoId)?.PerfilId);

    public Task<ReclassificacaoDaPermissao?> ObterAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Reclassificacoes.FirstOrDefault(r => r.Id == id));

    public Task<IReadOnlyList<ReclassificacaoDaPermissao>> ListarVigentesQueSuspendemAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ReclassificacaoDaPermissao>>(Reclassificacoes);

    public Task<bool> EstaVigenteAsync(ReclassificacaoDaPermissao reclassificacao, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<IReadOnlyList<LiberacaoDaReclassificacao>> ListarLiberacoesAsync(Guid reclassificacaoId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LiberacaoDaReclassificacao>>([.. Liberacoes.Where(l => l.ReclassificacaoId == reclassificacaoId)]);

    public Task<IReadOnlyList<ConcessaoComAPermissao>> ListarConcessoesComAPermissaoAsync(
        Guid perfilId, CodigoDePermissao codigo, DateTimeOffset agora, bool bloquearParaLeituraCompartilhada, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ConcessaoComAPermissao>>([.. ConcessoesComAPermissao]);

    public Task<IReadOnlyList<Guid>> ListarVersoesEmUsoAsync(Guid perfilId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>([.. VersoesEmUso]);

    public Task<IReadOnlySet<Guid>> ListarVersoesComDocumentoLidoAsync(
        Identidade leitor, IReadOnlyCollection<VersaoDoPerfil> versoes, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<Guid>>(versoes.Select(v => v.Id).Where(VersoesComDocumentoLido.Contains).ToHashSet());

    public Task<IReadOnlyList<DivergenciaDaGovernanca>> ListarDivergenciasAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DivergenciaDaGovernanca>>(Divergencias);

    public Task<IReadOnlyDictionary<Guid, string>> ObterNomesDosPerfisAsync(IReadOnlyCollection<Guid> perfilIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(NomesDosPerfis);

    public Task<IReadOnlyList<FatoSemAviso>> ListarFatosSemAvisoAsync(int limite, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FatoSemAviso>>([
            .. Reclassificacoes.Where(r => (r.Subiu || r.MudouEscopo) && !Avisos.Any(a => a.Origem == OrigemDoAviso.Reclassificacao && a.OrigemId == r.Id)).Select(r => new FatoSemAviso(OrigemDoAviso.Reclassificacao, r.Id)),
            .. Divergencias.Where(d => !Avisos.Any(a => a.Origem == OrigemDoAviso.Divergencia && a.OrigemId == d.Id)).Select(d => new FatoSemAviso(OrigemDoAviso.Divergencia, d.Id)),
        ]);

    public Task<DivergenciaDaGovernanca?> ObterDivergenciaAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Divergencias.FirstOrDefault(d => d.Id == id));

    public void Adicionar(AvisoDaReclassificacao aviso) => Avisos.Add(aviso);

    public void Adicionar(LiberacaoDaReclassificacao liberacao)
    {
        Liberacoes.Add(liberacao);
        Adicionadas.Add(liberacao);
    }

    public void Adicionar(BaseDaPermissaoNaConcessao baseNova)
    {
        Bases.Add(baseNova);
        Adicionadas.Add(baseNova);
    }
}
