namespace Unifesspa.UniPlus.Selecao.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

/// <summary>
/// O cadastro de reserva por chamada (UNI-REQ-0354, UNI-REQ-0355) é declaração própria do processo, fora da
/// configuração da distribuição de vagas: não é insumo nem saída do quadro. As duas partes não dependem uma
/// da outra.
/// </summary>
public sealed class CadastroDeReservaSeparadoDoQuadroDeVagasTests
{
    private static readonly string NamespaceDoCadastroDeReserva = typeof(CadastroDeReservaPorChamada).Namespace!;
    private static readonly string NamespaceDasVagas = typeof(QuadroDeVagas).Namespace!;
    private static readonly System.Type[] TiposDoQuadro = [typeof(ConfiguracaoDistribuicaoVagas), typeof(VagaOfertada), typeof(ModalidadeSelecionada)];

    private static readonly Architecture Arquitetura = new ArchLoader().LoadAssemblies(typeof(ProcessoSeletivo).Assembly).Build();

    [Fact(DisplayName = "A varredura encontra os tipos do cadastro de reserva e os do quadro de vagas")]
    public void Varredura_EncontraOsTiposDasDuasPartes()
    {
        Arquitetura.Types.Count(static tipo => tipo.Namespace.FullName == NamespaceDoCadastroDeReserva).Should().BeGreaterThan(3);
        Arquitetura.Types.Count(static tipo => tipo.Namespace.FullName == NamespaceDasVagas).Should().BeGreaterThan(3);
        Arquitetura.Types.Count(static tipo => TiposDoQuadro.Any(quadro => quadro.FullName == tipo.FullName)).Should().Be(TiposDoQuadro.Length);
    }

    [Fact(DisplayName = "O cadastro de reserva não depende do quadro de vagas nem dos tipos que o materializam")]
    public void CadastroDeReserva_NaoDependeDoQuadro()
    {
        IArchRule regra = Types().That().ResideInNamespace(NamespaceDoCadastroDeReserva)
            .Should().NotDependOnAnyTypesThat().ResideInNamespace(NamespaceDasVagas)
            .AndShould().NotDependOnAnyTypesThat().Are(TiposDoQuadro)
            .Because("o cadastro de reserva não é insumo nem saída do quadro: não muda o bloco das vagas nem o hash do congelamento");

        regra.Check(Arquitetura);
    }

    [Fact(DisplayName = "O quadro de vagas e os tipos que o materializam não dependem do cadastro de reserva")]
    public void Quadro_NaoDependeDoCadastroDeReserva()
    {
        IArchRule regra = Types().That().ResideInNamespace(NamespaceDasVagas).Or().Are(TiposDoQuadro)
            .Should().NotDependOnAnyTypesThat().ResideInNamespace(NamespaceDoCadastroDeReserva)
            .Because("as vagas de uma chamada chegam ao cadastro de reserva por argumento de quem convoca, e nunca pelo quadro");

        regra.Check(Arquitetura);
    }

    [Fact(DisplayName = "A regra do quadro detecta a violação: as vagas dependem dos tipos do quadro, e a regra que o proíbe falha")]
    public void RegraDoQuadro_DetectaAViolacao()
    {
        IArchRule regraQueAsVagasViolam = Types().That().ResideInNamespace(NamespaceDasVagas)
            .Should().NotDependOnAnyTypesThat().Are(TiposDoQuadro);

        Action verificar = () => regraQueAsVagasViolam.Check(Arquitetura);

        verificar.Should().Throw<FailedArchRuleException>();
    }

    [Fact(DisplayName = "A regra de namespace detecta a violação: a configuração da distribuição depende das vagas, e a regra que o proíbe falha")]
    public void RegraDeNamespace_DetectaAViolacao()
    {
        IArchRule regraQueAConfiguracaoViola = Types().That().Are(typeof(ConfiguracaoDistribuicaoVagas))
            .Should().NotDependOnAnyTypesThat().ResideInNamespace(NamespaceDasVagas);

        Action verificar = () => regraQueAConfiguracaoViola.Check(Arquitetura);

        verificar.Should().Throw<FailedArchRuleException>();
    }
}
