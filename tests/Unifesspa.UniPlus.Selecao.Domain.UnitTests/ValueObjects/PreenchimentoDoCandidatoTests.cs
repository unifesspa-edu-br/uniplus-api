namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class PreenchimentoDoCandidatoTests
{
    private static RespostasAdmitidas Admitidas => new(new Dictionary<string, DefinicaoDaResposta>(), new HashSet<string>(),
        new Dictionary<string, IReadOnlySet<string>>(), new HashSet<Guid>(), new Dictionary<TermoNoFormulario, TermoAdmitido>());

    private static PreenchimentoDoCandidato Criar(IReadOnlyDictionary<string, JsonElement>? respostas = null,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>? grupos = null, IReadOnlyList<AceiteDeTermo>? aceites = null) =>
        PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(null, null), respostas ?? new Dictionary<string, JsonElement>(),
            grupos ?? new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), aceites ?? []);

    [Fact]
    public void Equals_PreenchimentosIndependentesComMesmoConteudo_SaoIguais()
    {
        PreenchimentoDoCandidato primeiro = Completo();
        PreenchimentoDoCandidato segundo = Completo();
        primeiro.Should().Be(segundo);
        primeiro.GetHashCode().Should().Be(segundo.GetHashCode());
    }

    [Theory]
    [InlineData("resposta")]
    [InlineData("grupo")]
    [InlineData("aceite")]
    [InlineData("opcao")]
    public void Equals_ConteudoDiferente_SaoDiferentes(string alteracao)
    {
        Completo().Should().NotBe(Completo(alteracao));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Equals_NulosNasOcorrencias_ComparaSemExcecao(int nivel)
    {
        PreenchimentoDoCandidato nulo = ComNulo(nivel);
        nulo.Should().Be(ComNulo(nivel));
        nulo.Should().NotBe(ComNulo(nivel + 1));
        ComNulo(nivel + 1).Should().NotBe(nulo);
    }

    private static PreenchimentoDoCandidato ComNulo(int nivel) => Criar(grupos: new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>
    {
        ["G"] = nivel == 0 ? null! : nivel == 1 ? [null!] : [new("1", nivel == 2 ? null! : new Dictionary<string, JsonElement>())],
    });

    private static PreenchimentoDoCandidato Completo(string? alteracao = null) => PreenchimentoDoCandidato.Criar(
        OpcoesDeCursoDaInscricao.Reidratar(alteracao == "opcao" ? Guid.Empty : null, null),
        new Dictionary<string, JsonElement> { ["A"] = JsonSerializer.SerializeToElement(alteracao == "resposta" ? 2 : 1) },
        new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>
        {
            ["G"] = [new("1", new Dictionary<string, JsonElement>
            { ["B"] = JsonSerializer.SerializeToElement(alteracao == "grupo" ? 2 : 1) })]
        },
        [new(FormularioDeTeste.CodigoDaInscricao, "T", Guid.Empty, alteracao == "aceite" ? "outro" : "hash")]);

    [Fact]
    public void Criar_ColecoesMutadas_PreservaCopiaProfunda()
    {
        Dictionary<string, JsonElement> respostas = new() { ["A"] = JsonSerializer.SerializeToElement(1) };
        List<OcorrenciaDeGrupoInscricao> ocorrencias = [new("1", respostas)];
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = new() { ["G"] = ocorrencias };
        List<AceiteDeTermo> aceites = [new(FormularioDeTeste.CodigoDaInscricao, "T", Guid.Empty, "hash")];
        PreenchimentoDoCandidato valor = Criar(respostas, grupos, aceites);
        respostas.Clear(); ocorrencias.Clear(); grupos.Clear(); aceites.Clear();
        valor.Respostas.Should().ContainKey("A");
        valor.Grupos["G"].Single().Respostas.Should().ContainKey("A");
        valor.Aceites.Should().ContainSingle();
        ((IDictionary<string, JsonElement>)valor.Respostas).Invoking(d => d.Clear()).Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void MescladoCom_EntradaParcial_PreservaAusentesERemoveNulos()
    {
        PreenchimentoDoCandidato atual = Criar(new Dictionary<string, JsonElement> { ["A"] = JsonSerializer.SerializeToElement(1), ["B"] = JsonSerializer.SerializeToElement(2) });
        PreenchimentoDoCandidato recebido = Criar(new Dictionary<string, JsonElement> { ["A"] = JsonSerializer.SerializeToElement<object?>(null), ["C"] = JsonSerializer.SerializeToElement(3) });
        PreenchimentoDoCandidato mesclado = atual.MescladoCom(recebido);
        mesclado.Respostas.Keys.Should().BeEquivalentTo("B", "C");
        atual.Respostas.Keys.Should().Equal("A", "B");
    }

    [Fact]
    public void RecusasDeForma_CamposEGruposInvalidos_AcumulaNaOrdem()
    {
        RespostasAdmitidas admitidas = Admitidas with { Definicoes = new Dictionary<string, DefinicaoDaResposta> { ["SUB"] = new(TipoDoValorDeResposta.Texto, false, false) } };
        PreenchimentoDoCandidato valor = Criar(new Dictionary<string, JsonElement> { ["SUB"] = JsonSerializer.SerializeToElement("x"), ["FORA"] = JsonSerializer.SerializeToElement(1) },
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> { ["G"] = [] });
        valor.RecusasDeForma(admitidas).Select(static r => (r.Field, r.Error.Code, r.Error.Message)).Should().Equal(
            ("SUB", InscricaoErrorCodes.FatoSoDeGrupo, "Este campo só pode ser respondido dentro de uma ocorrência do grupo. Remova-o das respostas de nível superior e informe-o na ocorrência."),
            ("FORA", InscricaoErrorCodes.FatoForaDoFormulario, "Este campo não pertence ao formulário desta inscrição."),
            ("G", InscricaoErrorCodes.GrupoForaDoFormulario, "Este grupo não pertence ao formulário desta inscrição. Remova-o das respostas."));
    }

    [Fact]
    public void RecusasDeForma_OcorrenciasInvalidas_PreservaCampoSemExporCodigosNaMensagem()
    {
        RespostasAdmitidas admitidas = Admitidas with
        {
            SubitensPorGrupo = new Dictionary<string, IReadOnlySet<string>> { ["GRUPO"] = new HashSet<string>() },
        };
        PreenchimentoDoCandidato valor = Criar(grupos: new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>
        {
            ["GRUPO"] = [new("ocorrencia", new Dictionary<string, JsonElement> { ["CAMPO"] = JsonSerializer.SerializeToElement(1) }),
                new("ocorrencia", new Dictionary<string, JsonElement>())],
        });

        valor.RecusasDeForma(admitidas).Select(static r => (r.Field, r.Error.Code, r.Error.Message)).Should().Equal(
            ("GRUPO.ocorrencia.CAMPO", InscricaoErrorCodes.FatoForaDoGrupo, "Este campo não pertence ao grupo. Remova-o desta ocorrência e responda-o fora do grupo, se o formulário o pedir."),
            ("GRUPO.ocorrencia", InscricaoErrorCodes.GrupoIdDuplicado, "Cada ocorrência do grupo precisa de um identificador próprio, não vazio e não repetido."));
    }

    [Fact]
    public void RecusasDosAceites_RepetidosEForaDaVersao_AcumulaNaOrdem()
    {
        PreenchimentoDoCandidato valor = Criar(aceites: [new(FormularioDeTeste.CodigoDaInscricao, "A", Guid.Empty, "errado"), new(FormularioDeTeste.CodigoDaInscricao, "A", Guid.Empty, "errado"), new(FormularioDeTeste.CodigoDaInscricao, "B", Guid.Empty, "x")]);
        Dictionary<TermoNoFormulario, TermoAdmitido> termos = new() { [FormularioDeTeste.TermoDaInscricao("A")] = new(Guid.Empty, "certo") };
        valor.RecusasDosAceites(termos).Select(static r => (r.Field, r.Error.Code, r.Error.Message)).Should().Equal(
            ("aceites[0]", InscricaoErrorCodes.AceiteDeOutraVersao, "O aceite do termo 'A' não é da versão que o formulário apresenta. Recarregue a página e aceite o termo exibido."),
            ("aceites[1]", InscricaoErrorCodes.AceiteRepetido, "O termo 'A' foi aceito mais de uma vez. Envie um único aceite para cada termo."),
            ("aceites[2]", InscricaoErrorCodes.AceiteForaDoFormulario, "O termo 'B' não faz parte do formulário. Recarregue a página e aceite os termos exibidos."));
    }

    [Fact(DisplayName = "O mesmo código de termo aceito em dois formulários não é aceite repetido, e os dois se mantêm")]
    public void Aceites_MesmoCodigoEmDoisFormularios_NaoSaoRepetidos()
    {
        PreenchimentoDoCandidato valor = Criar(aceites: [new("INSCRICAO", "VERACIDADE", Guid.Empty, "abc"), new("HABILITACAO", "VERACIDADE", Guid.Empty, "abc")]);
        Dictionary<TermoNoFormulario, TermoAdmitido> termos = new()
        {
            [new("INSCRICAO", "VERACIDADE")] = new(Guid.Empty, "abc"),
            [new("HABILITACAO", "VERACIDADE")] = new(Guid.Empty, "abc"),
        };

        valor.RecusasDosAceites(termos).Should().BeEmpty();
        valor.ComAceitesAdmitidos(Admitidas with { Termos = termos }).Aceites.Should().Equal(valor.Aceites);
    }

    [Fact(DisplayName = "O termo aceito duas vezes no mesmo formulário é repetido, mesmo com o aceite de outro formulário no meio")]
    public void Aceites_MesmoTermoRepetidoNoMesmoFormulario_RecusaSoARepeticao()
    {
        PreenchimentoDoCandidato valor = Criar(aceites:
        [
            new("INSCRICAO", "VERACIDADE", Guid.Empty, "abc"), new("HABILITACAO", "VERACIDADE", Guid.Empty, "abc"), new("HABILITACAO", "VERACIDADE", Guid.Empty, "abc"),
        ]);
        Dictionary<TermoNoFormulario, TermoAdmitido> termos = new()
        {
            [new("INSCRICAO", "VERACIDADE")] = new(Guid.Empty, "abc"),
            [new("HABILITACAO", "VERACIDADE")] = new(Guid.Empty, "abc"),
        };

        valor.RecusasDosAceites(termos).Select(static r => (r.Field, r.Error.Code)).Should().Equal(("aceites[2]", InscricaoErrorCodes.AceiteRepetido));
    }

    [Fact]
    public void ComAceitesAdmitidos_VersaoFixadaOutraVersaoETermoFora_MantemApenasVersaoFixada()
    {
        AceiteDeTermo valido = new(FormularioDeTeste.CodigoDaInscricao, "A", Guid.Empty, "abc");
        PreenchimentoDoCandidato valor = Criar(aceites: [valido, new(FormularioDeTeste.CodigoDaInscricao, "B", Guid.NewGuid(), "abc"), new(FormularioDeTeste.CodigoDaInscricao, "FORA", Guid.Empty, "abc")]);
        RespostasAdmitidas admitidas = Admitidas with
        {
            Termos = new Dictionary<TermoNoFormulario, TermoAdmitido>
            { [FormularioDeTeste.TermoDaInscricao("A")] = new(Guid.Empty, "abc"), [FormularioDeTeste.TermoDaInscricao("B")] = new(Guid.Empty, "abc") }
        };

        valor.ComAceitesAdmitidos(admitidas).Aceites.Should().Equal(valido);
    }

    private static Dictionary<TermoNoFormulario, TermoAdmitido> TermosAdmitidosDaInscricao(params string[] codigos) =>
        codigos.ToDictionary(static c => FormularioDeTeste.TermoDaInscricao(c), static _ => new TermoAdmitido(Guid.Empty, "abc"));

    [Fact]
    public void ComAceitesAdmitidos_RecusasDeVersao_DescartaRecusadosEMantemApenasNaoRecusados()
    {
        PreenchimentoDoCandidato valor = Criar(aceites: [new(FormularioDeTeste.CodigoDaInscricao, "A", Guid.Empty, "abc"), new(FormularioDeTeste.CodigoDaInscricao, "B", Guid.NewGuid(), "abc"),
            new(FormularioDeTeste.CodigoDaInscricao, "C", Guid.Empty, "ABC"), new(FormularioDeTeste.CodigoDaInscricao, "FORA", Guid.Empty, "abc")]);
        Dictionary<TermoNoFormulario, TermoAdmitido> termos = TermosAdmitidosDaInscricao("A", "B", "C");
        IReadOnlyList<AceiteDeTermo> mantidos = valor.ComAceitesAdmitidos(Admitidas with { Termos = termos }).Aceites;
        List<FieldError> recusas = valor.RecusasDosAceites(termos).ToList();

        recusas.Where(r => r.Error.Code == InscricaoErrorCodes.AceiteDeOutraVersao).Select(r => r.Field)
            .Should().Equal("aceites[1]", "aceites[2]");
        mantidos.Should().Equal(valor.Aceites[0]);
        recusas.Select(r => r.Field).Should().NotContain("aceites[0]");
    }

    [Fact]
    public void RecusasDosAceites_RepetidoForaEDaOutraVersao_PreservaPrecedenciaEOrdemDoEnvio()
    {
        PreenchimentoDoCandidato valor = Criar(aceites: [new(FormularioDeTeste.CodigoDaInscricao, "A", Guid.Empty, "abc"), new(FormularioDeTeste.CodigoDaInscricao, "A", Guid.NewGuid(), "errado"),
            new(FormularioDeTeste.CodigoDaInscricao, "FORA", Guid.Empty, "abc"), new(FormularioDeTeste.CodigoDaInscricao, "B", Guid.NewGuid(), "abc")]);
        Dictionary<TermoNoFormulario, TermoAdmitido> termos = new() { [FormularioDeTeste.TermoDaInscricao("A")] = new(Guid.Empty, "abc"), [FormularioDeTeste.TermoDaInscricao("B")] = new(Guid.Empty, "abc") };

        valor.RecusasDosAceites(termos).Select(static r => (r.Field, r.Error.Code)).Should().Equal(
            ("aceites[1]", InscricaoErrorCodes.AceiteRepetido),
            ("aceites[2]", InscricaoErrorCodes.AceiteForaDoFormulario),
            ("aceites[3]", InscricaoErrorCodes.AceiteDeOutraVersao));
    }

    [Fact(DisplayName = "Aceite de outra versão seguido do aceite certo do mesmo termo recusa só o primeiro, como outra versão, e o segundo, como repetido")]
    public void RecusasDosAceites_OutraVersaoSeguidaDoAceiteCerto_RecusaOPrimeiroEORepetido()
    {
        PreenchimentoDoCandidato valor = Criar(aceites: [new(FormularioDeTeste.CodigoDaInscricao, "A", Guid.NewGuid(), "abc"), new(FormularioDeTeste.CodigoDaInscricao, "A", Guid.Empty, "abc")]);

        valor.RecusasDosAceites(TermosAdmitidosDaInscricao("A")).Select(static r => (r.Field, r.Error.Code)).Should().Equal(
            ("aceites[0]", InscricaoErrorCodes.AceiteDeOutraVersao),
            ("aceites[1]", InscricaoErrorCodes.AceiteRepetido));
    }

    [Fact]
    public void RecusasDoEndereco_SemCepEComplementoLongo_AcumulaNaOrdem()
    {
        RespostasAdmitidas admitidas = Admitidas with { Definicoes = new Dictionary<string, DefinicaoDaResposta> { ["ENDERECO"] = new(TipoDoValorDeResposta.Endereco, false, false) } };
        PreenchimentoDoCandidato valor = Criar(new Dictionary<string, JsonElement> { ["ENDERECO"] = JsonSerializer.SerializeToElement(new { cep = "", complemento = new string('a', 301) }) });
        valor.RecusasDoEndereco(admitidas).Select(static r => (r.Field, r.Error.Code, r.Error.Message)).Should().Equal(
            ("ENDERECO", InscricaoErrorCodes.EnderecoSemCep, "Informe o CEP do seu endereço, com 8 dígitos. Se você não sabe o CEP, consulte a busca dos Correios; se o seu bairro não tem CEP próprio, use o CEP geral da sua cidade e descreva o endereço real no complemento."),
            ("ENDERECO", InscricaoErrorCodes.EnderecoComplementoLongo, "O complemento aceita até 300 caracteres. Resuma a descrição do seu endereço."));
    }
}
