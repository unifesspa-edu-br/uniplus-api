namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Autorizacao.Domain.UnitTests.PedidoDeConcessaoTestBase;

/// <summary>
/// Monta o pedido de concessão de um cenário: quem pede, para quem, de qual perfil, se declara documento e se o termo de
/// responsabilidade é exigido. Leva o pedido pelos passos reais do agregado (criação, fixação do termo, documento
/// verificado e aceite) até a situação que o teste quer. Por padrão, Betina pede o perfil comum para Leandro.
/// </summary>
internal sealed class PedidoDeConcessaoBuilder
{
    private Perfil _perfil = PerfilComum();
    private PessoaNoToken _solicitante = Betina;
    private PessoaNoToken _beneficiario = Leandro;
    private bool _paraSiMesmo;
    private bool _anexara;
    private bool _protegido;

    /// <summary>A versão vigente do termo no instante da criação.</summary>
    public VersaoDoTermo Vigente { get; private set; } = TermosDeTeste.Publicada();

    public static PedidoDeConcessaoBuilder Pedido() => new();

    public PedidoDeConcessaoBuilder DoPerfil(Perfil perfil)
    {
        _perfil = perfil;
        return this;
    }

    public PedidoDeConcessaoBuilder De(PessoaNoToken solicitante)
    {
        _solicitante = solicitante;
        return this;
    }

    public PedidoDeConcessaoBuilder Para(PessoaNoToken beneficiario)
    {
        _beneficiario = beneficiario;
        _paraSiMesmo = false;
        return this;
    }

    public PedidoDeConcessaoBuilder ParaSiMesmo()
    {
        _paraSiMesmo = true;
        return this;
    }

    public PedidoDeConcessaoBuilder DeclarandoDocumento()
    {
        _anexara = true;
        return this;
    }

    /// <summary>O pedido exige o termo, como se o perfil reunisse permissão pessoal, mesmo com o catálogo de teste interno.</summary>
    public PedidoDeConcessaoBuilder Protegido()
    {
        _protegido = true;
        return this;
    }

    public PedidoDeConcessaoBuilder ComVigente(VersaoDoTermo vigente)
    {
        Vigente = vigente;
        return this;
    }

    /// <summary>O pedido criado e com o termo fixado, quando exigido; no pedido para si sem documento, já com o aceite.</summary>
    public PedidoDeConcessao Criado()
    {
        PedidoDeConcessao pedido = PedidoDeConcessao.Criar(
            new PartesDoPedido(_solicitante, _paraSiMesmo ? null : _beneficiario.Email),
            _perfil, EscopoDe(_perfil), new DeclaracaoDoPedido(Justificativa, null, _anexara), Dominios, Catalogo, Agora).Value!;
        bool exigido = _protegido || PedidoDeConcessao.ExigeTermo(_perfil, Catalogo);

        pedido.FixarTermo(new TermoNaCriacao(exigido ? Vigente : null, exigido ? Vigente.Id : null, exigido), Agora).IsSuccess.Should().BeTrue();
        return pedido;
    }

    /// <summary>O pedido depois do documento declarado verificado, quando houver, e do aceite do beneficiário.</summary>
    public PedidoDeConcessao AguardandoGestor()
    {
        PedidoDeConcessao pedido = Criado();
        if (pedido.Situacao == SituacaoDoPedido.AguardandoDocumento)
        {
            VerificarDocumento(pedido);
        }

        if (pedido.Situacao == SituacaoDoPedido.AguardandoBeneficiario)
        {
            pedido.Aceitar(_paraSiMesmo ? _solicitante : _beneficiario, pedido.TermoVersaoId, Agora).IsSuccess.Should().BeTrue();
        }

        return pedido;
    }

    /// <summary>Anexa um documento ao pedido, verifica-o e libera o pedido que o aguardava.</summary>
    public static void VerificarDocumento(PedidoDeConcessao pedido)
    {
        AnexoDoPedido anexo = pedido.NovoAnexo(1024).Value!;
        pedido.ConfirmarAnexo(anexo.Id, "\"etag\"", Agora);
        anexo.RegistrarVerificado(new string('a', 64), Agora);
        pedido.LiberarAoVerificarDocumento();
    }
}
