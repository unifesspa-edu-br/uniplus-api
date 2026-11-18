namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>
/// Dados lidos para a revisão com bloqueio do perfil: instante, concessões, permissões atuais e composição
/// da versão de destino.
/// </summary>
public sealed class ContextoDaRevisao
{
    private readonly Dictionary<Guid, Concessao> _porId;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<string>> _composicoes;
    private readonly IReadOnlyList<string> _composicaoDoAlvo;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;
    private readonly IReadOnlyList<VersaoDoPerfil> _versoes;

    /// <param name="agora">O instante lido após os bloqueios.</param>
    /// <param name="concessoes">As concessões do perfil abrangidas pela revisão, bloqueadas e rastreadas.</param>
    /// <param name="composicoes">Os códigos que cada concessão dá hoje, pela regra única da decisão de acesso.</param>
    /// <param name="composicaoDoAlvo">Os códigos da versão-alvo, ordenados.</param>
    /// <param name="catalogo">O catálogo de permissões.</param>
    /// <param name="versoes">As versões anterior e de destino do perfil.</param>
    public ContextoDaRevisao(
        DateTimeOffset agora,
        IReadOnlyList<Concessao> concessoes,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> composicoes,
        IReadOnlyList<string> composicaoDoAlvo,
        ICatalogoDePermissoesConsultavel catalogo,
        IReadOnlyList<VersaoDoPerfil> versoes)
    {
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(composicoes);
        ArgumentNullException.ThrowIfNull(composicaoDoAlvo);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(versoes);

        Agora = agora;
        Concessoes = concessoes;
        _porId = concessoes.ToDictionary(static c => c.Id);
        _composicoes = composicoes;
        _composicaoDoAlvo = composicaoDoAlvo;
        _catalogo = catalogo;
        _versoes = versoes;
    }

    public DateTimeOffset Agora { get; }

    public IReadOnlyList<Concessao> Concessoes { get; }

    /// <summary>A versão do perfil com o id.</summary>
    public VersaoDoPerfil Versao(Guid versaoId) => _versoes.First(v => v.Id == versaoId);

    public Concessao? Concessao(Guid concessaoId) => _porId.GetValueOrDefault(concessaoId);

    /// <summary>A composição avaliada para a concessão, conforme suas permissões atuais.</summary>
    public ComposicaoDoEnvio ComposicaoDe(Concessao concessao)
    {
        ArgumentNullException.ThrowIfNull(concessao);

        return ComposicaoDoEnvio.Calcular(_composicaoDoAlvo, _composicoes.GetValueOrDefault(concessao.Id) ?? [], _catalogo);
    }

    /// <summary>Informa se a pessoa tem concessão ativa do perfil em revisão.</summary>
    public bool TemConcessaoDoPerfil(Identidade pessoa) =>
        Concessoes.Any(c => c.Beneficiario == pessoa && c.EstaAtiva(Agora));
}
