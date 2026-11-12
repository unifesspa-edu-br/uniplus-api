namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O perfil de Encarregado de dados é imutável como os outros perfis de governança: não se edita, não se versiona, não se
/// desativa, não se reativa, não se suspende nem perde permissão por remoção. Só o perfil de gestor de acessos não admite
/// concessão com prazo (ADR-0162).
/// </summary>
public sealed class PerfilDoEncarregadoDeDadosTests
{
    private const string Consultar = "autorizacao:pedidos:consultar";
    private const string Descricao = "Acompanha o tratamento de dados pessoais na gestão de acessos.";

    private readonly CatalogoMutavelDeTeste _catalogo = new();

    [Theory(DisplayName = "Todo perfil de governança recusa nova versão, desativação e reativação")]
    [InlineData(TipoDeGovernanca.GestorDeAcessos)]
    [InlineData(TipoDeGovernanca.RepresentanteDoControlador)]
    [InlineData(TipoDeGovernanca.EncarregadoDeDados)]
    public void Governanca_RecusaVersaoDesativacaoEReativacao(TipoDeGovernanca tipo)
    {
        Perfil perfil = DeGovernanca(tipo);

        Codigo(PerfisDeTeste.CriarVersao(perfil, PerfisDeTeste.Conteudo("Outro", Descricao, [Consultar], _catalogo), PerfisDeTeste.SemContexto, PerfisDeTeste.AutoriaDoAutor, PerfisDeTeste.Instante, _catalogo).Errors)
            .Should().Be(PerfilErrorCodes.GovernancaImutavel);
        Codigo(PerfisDeTeste.Desativar(perfil, _catalogo).Errors).Should().Be(PerfilErrorCodes.GovernancaImutavel);
        Codigo(PerfisDeTeste.Reativar(perfil, _catalogo).Errors).Should().Be(PerfilErrorCodes.GovernancaImutavel);
        perfil.Ativo.Should().BeTrue();
    }

    [Fact(DisplayName = "O perfil de Encarregado de dados não se suspende, não perde permissão por remoção nem cancela versão")]
    public void Encarregado_RecusaSuspensaoRemocaoECancelamento()
    {
        Perfil perfil = DeGovernanca(TipoDeGovernanca.EncarregadoDeDados);

        Codigo(perfil.Suspender(PerfisDeTeste.Gestor, EtapaDaVersao.GestaoDeAcessos, "Incidente.", PerfisDeTeste.Instante, _catalogo).Errors)
            .Should().Be(PerfilErrorCodes.GovernancaImutavel);
        perfil.Remover(new PedidoDeRemocao([Consultar], "Incidente.", CategoriaDaRemocao.Incidente, PerfisDeTeste.AutoriaDoGestor, PerfisDeTeste.Instante), _catalogo)
            .Erro!.Code.Should().Be(PerfilErrorCodes.GovernancaImutavel);
        perfil.CancelarVersao(Guid.NewGuid(), PerfisDeTeste.Gestor, "Motivo.", PerfisDeTeste.Instante)
            .Erro!.Code.Should().Be(PerfilErrorCodes.GovernancaImutavel);
        perfil.ComposicaoEmVigor.Should().Equal(Consultar);
    }

    [Theory(DisplayName = "Só o perfil de gestor de acessos não admite prazo; o Representante, o Encarregado e o perfil comum admitem")]
    [InlineData(TipoDeGovernanca.GestorDeAcessos, false)]
    [InlineData(TipoDeGovernanca.RepresentanteDoControlador, true)]
    [InlineData(TipoDeGovernanca.EncarregadoDeDados, true)]
    [InlineData(null, true)]
    public void AdmitePrazo_SoOGestorNaoAdmite(TipoDeGovernanca? governanca, bool admite) =>
        Perfil.AdmitePrazoCom(governanca).Should().Be(admite);

    private Perfil DeGovernanca(TipoDeGovernanca tipo) =>
        PerfisDeTeste.DeGovernanca("Perfil de governança", Descricao, [Consultar], _catalogo, tipo);

    private static string Codigo(IReadOnlyList<FieldError> erros) => erros.Single().Error.Code;
}
