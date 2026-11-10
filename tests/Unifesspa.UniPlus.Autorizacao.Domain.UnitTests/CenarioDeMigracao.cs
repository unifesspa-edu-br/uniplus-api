namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Um perfil com a versão 1 em vigor, as concessões dela e a versão 2 que a substitui, pronto para a janela de migração.
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
    private readonly HashSet<Identidade> _comProtecao = [];

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

    public JanelaDeMigracao? Janela { get; private set; }

    public bool Protegido { get; }

    public IReadOnlyList<Concessao> Concessoes => _concessoes;

    public Concessao Conceder(Identidade pessoa, Guid? unidade = null, DateTimeOffset? validaAte = null, string? email = null)
    {
        Concessao concessao = PerfisDeTeste.Garantir(Concessao.Conceder(
            pessoa,
            Perfil,
            EscopoDaConcessao.DaUnidade(unidade ?? Guid.NewGuid()),
            new OrigemDaConcessao(TipoDeOrigem.Pedido, Guid.NewGuid()),
            PerfisDeTeste.Autor,
            Abertura.AddHours(-1),
            _catalogo,
            validaAte,
            email));
        _concessoes.Add(concessao);

        return concessao;
    }

    public void TemProtecaoDeDados(Identidade pessoa) => _comProtecao.Add(pessoa);

    /// <summary>Põe a versão 2 em vigor e abre a janela no instante da abertura.</summary>
    public JanelaDeMigracao Abrir()
    {
        string acrescimo = Protegido ? CatalogoMutavelDeTeste.Dados : Analisar;
        VersaoNova = PerfisDeTeste.NovaVersaoVigente(Perfil, [Consultar, acrescimo], _catalogo).Troca!.Nova;
        Janela = JanelaDeMigracao.Abrir(Perfil.Id, VersaoNova, Abertura);

        return Janela;
    }

    public ContextoDaMigracao Contexto(DateTimeOffset agora)
    {
        string[] alvo = [.. VersaoNova!.Permissoes.Select(static p => p.Codigo).Order(StringComparer.Ordinal)];
        Dictionary<Guid, IReadOnlyList<string>> composicoes = _concessoes.ToDictionary(
            static c => c.Id, c => (IReadOnlyList<string>)(c.VersaoId == VersaoNova.Id ? alvo : [Consultar]));

        return new ContextoDaMigracao(agora, _concessoes, composicoes, alvo, _comProtecao, _catalogo, Perfil.Versoes);
    }

    public static AutoriaDaVersao Autoria(Identidade pessoa) => PerfisDeTeste.Autoria(pessoa);

    public static PedidoDoLote Lote(bool confirmou, params (Concessao Concessao, ResultadoDaMigracao Decisao)[] itens) =>
        new([.. itens.Select(static i => new ItemDoLote(i.Concessao.Id, i.Decisao))], confirmou);
}
