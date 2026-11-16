namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ConferenciaDosFormulariosComEnvioTests
{
    private static CodigoDoFormulario Codigo(string valor) => CodigoDoFormulario.Criar(valor).Value!;

    private static FatosDaExecucao ComEnvio(params string[] codigos) => new()
    {
        Instante = FaseDaRetificacaoDeTeste.Instante,
        FasesIniciadas = FasesIniciadas.Nenhuma,
        FormulariosComEnvio = new HashSet<CodigoDoFormulario>(codigos.Select(Codigo)),
    };

    [Fact(DisplayName = "O formulário com envio que sai do documento é recusado, no campo dele")]
    public void FormularioComEnvioRetirado_Recusa()
    {
        IReadOnlyList<FieldError> recusas = ConferenciaDosFormulariosComEnvio.Conferir([Codigo("HABILITACAO")], ComEnvio("INSCRICAO", "HABILITACAO"));

        FieldError recusa = recusas.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("formularios[INSCRICAO]");
        recusa.Error.Code.Should().Be(RetificacaoErrorCodes.FormularioComEnvioNaoSai);
    }

    [Fact(DisplayName = "Os formulários com envio retirados acumulam em ordem ordinal do código")]
    public void FormulariosComEnvioRetirados_Acumulam()
    {
        ConferenciaDosFormulariosComEnvio.Conferir([], ComEnvio("INSCRICAO", "ISENCAO", "HABILITACAO"))
            .Select(static r => r.Field).Should().Equal("formularios[HABILITACAO]", "formularios[INSCRICAO]", "formularios[ISENCAO]");
    }

    [Fact(DisplayName = "O formulário com envio mantido, e o sem envio retirado, não são recusados")]
    public void FormularioMantidoOuSemEnvio_NaoRecusa()
    {
        ConferenciaDosFormulariosComEnvio.Conferir([Codigo("INSCRICAO")], ComEnvio("INSCRICAO")).Should().BeEmpty();
        ConferenciaDosFormulariosComEnvio.Conferir([], ComEnvio()).Should().BeEmpty();
    }
}
