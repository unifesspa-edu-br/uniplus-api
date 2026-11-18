namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Um perfil com a versão 1 em vigor, as concessões dela e a versão 2 que a substitui, pronto para a revisão das concessões.
/// A versão 2 acrescenta uma permissão Interno ou, quando <c>protegido</c>, a de dados pessoais.
/// </summary>
internal sealed class CenarioDeMigracao
{
    public const string Consultar = "selecao:processos:consultar";
    public const string Analisar = "selecao:processos:analisar";

    public static readonly Identidade Leandro = new("https://idp.test/realms/unifesspa", "leandro.viana", "Leandro Viana");
    public static readonly Identidade Isabella = new("https://idp.test/realms/unifesspa", "isabella.figueiredo", "Isabella Figueiredo");
    public static readonly DateTimeOffset Abertura = PerfisDeTeste.Instante;

    private readonly CatalogoMutavelDeTeste _catalogo = new();
    private readonly List<Concessao> _concessoes = [];

    public CenarioDeMigracao(bool protegido)
    {
        _catalogo.DeUnidadeExtras.UnionWith([Consultar, Analisar, CatalogoMutavelDeTeste.Dados]);
        Perfil = PerfisDeTeste.Vigente("Acompanhamento", "Acompanha os processos.", [Consultar], _catalogo);
        VersaoAntiga = Perfil.VersaoVigente!;
        Protegido = protegido;
    }

    public Perfil Perfil { get; }

    public VersaoDoPerfil VersaoAntiga { get; }

    public VersaoDoPerfil? VersaoNova { get; private set; }

    public RevisaoDasConcessoesDoPerfil? Revisao { get; private set; }

    public bool Protegido { get; }

    public IReadOnlyList<Concessao> Concessoes => _concessoes;

    public Concessao Conceder(Identidade pessoa, Guid? unidade = null, DateTimeOffset? validaAte = null, string? email = null)
    {
        Concessao concessao = ConcessaoBuilder.DoPerfil(Perfil, _catalogo)
            .Para(pessoa, email)
            .NaUnidade(unidade ?? Guid.NewGuid())
            .ConcedidaPor(PerfisDeTeste.Autor)
            .Em(Abertura.AddHours(-1))
            .ValidaAte(validaAte)
            .Construir();
        _concessoes.Add(concessao);

        return concessao;
    }

    /// <summary>Põe a versão 2 em vigor e abre a revisão no instante da abertura.</summary>
    public RevisaoDasConcessoesDoPerfil Abrir()
    {
        string acrescimo = Protegido ? CatalogoMutavelDeTeste.Dados : Analisar;
        VersaoNova = PerfisDeTeste.NovaVersaoVigente(Perfil, [Consultar, acrescimo], _catalogo).Troca!.Nova;
        Revisao = RevisaoDasConcessoesDoPerfil.Abrir(Perfil.Id, VersaoNova, Abertura);

        return Revisao;
    }

    public ContextoDaRevisao Contexto(DateTimeOffset agora)
    {
        string[] alvo = [.. VersaoNova!.Permissoes.Select(static p => p.Codigo.Valor).Order(StringComparer.Ordinal)];
        Dictionary<Guid, IReadOnlyList<string>> composicoes = _concessoes.ToDictionary(
            static c => c.Id, c => (IReadOnlyList<string>)(c.VersaoId == VersaoNova.Id ? alvo : [Consultar]));

        return new ContextoDaRevisao(agora, _concessoes, composicoes, alvo, _catalogo, Perfil.Versoes);
    }

    public static AutoriaDaVersao Autoria(Identidade pessoa) => PerfisDeTeste.Autoria(pessoa);

    public static PedidoDoEnvio Envio(bool confirmou, params (Concessao Concessao, ResultadoDaMigracao Decisao)[] itens) =>
        new([.. itens.Select(static i => new ItemDoEnvio(i.Concessao.Id, i.Decisao))], confirmou);
}
