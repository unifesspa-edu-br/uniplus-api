namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Leva um rascunho de teste ao estado enviado, sem respostas, pelo caminho real do domínio.
/// </summary>
internal static class InscricaoEnviadaDeTeste
{
    internal static Inscricao Enviar(Inscricao rascunho)
    {
        Dictionary<string, JsonElement> semRespostas = new(StringComparer.Ordinal);
        rascunho.Submeter("000001",
            PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(null, null), semRespostas, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), []), new RespostasAdmitidas(new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal), new HashSet<Guid>(), new Dictionary<string, TermoAdmitido>(StringComparer.Ordinal)),
            new ApuracaoDoEnvio(FormularioDeTeste.AvaliacaoSemOcultos, semRespostas, new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal), [], null), PrecondicaoIfMatch.DeTags([rascunho.ETag]), DateTimeOffset.UnixEpoch).IsSuccess.Should().BeTrue();

        return rascunho;
    }
}
