namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>
/// O que a janela não vê sozinha e quem opera sob as travas do perfil lê do banco: o instante, as concessões do perfil
/// que a janela alcança, o que cada uma dá hoje, a composição da versão-alvo e quem tem o perfil da proteção de dados.
/// </summary>
public sealed class ContextoDaMigracao
{
    private readonly Dictionary<Guid, Concessao> _porId;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<string>> _composicoes;
    private readonly IReadOnlyList<string> _composicaoDoAlvo;
    private readonly IReadOnlyCollection<Identidade> _comProtecaoDeDados;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;
    private readonly IReadOnlyList<VersaoDoPerfil> _versoes;

    /// <param name="agora">O instante lido depois das travas.</param>
    /// <param name="concessoes">As concessões do perfil que a janela alcança, travadas e rastreadas.</param>
    /// <param name="composicoes">Os códigos que cada concessão dá hoje, pela regra única da decisão de acesso.</param>
    /// <param name="composicaoDoAlvo">Os códigos da versão-alvo, ordenados.</param>
    /// <param name="comProtecaoDeDados">Quem tem concessão ativa do perfil da proteção de dados.</param>
    /// <param name="catalogo">O catálogo de permissões.</param>
    /// <param name="versoes">As versões do perfil, de onde a migração tira o retrato da que sai e o da que entra.</param>
    public ContextoDaMigracao(
        DateTimeOffset agora,
        IReadOnlyList<Concessao> concessoes,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> composicoes,
        IReadOnlyList<string> composicaoDoAlvo,
        IReadOnlyCollection<Identidade> comProtecaoDeDados,
        ICatalogoDePermissoesConsultavel catalogo,
        IReadOnlyList<VersaoDoPerfil> versoes)
    {
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(composicoes);
        ArgumentNullException.ThrowIfNull(composicaoDoAlvo);
        ArgumentNullException.ThrowIfNull(comProtecaoDeDados);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(versoes);

        Agora = agora;
        Concessoes = concessoes;
        _porId = concessoes.ToDictionary(static c => c.Id);
        _composicoes = composicoes;
        _composicaoDoAlvo = composicaoDoAlvo;
        _comProtecaoDeDados = comProtecaoDeDados;
        _catalogo = catalogo;
        _versoes = versoes;
    }

    public DateTimeOffset Agora { get; }

    public IReadOnlyList<Concessao> Concessoes { get; }

    /// <summary>A versão do perfil com o id.</summary>
    public VersaoDoPerfil Versao(Guid versaoId) => _versoes.First(v => v.Id == versaoId);

    public Concessao? Concessao(Guid concessaoId) => _porId.GetValueOrDefault(concessaoId);

    /// <summary>O que a migração avalia para a concessão, pela composição que ela dá hoje.</summary>
    public ComposicaoDoLote ComposicaoDe(Concessao concessao)
    {
        ArgumentNullException.ThrowIfNull(concessao);

        return ComposicaoDoLote.Calcular(_composicaoDoAlvo, _composicoes.GetValueOrDefault(concessao.Id) ?? [], _catalogo);
    }

    /// <summary>A pessoa tem hoje concessão do perfil da proteção de dados: a migração dela dispensa a segunda etapa.</summary>
    public bool TemProtecaoDeDados(Identidade pessoa) => _comProtecaoDeDados.Contains(pessoa);

    /// <summary>A pessoa tem concessão ativa do perfil em migração, que a trilha registra de quem decide.</summary>
    public bool TemConcessaoDoPerfil(Identidade pessoa) =>
        Concessoes.Any(c => c.Beneficiario == pessoa && c.EstaAtiva(Agora));
}
