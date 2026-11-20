namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// Cada campo do contrato público do certame sai da parte certa: o DTO é posicional e vários campos têm o
/// mesmo tipo, então um valor trocado por outro compila e só estes testes o acusam.
/// </summary>
public sealed class CamposDaProjecaoDoCertameTests
{
    private static readonly Guid IdDoDocumentoDoEdital = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly string HashDoDocumentoDoEdital = new('a', 64);

    [Fact(DisplayName = "O processo, o ato criador, o nome, a versão da projeção e o hash saem de quem os trouxe, e o endereço sai do envelope")]
    public void Projetar_Cabecalho_CadaCampoSaiDaSuaOrigem()
    {
        CertamePublicadoDto certame = ProjecaoDoCertameDeTeste.Projetado();

        certame.ProcessoSeletivoId.Should().Be(ProjecaoDoCertameDeTeste.ProcessoSeletivoId);
        certame.AtoCriadorId.Should().Be(ProjecaoDoCertameDeTeste.Ato.Id);
        certame.Nome.Should().Be(ProjecaoDoCertameDeTeste.Nome);
        certame.VersaoProjecao.Should().Be(ProjecaoDoCertamePublicado.Versao);
        certame.HashConfiguracao.Should().Be(ProjecaoDoCertameDeTeste.HashDaConfiguracao);
        certame.IdentificadorLegivel.Should().Be("certame-publicacao-de-teste");
    }

    [Fact(DisplayName = "O documento do edital traz o id e o hash do envelope e o endereço da chave do edital no acervo")]
    public void Projetar_DocumentoDoEdital_TrazIdHashEEnderecoDaChave()
    {
        AtoDaDivulgacao ato = ProjecaoDoCertameDeTeste.Ato;
        string chave = ChaveNoAcervoPublico.DoEdital(
            ato.Ano, "ceps", ato.TipoCodigo, "certame-publicacao-de-teste", ato.Id, HashDoDocumentoDoEdital, FormatoDeDocumento.Pdf);

        CertamePublicadoDto certame = ProjecaoDoCertameDeTeste.Projetado();

        certame.DocumentoEdital.Should().Be(
            new DocumentoEditalCertameDto(IdDoDocumentoDoEdital, HashDoDocumentoDoEdital, ProjecaoDoCertameDeTeste.EnderecoNoAcervo(chave)));
    }

    [Fact(DisplayName = "A identidade traz o tipo do processo, o período, a localidade e a unidade administradora do envelope")]
    public void Projetar_Identidade_CadaParteSaiDoSeuBloco()
    {
        CertamePublicadoDto certame = ProjecaoDoCertameDeTeste.Projetado();

        certame.TipoProcesso.Should().Be(new TipoCatalogadoCertameDto("SiSU", "SiSU"));
        certame.Periodo.Should().Be(new PeriodoInscricaoCertameDto(
            "001/2026", new DateTimeOffset(2026, 1, 1, 3, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 1, 2, 59, 59, TimeSpan.Zero)));
        certame.Localidade.Should().Be(new LocalidadeCertameDto("1504208", "Marabá", "PA", FusoHorarioDto.Institucional));
        certame.UnidadeAdministradora.Should().Be(
            new UnidadeAdministradoraCertameDto("CEPS", "Centro de Processos Seletivos", "ADMINISTRATIVA", null, null));
    }

    [Fact(DisplayName = "A oferta traz as ofertas de curso, as opções, as modalidades e o quadro de vagas do envelope")]
    public void Projetar_Oferta_CadaParteSaiDoSeuBloco()
    {
        CertamePublicadoDto certame = ProjecaoDoCertameDeTeste.Projetado();

        certame.Ofertas.Should().ContainSingle().Which.CursoNome.Should().Be("Medicina");
        certame.OpcoesDeCurso.Should().Be(new OpcoesDeCursoCertameDto(1));
        certame.ModalidadesOfertadas.Should().Equal("AC");
        certame.Vagas.Should().ContainSingle().Which.TotalDeVagasDoEdital.Should().Be(40);
    }

    [Fact(DisplayName = "A participação traz as etapas, a origem dos candidatos, as fases, as exigências e o atendimento do envelope")]
    public void Projetar_Participacao_CadaParteSaiDoSeuBloco()
    {
        CertamePublicadoDto certame = ProjecaoDoCertameDeTeste.Projetado();

        certame.Etapas.Should().ContainSingle().Which.Nome.Should().Be("Prova Objetiva");
        certame.OrigemCandidatos.Should().Be("InscricaoPropria");
        certame.CronogramaFases.Should().ContainSingle().Which.Codigo.Should().Be("INSCRICAO");
        certame.DocumentosExigidos.Should().ContainSingle().Which.Rotulo.Should().Be("Comprovante de residência");
        certame.Atendimento.Should().BeEquivalentTo(new AtendimentoCertameDto([], [], []));
    }

    [Fact(DisplayName = "A taxa de inscrição com presença declarada traz o que o envelope congelou")]
    public void Projetar_TaxaPresente_TrazATaxaDoEnvelope()
    {
        JsonObject envelope = ProjecaoDoCertameDeTeste.Envelope();
        envelope["taxaInscricao"] = JsonNode.Parse("""{"cobra":true,"fundamentos":["Lei de teste"],"presente":true,"valor":"85.00"}""");

        CertamePublicadoDto certame = ProjecaoDoCertameDeTeste.Projetado(envelope);

        certame.TaxaInscricao.Should().BeEquivalentTo(new TaxaInscricaoCertameDto(true, "85.00", ["Lei de teste"]));
    }

    [Fact(DisplayName = "A taxa de inscrição com presença falsa vira ausência da taxa, e não um bloco de campos nulos")]
    public void Projetar_TaxaAusente_ViraNulo()
    {
        JsonObject envelope = ProjecaoDoCertameDeTeste.Envelope();
        envelope["taxaInscricao"] = JsonNode.Parse("""{"presente":false}""");

        CertamePublicadoDto certame = ProjecaoDoCertameDeTeste.Projetado(envelope);

        certame.TaxaInscricao.Should().BeNull();
    }

    [Fact(DisplayName = "A retificação ausente do envelope vira nulo")]
    public void Projetar_RetificacaoAusente_ViraNulo() =>
        ProjecaoDoCertameDeTeste.Projetado().Retificacao.Should().BeNull();

    [Fact(DisplayName = "A retificação presente no envelope é mapeada com o ato retificado e o motivo")]
    public void Projetar_RetificacaoPresente_EMapeada()
    {
        Guid atoRetificado = Guid.Parse("44444444-cccc-4ccc-8ccc-444444444444");
        JsonObject envelope = ProjecaoDoCertameDeTeste.Envelope();
        envelope["retificacao"] = JsonNode.Parse($$"""{"editalRetificadoId":"{{atoRetificado}}","motivo":"Correção do período"}""");

        CertamePublicadoDto certame = ProjecaoDoCertameDeTeste.Projetado(envelope);

        certame.Retificacao.Should().Be(new RetificacaoCertameDto(atoRetificado, "Correção do período"));
    }
}
