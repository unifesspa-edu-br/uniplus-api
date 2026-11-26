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
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static InscricaoDeTeste;

public sealed class InscricaoPreenchimentoTests
{
    [Fact(DisplayName = "Salvar recusa apagar a resposta do CPF: sem ela o rascunho não se reconstitui")]
    public void Salvar_CpfNulo_Recusa()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal)
        {
            ["CPF"] = JsonSerializer.SerializeToElement<string?>(null),
        };

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), respostas, SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "CPF" && e.Error.Code == InscricaoErrorCodes.CpfNaoPodeSerRemovido);
        inscricao.Formulario.Preenchimento.Respostas.Should().ContainKey("CPF");
    }

    [Fact(DisplayName = "Salvar sem If-Match é recusado — a precondição é obrigatória")]
    public void Salvar_SemIfMatch_RetornaFalha()
    {
        Inscricao inscricao = NovaInscricao();

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(), SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.Ausente);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.PrecondicaoRequerida);
    }

    [Fact(DisplayName = "Salvar com If-Match vencido é recusado, sem mutar nada")]
    public void Salvar_IfMatchVencido_RetornaFalhaSemMutar()
    {
        Inscricao inscricao = NovaInscricao();
        string etagOriginal = inscricao.ETag;

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(Guid.NewGuid(), null), Respostas(), SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags(["\"etag-velho\""]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.PrecondicaoFalhou);
        inscricao.ETag.Should().Be(etagOriginal, "a recusa não pode avançar a revisão");
        inscricao.Formulario.Preenchimento.Opcoes.Primeira.Should().BeNull();
    }

    [Fact(DisplayName = "Salvar com If-Match correto grava e avança a revisão")]
    public void Salvar_IfMatchCorreto_GravaEAvancaRevisao()
    {
        Inscricao inscricao = NovaInscricao();
        Guid oferta = OfertaDoProcesso;

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(oferta, null), Respostas(), SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.IsSuccess.Should().BeTrue();
        inscricao.Formulario.Preenchimento.Opcoes.Primeira.Should().Be(oferta);
        inscricao.Revisao.Should().Be(2);
        inscricao.Formulario.Preenchimento.Respostas.Should().ContainKey("NOME_SOCIAL");
    }

    [Fact(DisplayName = "Salvar sem o número de documento no corpo preserva o valor gravado")]
    public void Salvar_SemDocumentoNoCorpo_PreservaValorGravado()
    {
        Inscricao inscricao = NovaInscricao();

        Dictionary<string, JsonElement> comRg = Respostas(("RG_NUMERO", "12.868.927-4"));
        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), comRg, SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();

        Dictionary<string, JsonElement> semRg = Respostas(("NOME_SOCIAL", "Ariel Drumond"));
        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), semRg, SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();

        inscricao.Formulario.Preenchimento.Respostas.Should().ContainKey("RG_NUMERO")
            .WhoseValue.GetString().Should().Be("12.868.927-4", "ausência no corpo nunca apaga");
        inscricao.Formulario.Preenchimento.Respostas.Should().ContainKey("NOME_SOCIAL");
    }

    [Fact(DisplayName = "Submeter pela primeira vez gera o número, muda a situação e emite InscricaoSubmetidaEvent")]
    public void Submeter_PrimeiraVez_GeraNumeroEEmiteEventoDeSubmissao()
    {
        Inscricao inscricao = NovaInscricao();

        Result<ComprovanteInscricao> resultado = inscricao.Submeter("000123",
            Recebido(Respostas(), SemGrupos(), SemAceites()), Admitidas,
            Apuracao(FormularioDeTeste.AvaliacaoSemOcultos), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.IsSuccess.Should().BeTrue();
        inscricao.Estado.Situacao.Should().Be(SituacaoInscricao.Enviada);
        inscricao.Estado.NumeroInscricao.Should().Be("000123");
        resultado.Value!.NumeroInscricao.Should().Be("000123");
        inscricao.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<InscricaoSubmetidaEvent>();
    }

    [Fact(DisplayName = "Submeter de novo (correção) preserva o número, gera comprovante novo e emite InscricaoCorrigidaEvent")]
    public void Submeter_Correcao_PreservaNumeroEEmiteEventoDeCorrecao()
    {
        Inscricao inscricao = NovaInscricao();
        Result<ComprovanteInscricao> primeiro = inscricao.Submeter("000123",
            Recebido(Respostas(), SemGrupos(), SemAceites()), Admitidas,
            Apuracao(FormularioDeTeste.AvaliacaoSemOcultos), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());
        inscricao.ClearDomainEvents();

        Result<ComprovanteInscricao> resultado = inscricao.Submeter("999999",
            Recebido(Respostas(), SemGrupos(), SemAceites()), Admitidas,
            Apuracao(FormularioDeTeste.AvaliacaoSemOcultos), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.IsSuccess.Should().BeTrue();
        inscricao.Estado.NumeroInscricao.Should().Be("000123", "o número gerado só vale se ainda não existir");
        resultado.Value.Should().NotBeSameAs(primeiro.Value, "a correção emite um comprovante novo, nunca reaproveita o anterior");
        inscricao.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<InscricaoCorrigidaEvent>();
    }

    [Fact(DisplayName = "Depois do envio, salvar rascunho é recusado sem mutar: só um novo envio muda a inscrição")]
    public void Salvar_DepoisDoEnvio_RecusaSemMutar()
    {
        Inscricao inscricao = NovaInscricao();
        inscricao.Submeter("000123",
            Recebido(Respostas(), SemGrupos(), SemAceites()), Admitidas,
            Apuracao(FormularioDeTeste.AvaliacaoSemOcultos), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());
        int revisao = inscricao.Revisao;

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(OfertaDoProcesso, null), Respostas(), SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.RascunhoSoAntesDoEnvio);
        inscricao.Revisao.Should().Be(revisao);
    }

    [Fact(DisplayName = "Salvar recusa resposta de fato que o formulário não coleta, sem mutar nada")]
    public void Salvar_FatoForaDoFormulario_RecusaSemMutar()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> respostas = Respostas(("CAMPO_INVENTADO", "valor"));

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), respostas, SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("CAMPO_INVENTADO");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.FatoForaDoFormulario);
        inscricao.Revisao.Should().Be(1);
    }

    [Fact(DisplayName = "Salvar recusa valor que não cabe no tipo do fato")]
    public void Salvar_ValorForaDoTipo_Recusa()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> respostas = Respostas(("DESEJA_NOME_SOCIAL", "sim"));

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), respostas, SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ValorForaDoTipo);
    }

    [Fact(DisplayName = "Salvar com nulo apaga a resposta que o candidato limpou")]
    public void Salvar_Nulo_ApagaResposta()
    {
        Inscricao inscricao = NovaInscricao();
        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(), SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));
        Dictionary<string, JsonElement> limpa = new(StringComparer.Ordinal)
        {
            ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement<string?>(null),
        };

        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), limpa, SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();

        inscricao.Formulario.Preenchimento.Respostas.Should().NotContainKey("NOME_SOCIAL");
    }

    [Fact(DisplayName = "Submeter recusa o fato derivado informado pelo candidato")]
    public void Submeter_FatoDerivadoInformadoPeloCandidato_Recusa()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> respostas = Respostas(("UF_RESIDENCIA", "PA"));

        Result<ComprovanteInscricao> resultado = inscricao.Submeter("000123",
            Recebido(respostas, SemGrupos(), SemAceites()), Admitidas,
            Apuracao(FormularioDeTeste.AvaliacaoSemOcultos), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.FatoForaDoFormulario);
    }

    [Fact(DisplayName = "Submeter separa os derivados das respostas, na inscrição e no comprovante")]
    public void Submeter_SeparaDerivadosDasRespostas()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> derivados = Respostas(("UF_RESIDENCIA", "PA"));

        Result<ComprovanteInscricao> resultado = inscricao.Submeter("000123",
            Recebido(Respostas(), SemGrupos(), SemAceites()), Admitidas,
            new ApuracaoDoEnvio(FormularioDeTeste.AvaliacaoSemOcultos, derivados, DefinicoesDosDerivados, [], null), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        inscricao.Formulario.Derivados.Should().ContainKey("UF_RESIDENCIA");
        inscricao.Formulario.Preenchimento.Respostas.Should().NotContainKey("UF_RESIDENCIA");
        resultado.Value!.Derivados.Should().ContainKey("UF_RESIDENCIA");
        resultado.Value!.Respostas.Should().ContainKey("NOME_SOCIAL").And.NotContainKey("UF_RESIDENCIA");
    }

    [Theory(DisplayName = "A resposta do CPF é aceita só quando é o CPF do dono, com ou sem pontuação")]
    [InlineData("69959836371", true)]
    [InlineData("699.598.363-71", true)]
    [InlineData("33257350376", false)]
    public void Salvar_CpfDaResposta_TemDeSerODoDono(string cpfRespondido, bool aceito)
    {
        Inscricao inscricao = Inscricao.Iniciar(
            Processo, PersonasDeTeste.FernandaDrumond, "conta-1", "https://issuer", VersaoFormulario).Value!;
        Dictionary<string, JsonElement> respostas = Respostas(("CPF", cpfRespondido));

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), respostas, SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.IsSuccess.Should().Be(aceito);
        if (!aceito)
        {
            resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
                e => e.Field == "CPF" && e.Error.Code == InscricaoErrorCodes.CpfDiferenteDoDono);
        }
    }

    [Fact(DisplayName = "O documento obrigatório ausente recusa o envio junto das demais recusas, sem mudar a inscrição")]
    public void Submeter_DocumentoObrigatorioAusente_RecusaJuntoDasDemais()
    {
        Inscricao inscricao = NovaInscricao();
        DocumentoExigido laudo = new DocumentoExigidoBuilder().NaFase(Guid.CreateVersion7(), FinalidadeFormulario.Inscricao).Construir();
        ResultadoDaAvaliacaoDaInscricao avaliacao = ComModalidades(SituacaoDasModalidades.FaltaResponder);
        avaliacao = avaliacao with { Documentos = avaliacao.Documentos with { Ausentes = [new DocumentoAusente([laudo], null)] } };

        Result<ComprovanteInscricao> resultado = SubmeterCom(inscricao, avaliacao);

        resultado.Errors.Select(static e => e.Error.Code).Should().BeEquivalentTo(
            [InscricaoErrorCodes.ModalidadeNaoDecidida, InscricaoErrorCodes.DocumentoObrigatorioAusente]);
        resultado.Errors.Should().Contain(e => e.Field == $"documentos.{laudo.Id}");
        inscricao.Estado.Situacao.Should().Be(SituacaoInscricao.Rascunho);
    }
}
