namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

public sealed class ComprovanteInscricaoTests
{
    private static readonly Guid Inscricao = Guid.CreateVersion7();
    private static readonly Guid Oferta = Guid.CreateVersion7();
    private static readonly DateTimeOffset Instante = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    public sealed record Conteudo(
        string Numero,
        DateTimeOffset Instante,
        Guid? Oferta,
        Dictionary<string, JsonElement> Respostas,
        Dictionary<string, JsonElement> Derivados,
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> Grupos,
        List<AceiteDeTermo> Aceites);

    private static Conteudo Base() => new(
        "00000001",
        Instante,
        Oferta,
        Valores(("NOME_SOCIAL", "Ariel Drumond")),
        Valores(("UF_RESIDENCIA", "MS")),
        new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] = [new OcorrenciaDeGrupoInscricao("membro-1", Valores(("PARENTESCO", "MAE")))],
        },
        [new AceiteDeTermo("VERACIDADE", Guid.Parse("01a12693-0000-7000-8000-000000000001"), new string('a', 64))]);

    private static Dictionary<string, JsonElement> Valores(params (string Fato, string Valor)[] valores) =>
        valores.ToDictionary(static v => v.Fato, static v => JsonSerializer.SerializeToElement(v.Valor), StringComparer.Ordinal);

    private static string Hash(Conteudo c) =>
        ComprovanteInscricao.Emitir(Inscricao, c.Numero, c.Instante, c.Oferta, c.Respostas, c.Derivados, c.Grupos, c.Aceites).HashIntegridade;

    [Fact(DisplayName = "O mesmo conteúdo dá o mesmo hash, em qualquer emissão")]
    public void Hash_MesmoConteudo_MesmoHash()
    {
        string primeiro = Hash(Base());

        Hash(Base()).Should().Be(primeiro);
        primeiro.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    public static TheoryData<string, Func<Conteudo, Conteudo>> Mudancas => new()
    {
        { "número", static c => c with { Numero = "00000002" } },
        { "instante", static c => c with { Instante = c.Instante.AddSeconds(1) } },
        { "oferta", static c => c with { Oferta = null } },
        { "resposta", static c => c with { Respostas = Valores(("NOME_SOCIAL", "Ariel D.")) } },
        { "derivado", static c => c with { Derivados = Valores(("UF_RESIDENCIA", "PA")) } },
        { "grupo", static c => c with { Grupos = new(StringComparer.Ordinal) } },
        { "aceite", static c => c with { Aceites = [] } },
    };

    [Theory(DisplayName = "Qualquer mudança no conteúdo comprovado muda o hash")]
    [MemberData(nameof(Mudancas))]
    public void Hash_ConteudoDiferente_HashDiferente(string campo, Func<Conteudo, Conteudo> mudar)
    {
        ArgumentNullException.ThrowIfNull(mudar);

        Hash(mudar(Base())).Should().NotBe(Hash(Base()), $"o hash cobre {campo}");
    }
}
