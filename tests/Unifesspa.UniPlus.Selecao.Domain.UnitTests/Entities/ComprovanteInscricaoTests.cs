namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

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
        List<AceiteDeTermo> Aceites,
        List<ModalidadeDeConcorrencia>? Modalidades,
        List<DocumentoDoComprovante> Documentos,
        Guid? OfertaSegundaOpcao = null,
        List<ModalidadeDeConcorrencia>? ModalidadesSegundaOpcao = null);

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
        [new AceiteDeTermo("VERACIDADE", Guid.Parse("01a12693-0000-7000-8000-000000000001"), new string('a', 64))],
        [new ModalidadeDeConcorrencia("AC", "Ampla concorrência", 0), new ModalidadeDeConcorrencia("LI_EP", "Escola pública", 1)],
        [Laudo()]);

    private static DocumentoDoComprovante Laudo() => new(
        Guid.Parse("01a12693-0000-7000-8000-0000000000d1"), "LAUDO_MEDICO", "Laudo médico", true, "SATISFEITA", null,
        "Sem o documento Laudo médico, a vantagem que você pleiteia não será concedida.",
        [new ArquivoDoComprovante(new string('b', 64), 2048, "PDF")]);

    private static Dictionary<string, JsonElement> Valores(params (string Fato, string Valor)[] valores) =>
        valores.ToDictionary(static v => v.Fato, static v => JsonSerializer.SerializeToElement(v.Valor), StringComparer.Ordinal);

    private static string Hash(Conteudo c) =>
        ComprovanteInscricao.Emitir(Inscricao, c.Numero, c.Instante, c.Oferta, c.Respostas, c.Derivados, c.Grupos, c.Aceites, c.Documentos, c.Modalidades,
            ofertaCursoSegundaOpcaoId: c.OfertaSegundaOpcao, modalidadesSegundaOpcao: c.ModalidadesSegundaOpcao).HashIntegridade;

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
        { "situação do documento", static c => c with { Documentos = [Laudo() with { Situacao = "PENDENTE", Arquivos = [] }] } },
        { "arquivo do documento", static c => c with { Documentos = [Laudo() with { Arquivos = [new ArquivoDoComprovante(new string('c', 64), 2048, "PDF")] }] } },
        { "consequência do documento", static c => c with { Documentos = [Laudo() with { Consequencia = null }] } },
        { "lista de documentos", static c => c with { Documentos = [] } },
        { "modalidade", static c => c with { Modalidades = [new ModalidadeDeConcorrencia("AC", "Ampla concorrência", 0)] } },
        { "nome da modalidade", static c => c with { Modalidades = [new ModalidadeDeConcorrencia("AC", "Ampla", 0), new ModalidadeDeConcorrencia("LI_EP", "Escola pública", 1)] } },
        { "seção das modalidades", static c => c with { Modalidades = null } },
        { "2ª opção", static c => c with { OfertaSegundaOpcao = Guid.Parse("01a12693-0000-7000-8000-0000000000bb") } },
        { "modalidades da 2ª opção", static c => c with { ModalidadesSegundaOpcao = [new ModalidadeDeConcorrencia("AC", "Ampla concorrência", 0)] } },
    };

    [Theory(DisplayName = "Qualquer mudança no conteúdo comprovado muda o hash")]
    [MemberData(nameof(Mudancas))]
    public void Hash_ConteudoDiferente_HashDiferente(string campo, Func<Conteudo, Conteudo> mudar)
    {
        ArgumentNullException.ThrowIfNull(mudar);

        Hash(mudar(Base())).Should().NotBe(Hash(Base()), $"o hash cobre {campo}");
    }

    [Fact(DisplayName = "O comprovante sem dado sensível tem o hash golden, com ou sem a lista vazia de sensíveis")]
    public void Hash_SemSensiveis_ContinuaIgual()
    {
        Conteudo c = Base() with { Oferta = Guid.Parse("01a12693-0000-7000-8000-0000000000aa") };

        ComprovanteInscricao.Emitir(Inscricao, c.Numero, c.Instante, c.Oferta, c.Respostas, c.Derivados, c.Grupos, c.Aceites, c.Documentos, c.Modalidades, new Dictionary<string, JsonElement>())
            .HashIntegridade.Should().Be(Hash(c)).And.Be("7492bc231faac865a679c9b5fff33c0c43958055afc9110014a2b5ddd030a1d3", "o hash do conteúdo canônico, com a lista de documentos, é estável");
    }

    [Fact(DisplayName = "O comprovante sem documentos tem o hash que os já emitidos sempre tiveram")]
    public void Hash_SemDocumentos_ContinuaIgualAoDeSempre()
    {
        Conteudo c = Base() with { Oferta = Guid.Parse("01a12693-0000-7000-8000-0000000000aa"), Documentos = [] };

        Hash(c).Should().Be("8b6439933cd74d633d0f17b210e2d5ceefd85d5d317c3d9d27d88a9e14f89c58");
    }

    [Fact(DisplayName = "O hash cobre os códigos dos fatos sensíveis e nunca os valores")]
    public void Hash_Sensiveis_CobreOsCodigosENaoOsValores()
    {
        Conteudo c = Base();
        string Emitir(string genero) => ComprovanteInscricao.Emitir(
            Inscricao, c.Numero, c.Instante, c.Oferta, c.Respostas, c.Derivados, c.Grupos, c.Aceites, c.Documentos, c.Modalidades,
            Valores(("IDENTIDADE_GENERO", genero))).HashIntegridade;

        Emitir("PESSOA_CIS").Should().Be(Emitir("PESSOA_NAO_BINARIA"), "o valor fica fora do hash: de poucas opções, seria recuperável por tentativa");
        Emitir("PESSOA_CIS").Should().NotBe(Hash(c), "o código do fato sensível está no hash");
    }

    [Fact(DisplayName = "Apagar os fatos sensíveis do comprovante emitido com eles faz a integridade acusar")]
    public void IntegridadeConfere_SensiveisApagados_Acusa()
    {
        Conteudo c = Base();
        ComprovanteInscricao comprovante = ComprovanteInscricao.Emitir(
            Inscricao, c.Numero, c.Instante, c.Oferta, c.Respostas, c.Derivados, c.Grupos, c.Aceites, c.Documentos, c.Modalidades,
            Valores(("IDENTIDADE_GENERO", "PESSOA_CIS")));
        comprovante.IntegridadeConfere().Should().BeTrue();

        comprovante.RestaurarSensiveis(new Dictionary<string, JsonElement>());

        comprovante.IntegridadeConfere().Should().BeFalse();
    }
}
