namespace Unifesspa.UniPlus.Autorizacao.Apoio;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Monta, pela fábrica do domínio, a concessão que o teste precisa como pré-condição. Sem ajuste, concede o perfil a
/// Leandro Viana, sem e-mail, no escopo que o alcance do perfil admite (uma unidade nova quando o perfil é de unidade),
/// por um pedido, pelo gestor de acessos de <see cref="PerfisDeTeste"/>, no instante padrão dos testes e sem prazo. Fonte
/// única de montagem de concessão, compartilhada pelos projetos de teste; o teste da própria fábrica a chama direto.
/// </summary>
public sealed class ConcessaoBuilder
{
    private static readonly Identidade BeneficiarioPadrao = new("https://idp.test/realms/unifesspa", "leandro.viana", "Leandro Viana");

    private readonly Perfil _perfil;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;
    private TitularDaConcessao _titular = new(BeneficiarioPadrao, null);
    private EscopoDaConcessao? _escopo;
    private OrigemDaConcessao _origem = new(TipoDeOrigem.Pedido, Guid.NewGuid());
    private Identidade _concedente = PerfisDeTeste.Gestor;
    private DateTimeOffset _instante = PerfisDeTeste.Instante;
    private DateTimeOffset? _validaAte;

    private ConcessaoBuilder(Perfil perfil, ICatalogoDePermissoesConsultavel catalogo)
    {
        _perfil = perfil;
        _catalogo = catalogo;
    }

    /// <summary>Começa a concessão do perfil, avaliado contra o catálogo informado.</summary>
    public static ConcessaoBuilder DoPerfil(Perfil perfil, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(catalogo);

        return new ConcessaoBuilder(perfil, catalogo);
    }

    /// <summary>A pessoa a quem a concessão se dá e o e-mail que a revogação pede de volta.</summary>
    public ConcessaoBuilder Para(Identidade beneficiario, string? email = null)
    {
        _titular = new TitularDaConcessao(beneficiario, email);
        return this;
    }

    public ConcessaoBuilder NoEscopo(EscopoDaConcessao escopo)
    {
        _escopo = escopo;
        return this;
    }

    public ConcessaoBuilder NaUnidade(Guid unidadeId) => NoEscopo(EscopoDaConcessao.DaUnidade(unidadeId));

    public ConcessaoBuilder NaInstituicao() => NoEscopo(EscopoDaConcessao.DaInstituicao());

    public ConcessaoBuilder DaOrigem(OrigemDaConcessao origem)
    {
        _origem = origem;
        return this;
    }

    public ConcessaoBuilder ConcedidaPor(Identidade concedente)
    {
        _concedente = concedente;
        return this;
    }

    public ConcessaoBuilder Em(DateTimeOffset instante)
    {
        _instante = instante;
        return this;
    }

    public ConcessaoBuilder ValidaAte(DateTimeOffset? validaAte)
    {
        _validaAte = validaAte;
        return this;
    }

    /// <summary>A concessão montada; lança quando a fábrica a recusa, porque a pré-condição do teste não se cumpriu.</summary>
    public Concessao Construir() =>
        PerfisDeTeste.Garantir(Concessao.Conceder(
            _titular, _perfil, _escopo ?? EscopoDoAlcance(), _origem, new AtoDoAgente(_concedente, _instante), _catalogo, _validaAte));

    private EscopoDaConcessao EscopoDoAlcance() =>
        _perfil.Alcance(_catalogo) == TipoDeEscopo.Unidade
            ? EscopoDaConcessao.DaUnidade(Guid.NewGuid())
            : EscopoDaConcessao.DaInstituicao();
}
