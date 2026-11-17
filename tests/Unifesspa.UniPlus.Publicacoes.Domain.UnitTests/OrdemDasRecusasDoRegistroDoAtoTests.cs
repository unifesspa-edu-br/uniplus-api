namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;


using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A ordem em que <c>AtoNormativo.Registrar</c> confere as invariantes é contrato: quando um
/// registro viola duas, a exceção aponta a primeira da sequência — órgão, série, tipo, assinante,
/// número, motivo da retificação, nome do tipo, ano, hash do documento, ato retificado vazio,
/// simetria do par de retificação, vínculos (tipo, identificador, repetição) e anexos. Cada linha de
/// dados viola um passo e o seguinte; a fábrica é chamada com os argumentos à vista porque é ela o assunto.
/// </summary>
public sealed class OrdemDasRecusasDoRegistroDoAtoTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Assinante = PersonasDeTeste.NomeKevinCarvalho;
    private static readonly DateOnly DataDoAto = new(2026, 3, 13);
    private static readonly DateTimeOffset PublicadoEm = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);
    private static readonly RegistroComViolacoes Valido = new();
    private static readonly (string, Guid) Processo = ("PROCESSO", Guid.CreateVersion7());
    private static readonly (string, Guid) ProcessoSemId = ("PROCESSO", Guid.Empty);
    private static readonly (string, string, string) AnexoSemHash = ("Anexo I.pdf", "application/pdf", "abc");

    public static TheoryData<string, RegistroComViolacoes, Type, string, string> TextosObrigatorios => new()
    {
        { "órgão nulo antes da série", Valido with { Orgao = null!, Serie = Texto(101) }, typeof(ArgumentNullException), "orgao", "" },
        { "órgão em branco antes da série", Valido with { Orgao = "  ", Serie = Texto(101) }, typeof(ArgumentException), "orgao", "" },
        { "órgão longo antes da série", Valido with { Orgao = Texto(201), Serie = Texto(101) }, typeof(ArgumentException), "orgao", "" },
        { "série antes do tipo", Valido with { Serie = Texto(101), TipoCodigo = Texto(61) }, typeof(ArgumentException), "serie", "" },
        { "tipo antes do assinante", Valido with { TipoCodigo = Texto(61), Assinante = Texto(201) }, typeof(ArgumentException), "tipoCodigo", "" },
        { "assinante antes do número", Valido with { Assinante = Texto(201), Numero = Texto(61) }, typeof(ArgumentException), "assinante", "" },
    };

    public static TheoryData<string, RegistroComViolacoes, Type, string, string> TextosOpcionaisAnoEHash => new()
    {
        { "número antes do motivo", Valido with { Numero = Texto(61), MotivoRetificacao = Texto(1001) }, typeof(ArgumentException), "numero", "" },
        { "motivo antes do nome do tipo", Valido with { MotivoRetificacao = Texto(1001), TipoNome = Texto(201) }, typeof(ArgumentException), "motivoRetificacao", "" },
        { "nome do tipo antes do ano", Valido with { TipoNome = Texto(201), Ano = 0 }, typeof(ArgumentException), "tipoNome", "" },
        { "ano antes do hash", Valido with { Ano = 0, DocumentoHash = "abc" }, typeof(ArgumentOutOfRangeException), "ano", "" },
        { "hash antes do ato retificado vazio", Valido with { DocumentoHash = "abc", AtoRetificadoId = Guid.Empty, MotivoRetificacao = "corrige" }, typeof(ArgumentException), "documentoHash", "" },
    };

    public static TheoryData<string, RegistroComViolacoes, Type, string, string> RetificacaoVinculosEAnexos => new()
    {
        { "ato retificado vazio antes da simetria", Valido with { AtoRetificadoId = Guid.Empty }, typeof(ArgumentException), "atoRetificadoId", "Identificador do ato retificado" },
        { "simetria antes dos vínculos", Valido with { MotivoRetificacao = "corrige", Vinculos = [("x", Guid.Empty)] }, typeof(ArgumentException), "atoRetificadoId", "par (ato retificado, motivo)" },
        { "tipo do vínculo antes do identificador", Valido with { Vinculos = [("x", Guid.Empty)] }, typeof(ArgumentException), "entidadeTipo", "" },
        { "identificador do vínculo antes da repetição", Valido with { Vinculos = [ProcessoSemId, ProcessoSemId] }, typeof(ArgumentException), "entidadeId", "" },
        { "repetição do vínculo antes dos anexos", Valido with { Vinculos = [Processo, Processo], Anexos = [AnexoSemHash] }, typeof(ArgumentException), "vinculos", "" },
        { "tipo do vínculo antes dos anexos", Valido with { Vinculos = [("x", Guid.CreateVersion7())], Anexos = [AnexoSemHash] }, typeof(ArgumentException), "entidadeTipo", "" },
    };

    [Theory(DisplayName = "Com duas invariantes violadas, a exceção aponta a primeira da sequência")]
    [MemberData(nameof(TextosObrigatorios))]
    [MemberData(nameof(TextosOpcionaisAnoEHash))]
    [MemberData(nameof(RetificacaoVinculosEAnexos))]
    public void Registrar_ComDuasInvariantesVioladas_RecusaAPrimeiraDaSequencia(
        string descricao, RegistroComViolacoes r, Type excecao, string parametro, string trechoDaMensagem)
    {
        Exception? recusa = Record.Exception(() => AtoNormativo.Registrar(
            Guid.CreateVersion7(), r.Orgao, r.Serie, r.Ano, r.Numero, r.TipoCodigo,
            congelaConfiguracao: false, efeitoIrreversivel: false, unicoPorObjeto: false,
            DataDoAto, r.DocumentoHash, r.Assinante, PublicadoEm, versaoDoObjeto: null,
            r.AtoRetificadoId, r.MotivoRetificacao, r.Vinculos, r.TipoNome, r.Anexos));

        recusa.Should().BeOfType(excecao, descricao);
        ((ArgumentException)recusa!).ParamName.Should().Be(parametro, descricao);
        recusa.Message.Contains(trechoDaMensagem, StringComparison.Ordinal).Should().BeTrue(descricao);
    }

    [Fact(DisplayName = "O identificador informado é mantido")]
    public void Registrar_ComIdInformado_Mantem()
    {
        Guid id = Guid.CreateVersion7();

        AtoNormativo ato = AtoNormativo.Registrar(
            id, "CEPS", "EDITAL", 2026, "13", "EDITAL_ABERTURA",
            congelaConfiguracao: false, efeitoIrreversivel: false, unicoPorObjeto: false,
            DataDoAto, HashValido, Assinante, PublicadoEm, versaoDoObjeto: null);

        ato.Id.Should().Be(id);
    }

    [Fact(DisplayName = "Identificador vazio faz a fábrica gerar um novo, diferente a cada registro")]
    public void Registrar_ComIdVazio_GeraNovoId()
    {
        AtoNormativo primeiro = AtoNormativo.Registrar(
            Guid.Empty, "CEPS", "EDITAL", 2026, "13", "EDITAL_ABERTURA",
            congelaConfiguracao: false, efeitoIrreversivel: false, unicoPorObjeto: false,
            DataDoAto, HashValido, Assinante, PublicadoEm, versaoDoObjeto: null);
        AtoNormativo segundo = AtoNormativo.Registrar(
            Guid.Empty, "CEPS", "EDITAL", 2026, "13", "EDITAL_ABERTURA",
            congelaConfiguracao: false, efeitoIrreversivel: false, unicoPorObjeto: false,
            DataDoAto, HashValido, Assinante, PublicadoEm, versaoDoObjeto: null);

        primeiro.Id.Should().NotBe(Guid.Empty);
        segundo.Id.Should().NotBe(Guid.Empty).And.NotBe(primeiro.Id);
    }

    [Fact(DisplayName = "A mesma entidade repetida é recusada mesmo com outro vínculo entre as duas ocorrências")]
    public void Registrar_MesmaEntidadeEmOutraOrdem_Recusa()
    {
        Guid processo = Guid.CreateVersion7();

        Action acao = () => AtoNormativo.Registrar(
            Guid.CreateVersion7(), "CEPS", "EDITAL", 2026, "13", "EDITAL_ABERTURA",
            congelaConfiguracao: false, efeitoIrreversivel: false, unicoPorObjeto: false,
            DataDoAto, HashValido, Assinante, PublicadoEm, versaoDoObjeto: null,
            vinculos: [("PROCESSO", processo), ("CHAMADA", Guid.CreateVersion7()), ("PROCESSO", processo)]);

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("vinculos");
    }

    [Fact(DisplayName = "Os anexos ficam na ordem de envio, numerados a partir de zero")]
    public void Registrar_ComAnexos_GuardaNaOrdemDeEnvio()
    {
        AtoNormativo ato = AtoNormativo.Registrar(
            Guid.CreateVersion7(), "CEPS", "EDITAL", 2026, "13", "EDITAL_ABERTURA",
            congelaConfiguracao: false, efeitoIrreversivel: false, unicoPorObjeto: false,
            DataDoAto, HashValido, Assinante, PublicadoEm, versaoDoObjeto: null,
            anexos: [("B.pdf", "application/pdf", HashValido), ("A.pdf", "application/pdf", HashValido)]);

        ato.Anexos.OrderBy(a => a.Ordem).Select(a => (a.Ordem, a.Nome)).Should().Equal((0, "B.pdf"), (1, "A.pdf"));
    }

    private static string Texto(int tamanho) => new('x', tamanho);

    /// <summary>Os argumentos de um registro válido; cada linha de dados troca só os que viola.</summary>
    public sealed record RegistroComViolacoes
    {
        public string Orgao { get; init; } = "CEPS";

        public string Serie { get; init; } = "EDITAL";

        public int Ano { get; init; } = 2026;

        public string? Numero { get; init; } = "13";

        public string TipoCodigo { get; init; } = "EDITAL_ABERTURA";

        public string DocumentoHash { get; init; } = HashValido;

        public string Assinante { get; init; } = OrdemDasRecusasDoRegistroDoAtoTests.Assinante;

        public Guid? AtoRetificadoId { get; init; }

        public string? MotivoRetificacao { get; init; }

        public string? TipoNome { get; init; }

        public IEnumerable<(string EntidadeTipo, Guid EntidadeId)>? Vinculos { get; init; }

        public IEnumerable<(string Nome, string TipoMidia, string Hash)>? Anexos { get; init; }
    }
}
