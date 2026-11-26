namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

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
        [new AceiteDeTermo(FormularioDeTeste.CodigoDaInscricao, "VERACIDADE", Guid.Parse("01a12693-0000-7000-8000-000000000001"), new string('a', 64))],
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

    public static TheoryData<string, Func<Conteudo, Conteudo>> MudancasNoConteudo => new()
    {
        { "número", static c => c with { Numero = "00000002" } },
        { "instante", static c => c with { Instante = c.Instante.AddSeconds(1) } },
        { "instante no microssegundo", static c => c with { Instante = c.Instante.AddMicroseconds(1) } },
        { "oferta", static c => c with { Oferta = null } },
        { "resposta", static c => c with { Respostas = Valores(("NOME_SOCIAL", "Ariel D.")) } },
        { "derivado", static c => c with { Derivados = Valores(("UF_RESIDENCIA", "PA")) } },
        { "grupo", static c => c with { Grupos = new(StringComparer.Ordinal) } },
        { "aceite", static c => c with { Aceites = [] } },
    };

    public static TheoryData<string, Func<Conteudo, Conteudo>> MudancasNosDocumentos => new()
    {
        { "situação do documento", static c => c with { Documentos = [Laudo() with { Situacao = "PENDENTE", Arquivos = [] }] } },
        { "arquivo do documento", static c => c with { Documentos = [Laudo() with { Arquivos = [new ArquivoDoComprovante(new string('c', 64), 2048, "PDF")] }] } },
        { "consequência do documento", static c => c with { Documentos = [Laudo() with { ConsequenciaDoDescumprimento = null }] } },
        { "lista de documentos", static c => c with { Documentos = [] } },
    };

    public static TheoryData<string, Func<Conteudo, Conteudo>> MudancasNasModalidades => new()
    {
        { "modalidade", static c => c with { Modalidades = [new ModalidadeDeConcorrencia("AC", "Ampla concorrência", 0)] } },
        { "nome da modalidade", static c => c with { Modalidades = [new ModalidadeDeConcorrencia("AC", "Ampla", 0), new ModalidadeDeConcorrencia("LI_EP", "Escola pública", 1)] } },
        { "seção das modalidades", static c => c with { Modalidades = null } },
        { "2ª opção", static c => c with { OfertaSegundaOpcao = Guid.Parse("01a12693-0000-7000-8000-0000000000bb") } },
        { "modalidades da 2ª opção", static c => c with { ModalidadesSegundaOpcao = [new ModalidadeDeConcorrencia("AC", "Ampla concorrência", 0)] } },
    };

    [Theory(DisplayName = "Qualquer mudança no conteúdo comprovado muda o hash")]
    [MemberData(nameof(MudancasNoConteudo))]
    [MemberData(nameof(MudancasNosDocumentos))]
    [MemberData(nameof(MudancasNasModalidades))]
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
            .HashIntegridade.Should().Be(Hash(c)).And.Be("e135df3874dfe2ec4be619127c74b9e0eee58a6d02546b5506ce780c94978775", "o hash do conteúdo canônico, com a lista de documentos, é estável");
    }

    [Fact(DisplayName = "O comprovante sem documentos tem o hash golden")]
    public void Hash_SemDocumentos_TemOHashGolden()
    {
        Conteudo c = Base() with { Oferta = Guid.Parse("01a12693-0000-7000-8000-0000000000aa"), Documentos = [] };

        Hash(c).Should().Be("2b19b8d7077c785e844fea829cd60e8d7de0c0d5bb3af1d67a2f3ddd15a276ad");
    }

    [Fact(DisplayName = "O comprovante emitido com microssegundos no instante tem a integridade conferida")]
    public void IntegridadeConfere_InstanteComMicrossegundos_Confere()
    {
        Conteudo c = Base() with { Instante = Instante.AddMicroseconds(123456) };

        ComprovanteInscricao comprovante = ComprovanteInscricao.Emitir(
            Inscricao, c.Numero, c.Instante, c.Oferta, c.Respostas, c.Derivados, c.Grupos, c.Aceites, c.Documentos, c.Modalidades);

        comprovante.IntegridadeConfere().Should().BeTrue();
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

    [Fact(DisplayName = "TodasAsRespostas junta as respostas comuns e as sensíveis de cifra individual")]
    public void TodasAsRespostas_JuntaComunsESensiveis()
    {
        Conteudo c = Base();
        ComprovanteInscricao comprovante = ComprovanteInscricao.Emitir(
            Inscricao, c.Numero, c.Instante, c.Oferta, Valores(("APELIDO", "Nanda")), c.Derivados, c.Grupos, c.Aceites, c.Documentos, c.Modalidades,
            Valores(("IDENTIDADE_GENERO", "PESSOA_CIS")));

        comprovante.TodasAsRespostas().Keys.Should().BeEquivalentTo(["APELIDO", "IDENTIDADE_GENERO"]);
    }

    [Fact(DisplayName = "A posição vale só no último comprovante, e os vizinhos faltam nas pontas")]
    public void ComprovanteNaPosicao_ValeEVizinhos()
    {
        Conteudo c = Base();
        ComprovanteInscricao comprovante = ComprovanteInscricao.Emitir(
            Inscricao, c.Numero, c.Instante, c.Oferta, c.Respostas, c.Derivados, c.Grupos, c.Aceites, c.Documentos, c.Modalidades);

        ComprovanteNaPosicao primeiro = new(comprovante, 1, 3);
        ComprovanteNaPosicao ultimo = new(comprovante, 3, 3);
        ComprovanteNaPosicao unico = new(comprovante, 1, 1);

        (primeiro.Vale, primeiro.NumeroAnterior, primeiro.NumeroSeguinte).Should().Be((false, (int?)null, (int?)2));
        (ultimo.Vale, ultimo.NumeroAnterior, ultimo.NumeroSeguinte).Should().Be((true, (int?)2, (int?)null));
        (unico.Vale, unico.NumeroAnterior, unico.NumeroSeguinte).Should().Be((true, (int?)null, (int?)null));
    }
}
