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

public sealed class InscricaoComprovantesTests
{
    [Fact(DisplayName = "A resposta do CPF segue cifrada como identificador ao salvar e ao enviar, mesmo com o fato congelado como pessoal")]
    public void CpfReclassificadoComoPessoal_ContinuaCifradoENaoVaiAoComprovante()
    {
        RespostasAdmitidas comoPessoal = ComCpfClassificadoComoPessoal();
        Inscricao inscricao = NovaInscricao();

        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(), SemGrupos(), SemAceites()), comoPessoal, PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();
        inscricao.Formulario.DefinicoesDasRespostas["CPF"].Should().Match<DefinicaoDaResposta>(d => d.Identificador && d.Cifrada);

        Result<ComprovanteInscricao> envio = inscricao.Submeter("000123",
            Recebido(Respostas(), SemGrupos(), SemAceites()), comoPessoal,
            Apuracao(FormularioDeTeste.AvaliacaoSemOcultos), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        envio.IsSuccess.Should().BeTrue();
        inscricao.Formulario.DefinicoesDasRespostas["CPF"].Identificador.Should().BeTrue();
        envio.Value!.Respostas.Should().NotContainKey("CPF", "o número de documento nunca vai ao comprovante");
    }

    [Fact(DisplayName = "O comprovante nunca carrega número de documento, mesmo que a inscrição carregue")]
    public void Submeter_Comprovante_NaoCarregaDocumento()
    {
        Inscricao inscricao = NovaInscricao();

        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["RG_NUMERO"] = JsonSerializer.SerializeToElement("12.868.927-4"),
            ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement("Ariel Drumond"),
        };

        Result<ComprovanteInscricao> resultado = inscricao.Submeter("000123",
            Recebido(respostas, SemGrupos(), SemAceites()), Admitidas,
            Apuracao(FormularioDeTeste.AvaliacaoSemOcultos), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.Value!.Respostas.Should().NotContainKey("RG_NUMERO");
        resultado.Value!.Respostas.Should().ContainKey("NOME_SOCIAL");
    }

    [Fact(DisplayName = "O comprovante leva o dado de cifra individual à parte, fora do JSON em claro e do hash, e o sexo civil segue em claro")]
    public void Submeter_Comprovante_LevaOCifradoIndividualAParte()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> respostas = Respostas(("NOME_SOCIAL", "Ariel Drumond"), ("SEXO_CIVIL", "FEMININO"), ("IDENTIDADE_GENERO", "PESSOA_NAO_BINARIA"));

        Result<ComprovanteInscricao> resultado = inscricao.Submeter("000123",
            Recebido(respostas, SemGrupos(), SemAceites()), Admitidas,
            Apuracao(FormularioDeTeste.AvaliacaoSemOcultos), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        ComprovanteInscricao comprovante = resultado.Value!;
        comprovante.Respostas.Should().NotContainKey("IDENTIDADE_GENERO");
        comprovante.Respostas["SEXO_CIVIL"].GetString().Should().Be("FEMININO");
        comprovante.RespostasSensiveis["IDENTIDADE_GENERO"].GetString().Should().Be("PESSOA_NAO_BINARIA");
        comprovante.IntegridadeConfere().Should().BeTrue();
        inscricao.Formulario.Preenchimento.Respostas["IDENTIDADE_GENERO"].GetString().Should().Be("PESSOA_NAO_BINARIA", "a inscrição guarda o valor; a cifra é da gravação");
    }

    [Fact(DisplayName = "O comprovante carrega os grupos declarados, sem o documento de uma ocorrência")]
    public void Submeter_Comprovante_CarregaGruposSemDocumentoDaOcorrencia()
    {
        Inscricao inscricao = NovaInscricao();

        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = FamiliaComMembro(("RG_NUMERO", "25.062.156-3"), ("PARENTESCO", "FILHO"));

        Result<ComprovanteInscricao> resultado = inscricao.Submeter("000123",
            Recebido(Respostas(), grupos, SemAceites()), Admitidas,
            Apuracao(FormularioDeTeste.AvaliacaoSemOcultos), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias = resultado.Value!.Grupos.Should()
            .ContainKey("COMPOSICAO_FAMILIAR").WhoseValue;
        ocorrencias.Should().ContainSingle().Which.Id.Should().Be("membro-1");
        ocorrencias.Single().Respostas.Should().NotContainKey("RG_NUMERO");
        ocorrencias.Single().Respostas.Should().ContainKey("PARENTESCO");
    }

    private static RespostasAdmitidas ComCpfClassificadoComoPessoal() =>
        Admitidas with
        {
            Definicoes = new Dictionary<string, DefinicaoDaResposta>(Admitidas.Definicoes, StringComparer.Ordinal)
            {
                ["CPF"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: false),
            },
        };

}
