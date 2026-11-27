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

public sealed class InscricaoEquivalenciaTests
{
    private static readonly RespostasAdmitidas Admitidas = new(
        new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal)
        {
            ["CAMPO"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false),
        },
        new HashSet<string>(StringComparer.Ordinal) { "CAMPO" },
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal),
        new HashSet<Guid>(),
        new Dictionary<TermoNoFormulario, TermoAdmitido>());

    [Fact]
    public void Salvar_RespostasValidas_GravaValorEAvancaRevisao()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal) { ["CAMPO"] = JsonSerializer.SerializeToElement("valor") };

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), respostas, SemGrupos(), []), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.IsSuccess.Should().BeTrue();
        inscricao.Formulario.Preenchimento.Respostas["CAMPO"].GetString().Should().Be("valor");
        inscricao.Revisao.Should().Be(2);
    }

    [Fact]
    public void Salvar_RespostasEOpcoesInvalidas_AcumulaRecusasNaOrdemSemMutar()
    {
        Inscricao inscricao = NovaInscricao();
        string etag = inscricao.ETag;
        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(Guid.NewGuid(), null), RespostasInvalidas(), SemGrupos(), []), Admitidas, PrecondicaoIfMatch.DeTags([etag]));

        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code, erro.Error.Message)).Should().Equal(
            ("DESCONHECIDO", InscricaoErrorCodes.FatoForaDoFormulario, "Este campo não pertence ao formulário desta inscrição."),
            ("CAMPO", InscricaoErrorCodes.ValorForaDoTipo, "A resposta não tem o tipo que o campo pede."),
            ("ofertaCursoId", InscricaoErrorCodes.OfertaForaDoProcesso, "A oferta de curso escolhida não é deste processo seletivo."));
        inscricao.ETag.Should().Be(etag);
        inscricao.Formulario.Preenchimento.Respostas.Should().NotContainKey("CAMPO");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Salvar_GrupoForaDegenerado_RecusaSemExaminarOcorrencias(bool listaNula)
    {
        Inscricao inscricao = NovaInscricao();
        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), new Dictionary<string, JsonElement>(), GrupoDegenerado(listaNula), []), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));
        resultado.Errors.Should().Contain(e => e.Error.Code == InscricaoErrorCodes.GrupoForaDoFormulario);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Submeter_GrupoForaDegenerado_RecusaSemExaminarOcorrencias(bool listaNula)
    {
        Inscricao inscricao = NovaInscricao();
        Result<ComprovanteInscricao> resultado = inscricao.Submeter("000123",
            PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(null, null), new Dictionary<string, JsonElement>(), GrupoDegenerado(listaNula), []), Admitidas,
            new ApuracaoDoEnvio(FormularioDeTeste.AvaliacaoSemOcultos, new Dictionary<string, JsonElement>(), new Dictionary<string, DefinicaoDaResposta>(), []),
            PrecondicaoIfMatch.DeTags([inscricao.ETag]), new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero));
        resultado.Errors.Should().Contain(e => e.Error.Code == InscricaoErrorCodes.GrupoForaDoFormulario);
    }

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> GrupoDegenerado(bool listaNula) =>
        new() { ["FORA"] = listaNula ? null! : [new("1", null!)] };

    [Fact]
    public void AdotarVersao_ExcecaoAoFiltrarRespostas_PreservaEstadoParcial()
    {
        Inscricao inscricao = NovaInscricao();
        FormularioDaInscricao anterior = inscricao.Formulario;
        Guid versao = Guid.NewGuid();
        Action adotar = () => inscricao.AdotarVersao(versao, Admitidas with { DoNivelSuperior = null! });
        adotar.Should().Throw<NullReferenceException>();
        ConferirEstado(inscricao, SituacaoInscricao.Rascunho, null, 1, FormularioDaInscricao.Reidratar(versao, anterior.Preenchimento, anterior.Derivados, anterior.DefinicoesDasRespostas));
    }

    [Theory]
    [InlineData("opcoes")]
    [InlineData("grupos")]
    [InlineData("aceites")]
    public void AdotarVersao_ExcecaoNasEtapas_PreservaEstadoParcial(string etapa)
    {
        Inscricao inscricao = InscricaoPreenchida();
        FormularioDaInscricao anterior = inscricao.Formulario;
        Guid versao = Guid.NewGuid();
        RespostasAdmitidas admitidas = AdmitidasParaExcecao(etapa);
        Action adotar = () => inscricao.AdotarVersao(versao, admitidas);
        adotar.Should().Throw<NullReferenceException>();
        ConferirEstado(inscricao, SituacaoInscricao.Rascunho, "123", 7, FormularioEsperado(anterior, versao, etapa));
    }

    [Fact]
    public void AdotarVersao_ExcecaoNaSegundaOpcao_PreservaPrimeiraAtualizada()
    {
        Inscricao inscricao = InscricaoPreenchida();
        FormularioDaInscricao anterior = inscricao.Formulario;
        Guid versao = Guid.NewGuid();
        IReadOnlySet<Guid> ofertas = new OfertasQueFalhamNaSegundaConsulta();
        Action adotar = () => inscricao.AdotarVersao(versao, Admitidas with { OfertasDeCurso = ofertas });
        adotar.Should().Throw<InvalidOperationException>();
        PreenchimentoDoCandidato preenchimento = anterior.Preenchimento.ComOpcoes(OpcoesDeCursoDaInscricao.Reidratar(anterior.Preenchimento.Opcoes.Primeira, anterior.Preenchimento.Opcoes.Segunda));
        ConferirEstado(inscricao, SituacaoInscricao.Rascunho, "123", 7, FormularioDaInscricao.Reidratar(versao, preenchimento, anterior.Derivados, anterior.DefinicoesDasRespostas));
    }

    private static RespostasAdmitidas AdmitidasParaExcecao(string etapa) => Admitidas with
    {
        OfertasDeCurso = etapa == "opcoes" ? null! : new HashSet<Guid>(),
        SubitensPorGrupo = etapa == "grupos" ? null! : new Dictionary<string, IReadOnlySet<string>>(),
        Termos = etapa == "aceites" ? null! : new Dictionary<TermoNoFormulario, TermoAdmitido>(),
    };

    private static FormularioDaInscricao FormularioEsperado(FormularioDaInscricao anterior, Guid versao, string etapa)
    {
        PreenchimentoDoCandidato preenchimento = anterior.Preenchimento;
        if (etapa != "opcoes") preenchimento = preenchimento.ComOpcoes(OpcoesDeCursoDaInscricao.Reidratar(null, null)).ComRespostas(new Dictionary<string, JsonElement>());
        if (etapa == "aceites") preenchimento = preenchimento.ComGrupos(SemGrupos());
        return FormularioDaInscricao.Reidratar(versao, preenchimento, anterior.Derivados, anterior.DefinicoesDasRespostas);
    }

    private static Inscricao InscricaoPreenchida() => Inscricao.Reidratar(Guid.NewGuid(), Guid.NewGuid(),
            TitularDaInscricao.Reidratar(PersonasDeTeste.FernandaDrumond, "conta-1", "https://issuer"),
            EstadoDaInscricao.Reidratar(SituacaoInscricao.Rascunho, "123", CancelamentoDaInscricao.Reidratar(null, null, null)),
            FormularioDaInscricao.Reidratar(Guid.NewGuid(), PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(Guid.NewGuid(), Guid.NewGuid()), new Dictionary<string, JsonElement> { ["ANTIGO"] = JsonSerializer.SerializeToElement("valor") }, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> { ["G"] = [new("1", new Dictionary<string, JsonElement>())] }, [new(FormularioDeTeste.CodigoDaInscricao, "T", Guid.NewGuid(), "hash")]),
                new Dictionary<string, JsonElement>(), new Dictionary<string, DefinicaoDaResposta>()), 7, new Unifesspa.UniPlus.Selecao.Domain.ValueObjects.DatasDeGravacaoDaInscricao(default, null));

    [Fact]
    public void AdotarVersao_ExcecaoNasDefinicoes_PreservaFiltrosAplicados()
    {
        Inscricao inscricao = NovaInscricao();
        FormularioDaInscricao anterior = inscricao.Formulario;
        Guid versao = Guid.NewGuid();
        Action adotar = () => inscricao.AdotarVersao(versao, Admitidas with { Definicoes = null! });
        adotar.Should().Throw<ArgumentNullException>();
        PreenchimentoDoCandidato vazio = PreenchimentoDoCandidato.Criar(anterior.Preenchimento.Opcoes, new Dictionary<string, JsonElement>(), SemGrupos(), []);
        ConferirEstado(inscricao, SituacaoInscricao.Rascunho, null, 1, FormularioDaInscricao.Reidratar(versao, vazio, anterior.Derivados, anterior.DefinicoesDasRespostas));
    }

    [Fact]
    public void Submeter_ExcecaoNosDerivados_PreservaNumeroEFormularioAnterior()
    {
        Inscricao inscricao = NovaInscricao();
        FormularioDaInscricao anterior = inscricao.Formulario;
        Dictionary<string, JsonElement> derivados = new() { ["M"] = JsonSerializer.SerializeToElement("2927408") };
        Dictionary<string, DefinicaoDaResposta> definicoes = new() { ["M"] = new(TipoDoValorDeResposta.Municipio, false, false) };
        Action enviar = () => Enviar(inscricao, derivados, definicoes, []);
        enviar.Should().Throw<InvalidOperationException>();
        ConferirEstado(inscricao, SituacaoInscricao.Rascunho, "123", 1, anterior);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Submeter_ExcecaoEmMunicipioDasRespostasOuGrupos_PreservaEtapasAnteriores(bool noGrupo)
    {
        Inscricao inscricao = NovaInscricao();
        FormularioDaInscricao anterior = inscricao.Formulario;
        Dictionary<string, JsonElement> derivados = new() { ["D"] = JsonSerializer.SerializeToElement("valor") };
        Action enviar = () => EnviarComMunicipio(inscricao, noGrupo, derivados);
        enviar.Should().Throw<InvalidOperationException>();
        ConferirEstado(inscricao, SituacaoInscricao.Rascunho, "123", 1, FormularioDaInscricao.Reidratar(anterior.VersaoFormularioId, anterior.Preenchimento, derivados, anterior.DefinicoesDasRespostas));
    }

    private static void EnviarComMunicipio(Inscricao inscricao, bool noGrupo, Dictionary<string, JsonElement> derivados)
    {
        Dictionary<string, JsonElement> municipio = new() { ["M"] = JsonSerializer.SerializeToElement("2927408") };
        Dictionary<string, DefinicaoDaResposta> definicoes = new() { ["M"] = new(TipoDoValorDeResposta.Municipio, false, false) };
        RespostasAdmitidas admitidas = Admitidas with
        {
            Definicoes = definicoes,
            DoNivelSuperior = new HashSet<string> { "M" },
            SubitensPorGrupo = new Dictionary<string, IReadOnlySet<string>> { ["G"] = new HashSet<string> { "M" } }
        };
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = noGrupo ? new() { ["G"] = [new("1", municipio)] } : SemGrupos();
        inscricao.Submeter("123",
            PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(null, null), noGrupo ? new Dictionary<string, JsonElement>() : municipio, grupos, []), admitidas,
            new ApuracaoDoEnvio(FormularioDeTeste.AvaliacaoSemOcultos, derivados, new Dictionary<string, DefinicaoDaResposta> { ["D"] = new(TipoDoValorDeResposta.Texto, false, false) }, [], null), PrecondicaoIfMatch.DeTags([inscricao.ETag]), DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void Submeter_ExcecaoNoComprovante_PreservaEnvioSemEvento()
    {
        Inscricao inscricao = NovaInscricao();
        FormularioDaInscricao anterior = inscricao.Formulario;
        Action enviar = () => Enviar(inscricao, new Dictionary<string, JsonElement>(), new Dictionary<string, DefinicaoDaResposta>(), null!);
        enviar.Should().Throw<ArgumentNullException>();
        Dictionary<string, DefinicaoDaResposta> definicoes = new(anterior.DefinicoesDasRespostas) { ["CAMPO"] = Admitidas.Definicoes["CAMPO"] };
        ConferirEstado(inscricao, SituacaoInscricao.Enviada, "123", 2, FormularioDaInscricao.Reidratar(anterior.VersaoFormularioId, anterior.Preenchimento, anterior.Derivados, definicoes));
    }

    private static void Enviar(Inscricao inscricao, IReadOnlyDictionary<string, JsonElement> derivados,
        IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes, IReadOnlyList<ArquivoConfirmado> arquivos) =>
        inscricao.Submeter("123",
            PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(null, null), new Dictionary<string, JsonElement>(), SemGrupos(), []), Admitidas,
            new ApuracaoDoEnvio(FormularioDeTeste.AvaliacaoSemOcultos, derivados, definicoes, arquivos, null), PrecondicaoIfMatch.DeTags([inscricao.ETag]), DateTimeOffset.UnixEpoch);

    private static void ConferirEstado(Inscricao inscricao, SituacaoInscricao situacao, string? numero, int revisao, FormularioDaInscricao formulario)
    {
        inscricao.Estado.Situacao.Should().Be(situacao);
        inscricao.Estado.NumeroInscricao.Should().Be(numero);
        inscricao.Revisao.Should().Be(revisao);
        inscricao.DomainEvents.Should().BeEmpty();
        inscricao.Formulario.Should().Be(formulario);
    }

    private static Inscricao NovaInscricao() => Inscricao.Iniciar(
        Guid.NewGuid(), PersonasDeTeste.FernandaDrumond, "conta-1", "https://issuer", Guid.NewGuid()).Value!;

    private static Dictionary<string, JsonElement> RespostasInvalidas() => new(StringComparer.Ordinal)
    {
        ["DESCONHECIDO"] = JsonSerializer.SerializeToElement("valor"),
        ["CAMPO"] = JsonSerializer.SerializeToElement(42),
    };

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> SemGrupos() => new(StringComparer.Ordinal);

    private sealed class OfertasQueFalhamNaSegundaConsulta : HashSet<Guid>, IReadOnlySet<Guid>
    {
        private int _consultas;

        bool IReadOnlySet<Guid>.Contains(Guid item) => ++_consultas == 1 ? true : throw new InvalidOperationException();
    }
}
