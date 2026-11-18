namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static InscricaoDeTeste;

public sealed class InscricaoValoresTests
{
    [Fact(DisplayName = "Salvar grava o telefone só com os dígitos; o texto fora do formato fica como veio, para o envio recusá-lo")]
    public void Salvar_Telefone_GravaNaFormaCanonica()
    {
        Inscricao inscricao = NovaInscricao();

        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), ComTelefone("(94) 3322-1234"), SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();
        inscricao.Formulario.Preenchimento.Respostas["TELEFONE"].GetString().Should().Be("9433221234");

        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), ComTelefone("(94) 3322"), SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();
        inscricao.Formulario.Preenchimento.Respostas["TELEFONE"].GetString().Should().Be("(94) 3322");
    }

    [Fact(DisplayName = "Submeter grava e assina no comprovante o telefone só com os dígitos, também dentro de um grupo")]
    public void Submeter_Telefone_ComprovanteCobreAFormaCanonica()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = FamiliaComTelefone();

        Result<ComprovanteInscricao> resultado = inscricao.Submeter("000123",
            Recebido(ComTelefone("(94) 3322-1234"), grupos, SemAceites()), Admitidas,
            Apuracao(FormularioDeTeste.AvaliacaoSemOcultos), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        ComprovanteInscricao comprovante = resultado.Value!;
        inscricao.Formulario.Preenchimento.Respostas["TELEFONE"].GetString().Should().Be("9433221234");
        comprovante.Respostas["TELEFONE"].GetString().Should().Be("9433221234");
        comprovante.Grupos["COMPOSICAO_FAMILIAR"].Should().ContainSingle()
            .Which.Respostas["TELEFONE"].GetString().Should().Be("94991234567");
        comprovante.IntegridadeConfere().Should().BeTrue("o hash é calculado sobre o que o comprovante guarda");
    }

    [Fact(DisplayName = "Submeter município sem nome conferido lança e preserva a situação e a revisão")]
    public void Submeter_MunicipioSemNomeConferido_MantemRascunhoComNumero()
    {
        Inscricao inscricao = NovaInscricao();
        int revisao = inscricao.Revisao;

        Action submeter = () => inscricao.Submeter("000123",
            Recebido(Respostas(), SemGrupos(), SemAceites()), Admitidas,
            ComMunicipioSemNome(), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        submeter.Should().Throw<InvalidOperationException>().WithMessage("*não foi conferido no Geo*");
        inscricao.Estado.Situacao.Should().Be(SituacaoInscricao.Rascunho);
        inscricao.Estado.NumeroInscricao.Should().Be("000123");
        inscricao.Formulario.Preenchimento.Opcoes.Primeira.Should().Be(OfertaDoProcesso);
        inscricao.Revisao.Should().Be(revisao);
        inscricao.DomainEvents.Should().BeEmpty();
    }

    [Theory(DisplayName = "Endereço sem CEP, ou com CEP que não tem 8 dígitos, é recusado ao salvar com a orientação")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("7909673")]
    [InlineData("7909673A")]
    public void Salvar_EnderecoSemCepValido_Recusa(string? cep)
    {
        Inscricao inscricao = NovaInscricao();

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), EnderecoResidencial(cep, "Campo Grande"), SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "ENDERECO_RESIDENCIAL" && e.Error.Code == InscricaoErrorCodes.EnderecoSemCep
                && e.Error.Message.Contains("CEP geral da sua cidade", StringComparison.Ordinal));
    }

    [Theory(DisplayName = "O CEP com a máscara ou sem ela é aceito")]
    [InlineData("79096-730")]
    [InlineData("79096730")]
    public void Salvar_EnderecoComCep_Aceita(string cep)
    {
        Inscricao inscricao = NovaInscricao();

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), EnderecoResidencial(cep, "Campo Grande"), SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "O endereço em branco não é um endereço sem CEP: é a resposta ainda não dada")]
    public void Salvar_EnderecoEmBranco_Aceita()
    {
        Inscricao inscricao = NovaInscricao();

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), EnderecoResidencial(null, null), SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "O complemento de até 300 caracteres é aceito e o de 301 é recusado, ao salvar e ao enviar")]
    public void Complemento_LimiteDe300Caracteres()
    {
        Inscricao salvando = NovaInscricao();
        Result noLimite = salvando.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), EnderecoResidencial("79096730", "Campo Grande", new string('a', 300)), SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([salvando.ETag]));
        Result acima = salvando.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), EnderecoResidencial("79096730", "Campo Grande", new string('a', 301)), SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([salvando.ETag]));

        noLimite.IsSuccess.Should().BeTrue();
        acima.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "ENDERECO_RESIDENCIAL" && e.Error.Code == InscricaoErrorCodes.EnderecoComplementoLongo);

    }

    [Fact(DisplayName = "O envio recusa complemento com 301 caracteres")]
    public void Submeter_ComplementoAcimaDoLimite_Recusa()
    {
        Inscricao enviando = NovaInscricao();
        Result<ComprovanteInscricao> enviado = enviando.Submeter("000123",
            Recebido(EnderecoResidencial("79096730", "Campo Grande", new string('a', 301)), SemGrupos(), SemAceites()), Admitidas,
            Apuracao(FormularioDeTeste.AvaliacaoSemOcultos), PrecondicaoIfMatch.DeTags([enviando.ETag]), Instante());

        enviado.Errors.Should().ContainSingle(e => e.Error.Code == InscricaoErrorCodes.EnderecoComplementoLongo);
    }

    private static ApuracaoDoEnvio ComMunicipioSemNome() =>
        new(FormularioDeTeste.AvaliacaoSemOcultos, Respostas(("MUNICIPIO_RESIDENCIA", "2927408")),
            new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal)
            {
                ["MUNICIPIO_RESIDENCIA"] = new(TipoDoValorDeResposta.Municipio, Multivalorado: false, Identificador: false),
            }, []);

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> FamiliaComTelefone() =>
         new(StringComparer.Ordinal)
         {
             ["COMPOSICAO_FAMILIAR"] = [new OcorrenciaDeGrupoInscricao("1", ComTelefone("(94) 99123-4567"))],
         };

}
