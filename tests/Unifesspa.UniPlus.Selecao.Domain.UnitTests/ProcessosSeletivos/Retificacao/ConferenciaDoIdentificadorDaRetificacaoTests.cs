namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O identificador legível que a versão publicada vigente congelou não muda: o endereço da página pública e a chave
/// do documento no acervo derivam dele. Quando a versão vigente não o traz, o documento o declara livremente.
/// </summary>
public sealed class ConferenciaDoIdentificadorDaRetificacaoTests
{
    [Fact(DisplayName = "O documento que troca o identificador da versão vigente é recusado, no campo do identificador")]
    public void IdentificadorTrocado_Recusa()
    {
        IReadOnlyList<FieldError> recusas =
            ConferenciaDoIdentificadorDaRetificacao.Conferir(Identificador("psiq-2026"), Identificador("psiq-2026b"));

        recusas.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = ConferenciaDoIdentificadorDaRetificacao.Campo,
            Error = new { Code = ProcessoSeletivoErrorCodes.IdentificadorLegivelImutavel },
        });
    }

    [Fact(DisplayName = "O documento que omite o identificador da versão vigente também é recusado")]
    public void IdentificadorOmitido_Recusa() =>
        ConferenciaDoIdentificadorDaRetificacao.Conferir(Identificador("psiq-2026"), null)
            .Should().ContainSingle().Which.Error.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelImutavel);

    [Fact(DisplayName = "Reenviar o mesmo identificador não é troca")]
    public void IdentificadorIgual_Aceita() =>
        ConferenciaDoIdentificadorDaRetificacao.Conferir(Identificador("psiq-2026"), Identificador("psiq-2026")).Should().BeEmpty();

    [Theory(DisplayName = "A versão vigente sem identificador deixa o documento declará-lo livremente")]
    [InlineData(null)]
    [InlineData("psiq-2026")]
    public void VersaoSemIdentificador_Livre(string? doDocumento) =>
        ConferenciaDoIdentificadorDaRetificacao.Conferir(null, doDocumento is null ? null : Identificador(doDocumento)).Should().BeEmpty();

    private static IdentificadorLegivel Identificador(string valor) => IdentificadorLegivel.Criar(valor).Value;
}
