namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.DoublesDaInstalacao;

/// <summary>
/// O código da leitura por titularidade só identifica a operação no registro de acesso: não está no catálogo de
/// permissões, e o perfil que o reúne é recusado na criação e na aprovação. Sem versão em vigor, nenhum pedido de
/// concessão alcança esse perfil (ADR-0166). O prefixo <c>autorizacao</c> do código o prende às permissões do módulo de
/// autorização no catálogo de permissões, as únicas entre as quais poderia constar.
/// </summary>
public sealed class LeituraPorTitularidadeForaDoCatalogoTests
{
    private const string Codigo = AcessoDoAprovador.CodigoDaLeituraPorTitularidade;

    private static readonly CatalogoDaAutorizacao CatalogoReal = new();

    [Fact(DisplayName = "O código da leitura por titularidade não está no catálogo de permissões")]
    public void Codigo_ForaDoCatalogoDePermissoes()
    {
        CatalogoDePermissoes<PermissaoAutorizacao>.Todas().Select(static d => d.Codigo).Should().NotContain(Codigo);
        CatalogoReal.Existe(Codigo).Should().BeFalse();
    }

    [Fact(DisplayName = "O perfil que reúne o código da leitura por titularidade é recusado na criação")]
    public void Perfil_ComOCodigo_RecusadoNaCriacao()
    {
        Result<ConteudoDaVersao> conteudo = ConteudoDaVersao.Validar(
            "Leitura dos próprios pedidos", "Lê os próprios pedidos de concessão.", [Codigo],
            PerfisDeTeste.Protocolo, PerfisDeTeste.TamanhoDoDocumento, CatalogoReal);

        conteudo.Errors.Select(static e => e.Error.Code).Should().Contain(PerfilErrorCodes.PermissaoInexistente);
    }

    [Fact(DisplayName = "A versão que reúne o código da leitura por titularidade é recusada na aprovação, e nenhum pedido de concessão alcança o perfil")]
    public void Versao_ComOCodigo_RecusadaNaAprovacaoESemPedido()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Leitura dos próprios pedidos", "Lê os próprios pedidos de concessão.", [Codigo], new CatalogoQueAceitaOCodigo());
        VersaoDoPerfil versao = ComDocumentoVerificado(perfil);

        DesfechoDaVersao desfecho = PerfisDeTeste.Decidir(perfil, versao, EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.AutoriaDoGestor, CatalogoReal);

        desfecho.Erro!.Code.Should().Be(PerfilErrorCodes.PermissaoInexistente);
        perfil.TemComposicaoEmVigor.Should().BeFalse();
        PedidoDoPerfil(perfil).Errors.Select(static e => e.Error.Code).Should().Contain(PedidoErrorCodes.PerfilSemVersaoEmVigor);
    }

    private static VersaoDoPerfil ComDocumentoVerificado(Perfil perfil)
    {
        VersaoDoPerfil versao = perfil.VersaoPendente!;
        DocumentoDaVersao documento = PerfisDeTeste.Garantir(perfil.ConfirmarDocumento(versao.Id, "etag-de-teste", PerfisDeTeste.Instante));
        perfil.RegistrarDocumentoVerificado(documento.Id, new string('a', 64), PerfisDeTeste.Instante);

        return versao;
    }

    private static Result<PedidoDeConcessao> PedidoDoPerfil(Perfil perfil) =>
        PedidoDeConcessao.Criar(
            new PartesDoPedido(new PessoaNoToken(new Identidade(Emissor, "betina.neves"), "Betina das Neves", "betina-dasneves81@uniplus-test.local", true), null),
            perfil,
            EscopoDaConcessao.DaInstituicao(),
            new DeclaracaoDoPedido("Acompanha os próprios pedidos de concessão."),
            new DominiosDeTeste(),
            CatalogoReal,
            PerfisDeTeste.Instante);

    // A consulta do catálogo de permissões da aplicação, como a infraestrutura a faz, com as permissões da autorização.
    private sealed class CatalogoDaAutorizacao : ICatalogoDePermissoesConsultavel
    {
        private readonly CatalogoDaAplicacao _catalogo = new([new FonteDaAutorizacao()]);

        public bool Existe(string codigo) => _catalogo.TentarObter(codigo, out _);

        public bool ReservadaAGovernanca(string codigo) =>
            _catalogo.TentarObter(codigo, out DefinicaoDePermissao? definicao) && definicao!.ReservadaAGovernanca;

        public bool DeUnidade(string codigo) => _catalogo.TentarObter(codigo, out DefinicaoDePermissao? definicao) && definicao!.DeUnidade;

        public ClassificacaoDeProtecao Sensibilidade(string codigo) =>
            _catalogo.TentarObter(codigo, out DefinicaoDePermissao? definicao) ? definicao!.Requisito.Sensibilidade : ClassificacaoDeProtecao.Sensivel;
    }

    // A versão nasce com um catálogo de permissões que aceitasse o código; a aprovação confere de novo contra o catálogo de
    // permissões atual, que o recusa. Fora a existência do código, responde como o atual, para a aprovação não recalcular
    // as etapas.
    private sealed class CatalogoQueAceitaOCodigo : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => codigo == Codigo || CatalogoReal.Existe(codigo);

        public bool ReservadaAGovernanca(string codigo) => CatalogoReal.ReservadaAGovernanca(codigo);

        public bool DeUnidade(string codigo) => CatalogoReal.DeUnidade(codigo);

        public ClassificacaoDeProtecao Sensibilidade(string codigo) => CatalogoReal.Sensibilidade(codigo);
    }

    private sealed class FonteDaAutorizacao : IFonteDeCatalogo
    {
        public Type TipoDoEnum => typeof(PermissaoAutorizacao);

        public IReadOnlyList<DefinicaoDePermissao> Definicoes() => CatalogoDePermissoes<PermissaoAutorizacao>.Todas();
    }
}
