namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As guardas finais de <c>AtoNormativo.Registrar</c> lançam <see cref="ArgumentException"/> com o campo da
/// primeira recusa, na ordem do corpo do pedido: órgão, série, ano, número, código do tipo, nome do
/// tipo, hash do documento, assinante, ato retificado, motivo da retificação, vínculos (por posição) e
/// anexos. Cada linha de dados viola um passo e o seguinte; a fábrica é chamada com os argumentos à vista
/// porque é ela o assunto.
/// </summary>
public sealed class OrdemDasRecusasDoRegistroDoAtoTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Assinante = PersonasDeTeste.NomeKevinCarvalho;
    private static readonly DateOnly DataDoAto = new(2026, 3, 13);
    private static readonly DateTimeOffset PublicadoEm = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);
    private static readonly RegistroComViolacoes Valido = new();
    private static readonly EntidadeVinculada Processo = new("PROCESSO", Guid.CreateVersion7());
    private static readonly EntidadeVinculada ProcessoSemId = new("PROCESSO", Guid.Empty);
    private static readonly (string, string, string) AnexoSemHash = ("Anexo I.pdf", "application/pdf", "abc");

    public static TheoryData<string, RegistroComViolacoes, string> Essencia => new()
    {
        { "órgão nulo antes da série", Valido with { Orgao = null!, Serie = Texto(101) }, "orgao" },
        { "órgão em branco antes da série", Valido with { Orgao = "  ", Serie = Texto(101) }, "orgao" },
        { "órgão longo antes da série", Valido with { Orgao = Texto(201), Serie = Texto(101) }, "orgao" },
        { "série antes do ano", Valido with { Serie = Texto(101), Ano = 0 }, "serie" },
        { "ano antes do número", Valido with { Ano = 0, Numero = Texto(61) }, "ano" },
        { "número antes do código do tipo", Valido with { Numero = Texto(61), TipoCodigo = Texto(61) }, "numero" },
        { "código do tipo antes do nome do tipo", Valido with { TipoCodigo = Texto(61), TipoNome = Texto(201) }, "tipoCodigo" },
        { "nome do tipo antes do hash", Valido with { TipoNome = Texto(201), DocumentoHash = "abc" }, "tipoNome" },
        { "hash antes do assinante", Valido with { DocumentoHash = "abc", Assinante = Texto(201) }, "documentoHash" },
        { "assinante antes do ato retificado", Valido with { Assinante = Texto(201), AtoRetificadoId = Guid.Empty }, "assinante" },
    };

    public static TheoryData<string, RegistroComViolacoes, string> RetificacaoVinculosEAnexos => new()
    {
        { "ato retificado vazio antes do motivo", Valido with { AtoRetificadoId = Guid.Empty, MotivoRetificacao = Texto(1001) }, "atoRetificadoId" },
        { "motivo antes dos vínculos", Valido with { MotivoRetificacao = Texto(1001), AtoRetificadoId = Guid.CreateVersion7(), Vinculos = [ProcessoSemId] }, "motivoRetificacao" },
        { "tipo do vínculo antes do identificador", Valido with { Vinculos = [new("x", Guid.Empty)] }, "vinculos[0].entidadeTipo" },
        { "identificador do vínculo antes da repetição", Valido with { Vinculos = [ProcessoSemId, ProcessoSemId] }, "vinculos[0].entidadeId" },
        { "repetição do vínculo antes dos anexos", Valido with { Vinculos = [Processo, Processo], Anexos = [AnexoSemHash] }, "vinculos[1]" },
        { "segundo vínculo inválido depois de um válido", Valido with { Vinculos = [Processo, new("x", Guid.CreateVersion7())] }, "vinculos[1].entidadeTipo" },
        { "vínculo antes dos anexos", Valido with { Vinculos = [new("x", Guid.CreateVersion7())], Anexos = [AnexoSemHash] }, "vinculos[0].entidadeTipo" },
        { "anexo inválido", Valido with { Anexos = [AnexoSemHash] }, "anexos[0].hash" },
    };

    [Theory(DisplayName = "Com duas invariantes violadas, a exceção aponta o campo da primeira na ordem do corpo")]
    [MemberData(nameof(Essencia))]
    [MemberData(nameof(RetificacaoVinculosEAnexos))]
    public void Registrar_ComDuasInvariantesVioladas_RecusaAPrimeiraDaSequencia(
        string descricao, RegistroComViolacoes r, string campo)
    {
        Exception? recusa = Record.Exception(() => AtoNormativo.Registrar(
            Guid.CreateVersion7(), EssenciaDe(r), PublicadoEm, versaoDoObjeto: null, r.Vinculos, r.Anexos));

        recusa.Should().BeOfType<ArgumentException>(descricao);
        ((ArgumentException)recusa!).ParamName.Should().Be(campo, descricao);
    }

    [Fact(DisplayName = "O identificador informado é mantido")]
    public void Registrar_ComIdInformado_Mantem()
    {
        Guid id = Guid.CreateVersion7();

        AtoNormativo ato = AtoNormativo.Registrar(id, EssenciaDe(Valido), PublicadoEm, versaoDoObjeto: null);

        ato.Id.Should().Be(id);
    }

    [Fact(DisplayName = "Identificador vazio faz a fábrica gerar um novo, diferente a cada registro")]
    public void Registrar_ComIdVazio_GeraNovoId()
    {
        AtoNormativo primeiro = AtoNormativo.Registrar(Guid.Empty, EssenciaDe(Valido), PublicadoEm, versaoDoObjeto: null);
        AtoNormativo segundo = AtoNormativo.Registrar(Guid.Empty, EssenciaDe(Valido), PublicadoEm, versaoDoObjeto: null);

        primeiro.Id.Should().NotBe(Guid.Empty);
        segundo.Id.Should().NotBe(Guid.Empty).And.NotBe(primeiro.Id);
    }

    [Fact(DisplayName = "A mesma entidade repetida é recusada mesmo com outro vínculo entre as duas ocorrências")]
    public void Registrar_MesmaEntidadeEmOutraOrdem_Recusa()
    {
        Guid processo = Guid.CreateVersion7();

        Action acao = () => AtoNormativo.Registrar(
            Guid.CreateVersion7(), EssenciaDe(Valido), PublicadoEm, versaoDoObjeto: null,
            vinculos: [new("PROCESSO", processo), new("CHAMADA", Guid.CreateVersion7()), new("PROCESSO", processo)]);

        acao.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("vinculos[2]");
    }

    [Fact(DisplayName = "Os anexos ficam na ordem de envio, numerados a partir de zero")]
    public void Registrar_ComAnexos_GuardaNaOrdemDeEnvio()
    {
        AtoNormativo ato = AtoNormativo.Registrar(
            Guid.CreateVersion7(), EssenciaDe(Valido), PublicadoEm, versaoDoObjeto: null,
            anexos: [("B.pdf", "application/pdf", HashValido), ("A.pdf", "application/pdf", HashValido)]);

        ato.Anexos.OrderBy(a => a.Ordem).Select(a => (a.Ordem, a.Nome)).Should().Equal((0, "B.pdf"), (1, "A.pdf"));
    }

    private static EssenciaDocumentalDoAto EssenciaDe(RegistroComViolacoes r) =>
        new(
            new NumeracaoDoAto(r.Orgao, r.Serie, r.Ano, r.Numero),
            new TipoDoAtoNoRegistro(r.TipoCodigo, r.TipoNome, CongelaConfiguracao: false, EfeitoIrreversivel: false, UnicoPorObjeto: false),
            DataDoAto,
            r.DocumentoHash,
            r.Assinante,
            new RelacaoDeRetificacao(r.AtoRetificadoId, r.MotivoRetificacao));

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

        public IEnumerable<EntidadeVinculada>? Vinculos { get; init; }

        public IEnumerable<(string Nome, string TipoMidia, string Hash)>? Anexos { get; init; }
    }
}
