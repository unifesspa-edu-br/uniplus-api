namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Monta, pela fábrica do domínio, a providência que o teste precisa como pré-condição. Sem ajuste, Cecília Moura,
/// Encarregada de dados com e-mail verificado, pede a retirada do acesso de Leandro Viana, cuja concessão está ativa e sem
/// prazo. Os testes da própria abertura chamam a fábrica direto.
/// </summary>
public sealed class ProvidenciaBuilder
{
    public const string Emissor = "https://idp.test/realms/unifesspa";
    public const string Fundamento = "O servidor deixou a banca do edital 12/2026 e mantém acesso a documentos sensíveis.";
    public const string Motivo = "O acesso é necessário até o fim do recurso do edital 12/2026.";

    public static readonly DateTimeOffset Agora = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
    public static readonly PessoaNoToken Cecilia = Pessoa("cecilia.moura", "Cecília Ester Aurora Moura", "cecilia_ester_moura@uniplus-test.local");
    public static readonly PessoaNoToken Leandro = Pessoa("leandro.viana", "Leandro Caleb Viana", "leandro-viana84@uniplus-test.local");
    public static readonly Identidade Diogo = new(Emissor, "diogo.souza", "Diogo Bruno Filipe Souza");

    private static readonly CatalogoMutavelDeTeste Catalogo = new();

    private Concessao? _concessao;
    private PessoaNoToken _solicitante = Cecilia;
    private SolicitacaoDaProvidencia _solicitacao = new("retirada", Fundamento);

    /// <summary>A concessão alvo; a padrão nasce na primeira leitura.</summary>
    public Concessao Concessao => _concessao ??= ConcessaoDeLeandro();

    public ProvidenciaBuilder SobreA(Concessao concessao)
    {
        _concessao = concessao;
        return this;
    }

    public ProvidenciaBuilder SolicitadaPor(PessoaNoToken solicitante)
    {
        _solicitante = solicitante;
        return this;
    }

    public ProvidenciaBuilder DoTipo(string tipo)
    {
        _solicitacao = _solicitacao with { Tipo = tipo };
        return this;
    }

    /// <summary>A providência aberta agora; lança quando a fábrica a recusa, porque a pré-condição do teste não se cumpriu.</summary>
    public Providencia Aberta() => PerfisDeTeste.Garantir(Providencia.Abrir(Concessao, null, _solicitacao, _solicitante, Agora));

    /// <summary>A providência aberta e já respondida por Diogo.</summary>
    public Providencia Respondida()
    {
        Providencia providencia = Aberta();
        Garantir(providencia.Responder(Concessao, AtoDeDiogo(), Motivo));
        return providencia;
    }

    /// <summary>A providência aberta e já atendida por Diogo, que revogou a concessão.</summary>
    public Providencia Atendida()
    {
        Providencia providencia = Aberta();
        Garantir(providencia.Atender(Concessao, AtoDeDiogo(), new DeclaracaoDoAtendimento(Motivo, Leandro.Email)));
        return providencia;
    }

    /// <summary>A concessão de Leandro, ativa desde trinta dias antes, com o e-mail dele e o fim da vigência informado.</summary>
    public static Concessao ConcessaoDeLeandro(DateTimeOffset? validaAte = null, string? email = null) =>
        ConcessaoBuilder.DoPerfil(Perfil(), Catalogo)
            .Para(Leandro.Identidade, email ?? Leandro.Email)
            .ConcedidaPor(Diogo)
            .Em(Agora.AddDays(-30))
            .ValidaAte(validaAte)
            .Construir();

    public static AtoDoAgente AtoDeDiogo(DateTimeOffset? instante = null) => new(Diogo, instante ?? Agora);

    public static PessoaNoToken Pessoa(string sub, string nome, string email, bool verificado = true) =>
        new(new Identidade(Emissor, sub, nome), nome, email, verificado);

    private static Perfil Perfil() =>
        PerfisDeTeste.Vigente("Banca de heteroidentificação", "Analisa os documentos da banca.", ["selecao:inscricoes:operar"], Catalogo);

    private static void Garantir(Kernel.Results.Result resultado)
    {
        if (resultado.IsFailure)
        {
            throw new InvalidOperationException(string.Join("; ", resultado.Errors.Select(e => e.Error.Code)));
        }
    }
}
