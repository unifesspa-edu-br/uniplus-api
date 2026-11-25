namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

/// <summary>
/// A coluna <c>regra_de_homologacao</c> (<c>jsonb</c>) da fase do cronograma: a regra de homologação das
/// inscrições num documento só, com as alternativas e as condições na ordem declarada, que decide a ordem dos
/// motivos da inscrição não homologada. Não há tabela filha porque nunca se consulta por alternativa nem por
/// condição: a regra é lida e gravada com a fase que a declara. A coluna nula é a fase que não homologa.
/// </summary>
/// <remarks>
/// A leitura remonta a regra pelas mesmas fábricas do domínio e o enquadramento pela forma de
/// <see cref="PredicadoDnfJson"/>; conteúdo gravado que as viola é dado corrompido e a leitura falha, nunca
/// devolve uma regra que o domínio não declararia. A leitura exige nomes em camelCase com caixa exata,
/// números como números e chaves conhecidas e únicas, pois ignorar conteúdo pode alterar o sentido da regra.
/// </remarks>
internal static class ColunaDaRegraDeHomologacao
{
    private static readonly JsonSerializerOptions Opcoes = OpcoesJsonDeDocumento.Criar(new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
    });

    private static readonly string[] ChavesDoAtomo = ["codigoDoDado", "operador", "valor"];

    // O EF não chama o conversor para nulo: a coluna nula é a fase que não homologa.
    internal static readonly ValueConverter<RegraDeHomologacaoDasInscricoes?, string?> Conversor = new(
        regra => Escrever(regra!),
        documento => Ler(documento!));

    internal static readonly ValueComparer<RegraDeHomologacaoDasInscricoes?> Comparador = new(
        (a, b) => ReferenceEquals(a, b) || (a != null && b != null && a.MesmoConteudo(b)),
        regra => regra == null ? 0 : Escrever(regra).GetHashCode(StringComparison.Ordinal),
        regra => regra);

    internal static void MapearRegraDeHomologacao<TEntidade>(
        this EntityTypeBuilder<TEntidade> builder, Expression<Func<TEntidade, RegraDeHomologacaoDasInscricoes?>> propriedade)
        where TEntidade : class =>
        builder.Property(propriedade)
            .HasColumnName("regra_de_homologacao")
            .HasColumnType("jsonb")
            .HasComment("Regra de homologação das inscrições que a fase declara: o resultado que publica a lista e as alternativas de condições, na ordem declarada. Nula quando a fase não homologa.")
            .HasConversion(Conversor, Comparador);

    internal static string Escrever(RegraDeHomologacaoDasInscricoes regra) =>
        JsonSerializer.Serialize(
            new RegraPersistida(regra.ResultadoAtoCodigo, [.. regra.Alternativas.Select(PersistirAlternativa)]),
            Opcoes);

    internal static RegraDeHomologacaoDasInscricoes Ler(string documento)
    {
        RegraPersistida regra = Desserializar(documento)
            ?? throw Corrompida("o documento não é um objeto");
        IReadOnlyList<AlternativaPersistida> alternativas = regra.Alternativas
            ?? throw Corrompida("o documento não tem a lista de alternativas");

        return RegraDeHomologacaoDasInscricoes.Declarar(regra.ResultadoAtoCodigo, [.. alternativas.Select(Alternativa)]);
    }

    private static RegraPersistida? Desserializar(string documento)
    {
        try
        {
            return JsonSerializer.Deserialize<RegraPersistida>(documento, Opcoes);
        }
        catch (JsonException excecao)
        {
            throw new InvalidOperationException("Regra de homologação gravada inválida: documento JSON inválido.", excecao);
        }
    }

    private static AlternativaPersistida PersistirAlternativa(AlternativaDeCondicoes alternativa) =>
        new([.. alternativa.Condicoes.Select(PersistirCondicao)]);

    private static CondicaoPersistida PersistirCondicao(CondicaoDeHomologacao condicao) => new(
        condicao.Tipo.Codigo(),
        condicao.EdicaoDoExame,
        condicao.MotivoDesfavoravelCodigo,
        condicao.Enquadramento is null ? null : PredicadoDnfJson.ParaJson(condicao.Enquadramento));

    private static AlternativaDeCondicoes Alternativa(AlternativaPersistida persistida)
    {
        IReadOnlyList<CondicaoPersistida> condicoes = persistida.Condicoes
            ?? throw Corrompida("uma alternativa não tem a lista de condições");

        return Exigir(AlternativaDeCondicoes.Declarar([.. condicoes.Select(Condicao)]));
    }

    private static CondicaoDeHomologacao Condicao(CondicaoPersistida persistida) => Exigir(CondicaoDeHomologacao.Declarar(
        persistida.Tipo, persistida.EdicaoDoExame, persistida.MotivoDesfavoravelCodigo, Enquadramento(persistida.Enquadramento)));

    private static PredicadoDnf? Enquadramento(JsonNode? enquadramento)
    {
        if (enquadramento is null)
        {
            return null;
        }

        JsonElement documento = enquadramento.Deserialize<JsonElement>(Opcoes);
        ExigirSomenteAsChavesDosAtomos(documento);
        PredicadoDnf predicado = Exigir(PredicadoDnfJson.DeJson(documento));
        return predicado.Clausulas.Count > 0
            ? predicado
            : throw Corrompida("o enquadramento não tem cláusula");
    }

    // A leitura do predicado exige as três chaves do átomo e ignora as demais; uma chave a mais, como um
    // operador escrito errado, sobreviveria à leitura e a regravação a apagaria em silêncio.
    private static void ExigirSomenteAsChavesDosAtomos(JsonElement enquadramento)
    {
        IEnumerable<JsonElement> clausulas = enquadramento.ValueKind == JsonValueKind.Array
            ? enquadramento.EnumerateArray().Where(static clausula => clausula.ValueKind == JsonValueKind.Array)
            : [];
        IEnumerable<JsonElement> atomos = clausulas
            .SelectMany(static clausula => clausula.EnumerateArray())
            .Where(static atomo => atomo.ValueKind == JsonValueKind.Object);

        if (atomos.Any(static atomo => atomo.EnumerateObject().Any(static chave => !ChavesDoAtomo.Contains(chave.Name, StringComparer.Ordinal))))
        {
            throw Corrompida("um átomo do enquadramento tem chave desconhecida");
        }
    }

    private static T Exigir<T>(Result<T> resultado) =>
        resultado.IsSuccess
            ? resultado.Value!
            : throw Corrompida(string.Join(" ", resultado.Errors.Select(static recusa => recusa.Error.Message)));

    private static InvalidOperationException Corrompida(string motivo) =>
        new($"Regra de homologação gravada inválida: {motivo}.");

    private sealed record RegraPersistida(string? ResultadoAtoCodigo, IReadOnlyList<AlternativaPersistida>? Alternativas);

    private sealed record AlternativaPersistida(IReadOnlyList<CondicaoPersistida>? Condicoes);

    private sealed record CondicaoPersistida(string? Tipo, int? EdicaoDoExame, string? MotivoDesfavoravelCodigo, JsonNode? Enquadramento);
}
