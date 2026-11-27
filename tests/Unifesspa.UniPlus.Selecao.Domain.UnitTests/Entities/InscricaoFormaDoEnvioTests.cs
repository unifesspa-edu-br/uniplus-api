namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class InscricaoFormaDoEnvioTests
{
    private static readonly DateTimeOffset Instante = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly RespostasAdmitidas Admitidas = new(
        new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal)
        {
            ["CAMPO"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false),
            ["NOME_MEMBRO"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false),
        },
        new HashSet<string>(StringComparer.Ordinal) { "CAMPO" },
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal),
        new HashSet<Guid>(),
        new Dictionary<TermoNoFormulario, TermoAdmitido>());

    [Fact(DisplayName = "As recusas locais do envio não repetem a conferência da forma")]
    public void RecusasDoEnvio_RespostaForaDaForma_NaoIncluiARecusaDeForma()
    {
        Inscricao inscricao = NovaInscricao();

        IReadOnlyList<FieldError> recusas = inscricao.RecusasDoEnvio(Recebido(ValorForaDoFormato()), Admitidas, Apuracao());

        recusas.Should().NotContain(e => e.Error.Code == InscricaoErrorCodes.ValorForaDoTipo);
    }

    [Fact(DisplayName = "O envio recusa por conta própria o valor fora do formato, uma só vez")]
    public void Submeter_ValorForaDoFormato_RecusaUmaVez()
    {
        Inscricao inscricao = NovaInscricao();

        Result<ComprovanteInscricao> resultado = Enviar(inscricao, Recebido(ValorForaDoFormato()));

        resultado.Errors.Should().ContainSingle(e => e.Error.Code == InscricaoErrorCodes.ValorForaDoTipo && e.Field == "CAMPO");
    }

    [Fact(DisplayName = "O envio recusa o campo que só existe em grupo respondido no nível de cima")]
    public void Submeter_CampoDeGrupoRespondidoNoNivelDeCima_RecusaUmaVez()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal) { ["NOME_MEMBRO"] = JsonSerializer.SerializeToElement("valor") };

        Result<ComprovanteInscricao> resultado = Enviar(inscricao, Recebido(respostas));

        resultado.Errors.Should().ContainSingle(e => e.Error.Code == InscricaoErrorCodes.FatoSoDeGrupo && e.Field == "NOME_MEMBRO");
    }

    [Fact(DisplayName = "O envio sem recusas emite o comprovante")]
    public void Submeter_SemRecusas_EmiteComprovante()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal) { ["CAMPO"] = JsonSerializer.SerializeToElement("valor") };

        Result<ComprovanteInscricao> resultado = Enviar(inscricao, Recebido(respostas));

        resultado.IsSuccess.Should().BeTrue();
        inscricao.Estado.NumeroInscricao.Should().Be("000123");
    }

    private static Result<ComprovanteInscricao> Enviar(Inscricao inscricao, PreenchimentoDoCandidato recebido) =>
        inscricao.Submeter("000123", recebido, Admitidas, Apuracao(), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante);

    private static ApuracaoDoEnvio Apuracao() =>
        new(FormularioDeTeste.AvaliacaoSemOcultos, new Dictionary<string, JsonElement>(), new Dictionary<string, DefinicaoDaResposta>(), []);

    private static PreenchimentoDoCandidato Recebido(Dictionary<string, JsonElement> respostas) =>
        PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(null, null), respostas,
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), []);

    private static Dictionary<string, JsonElement> ValorForaDoFormato() =>
        new(StringComparer.Ordinal) { ["CAMPO"] = JsonSerializer.SerializeToElement(42) };

    private static Inscricao NovaInscricao() => Inscricao.Iniciar(
        Guid.NewGuid(), PersonasDeTeste.FernandaDrumond, "conta-1", "https://issuer", Guid.NewGuid()).Value!;
}
