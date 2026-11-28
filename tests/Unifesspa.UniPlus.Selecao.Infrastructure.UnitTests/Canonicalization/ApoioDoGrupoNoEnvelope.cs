namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

/// <summary>A leitura de um documento com um grupo e a conferência das recusas, usadas pelos testes de recusa do grupo.</summary>
internal static class ApoioDoGrupoNoEnvelope
{
    internal const string Grupo = "gruposDosFormularios[0]";

    internal static readonly DomainError OrdemNegativa = new(GrupoFormularioErrorCodes.OrdemInvalida, "A ordem do grupo não pode ser negativa.");

    internal static DomainError DoDominio(string codigo) => new(codigo, string.Empty);

    internal static IReadOnlyList<FieldError> Ler(JsonObject documento)
    {
        LeitorEnvelope leitor = new();
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores = new(StringComparer.Ordinal);
        LeituraDosGrupos.LerGruposColetados(leitor, documento, valores, [], FormulariosDoDocumentoDeTeste.ComFormularios());
        return leitor.Falha<object>().Errors;
    }

    internal static void Conferir(JsonObject grupo, (string Campo, DomainError Erro)[] esperadas, IReadOnlyList<string> valoresInjetados)
    {
        JsonObject documento = GrupoNoEnvelope.Documento(grupo);
        IReadOnlyList<FieldError> causas = Ler(documento);
        causas.Select(causa => (causa.Field!, causa.Error.Code)).Should().Equal(esperadas.Select(esperada => (esperada.Campo, esperada.Erro.Code)));
        causas.Select(static causa => causa.Error.Message).Zip(esperadas, static (mensagem, esperada) => esperada.Erro.Message.Length == 0 || mensagem == esperada.Erro.Message)
            .Should().AllBeEquivalentTo(true);
        ConferirCausas(documento, causas, valoresInjetados);
    }

    internal static void ConferirCausas(JsonObject documento, IReadOnlyList<FieldError> causas, IReadOnlyList<string> valoresInjetados)
    {
        causas.Should().OnlyHaveUniqueItems();
        causas.Should().OnlyContain(causa => CaminhoNoDocumento.Existe(documento, causa.Field!));
        foreach (string valor in valoresInjetados)
        {
            causas.Should().OnlyContain(causa => !causa.Error.Message.Contains(valor) && !causa.Field!.Contains(valor));
        }
    }
}
