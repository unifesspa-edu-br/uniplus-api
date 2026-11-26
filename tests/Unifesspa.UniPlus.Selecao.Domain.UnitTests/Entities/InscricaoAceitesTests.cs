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

public sealed class InscricaoAceitesTests
{
    [Fact(DisplayName = "Salvar grava o aceite do termo na versão e no hash do formulário, e aceita a lista incompleta")]
    public void Salvar_AceiteConferido_GravaEAceitaListaIncompleta()
    {
        Inscricao inscricao = NovaInscricao();
        AceiteDeTermo lgpd = new(FormularioDeTeste.CodigoDaInscricao, TermoLgpd, VersaoDoTermoLgpd, HashDoTermoLgpd);

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(), SemGrupos(), [lgpd]), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.IsSuccess.Should().BeTrue("o rascunho não exige todos os termos; isso é do veredito de submissão");
        inscricao.Formulario.Preenchimento.Aceites.Should().ContainSingle().Which.Should().Be(lgpd);
    }

    [Fact(DisplayName = "Salvar recusa o aceite de código repetido no caminho do repetido, sem mutar nada")]
    public void Salvar_AceiteRepetido_RecusaSemMutar()
    {
        Inscricao inscricao = NovaInscricao();
        List<AceiteDeTermo> aceites =
        [
            new(FormularioDeTeste.CodigoDaInscricao, TermoLgpd, VersaoDoTermoLgpd, HashDoTermoLgpd),
            new(FormularioDeTeste.CodigoDaInscricao, TermoLgpd, Guid.CreateVersion7(), "hash-forjado"),
        ];

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(), SemGrupos(), aceites), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("aceites[1]");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.AceiteRepetido);
        inscricao.Revisao.Should().Be(1);
        inscricao.Formulario.Preenchimento.Aceites.Should().BeEmpty();
    }

    [Fact(DisplayName = "Salvar recusa o aceite de termo que a versão fixada não exige, sem mutar nada")]
    public void Salvar_AceiteForaDoFormulario_RecusaSemMutar()
    {
        Inscricao inscricao = NovaInscricao();
        List<AceiteDeTermo> aceites =
        [
            new(FormularioDeTeste.CodigoDaInscricao, TermoLgpd, VersaoDoTermoLgpd, HashDoTermoLgpd),
            new(FormularioDeTeste.CodigoDaInscricao, "TERMO_INEXISTENTE", Guid.CreateVersion7(), "hash-qualquer"),
        ];

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(), SemGrupos(), aceites), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("aceites[1]");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.AceiteForaDoFormulario);
        inscricao.Revisao.Should().Be(1);
        inscricao.Formulario.Preenchimento.Aceites.Should().BeEmpty();
    }

    [Theory(DisplayName = "Salvar recusa o aceite cuja versão ou hash não confere com o do termo exigido, sem mutar nada")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Salvar_AceiteDeOutraVersao_RecusaSemMutar(bool versaoDiferente, bool hashDiferente)
    {
        Inscricao inscricao = NovaInscricao();
        AceiteDeTermo aceite = new(
            FormularioDeTeste.CodigoDaInscricao,
            TermoLgpd,
            versaoDiferente ? Guid.CreateVersion7() : VersaoDoTermoLgpd,
            hashDiferente ? "hash-forjado" : HashDoTermoLgpd);

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(), SemGrupos(), [aceite]), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("aceites[0]");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.AceiteDeOutraVersao);
        inscricao.Revisao.Should().Be(1);
        inscricao.Formulario.Preenchimento.Aceites.Should().BeEmpty();
    }

    [Theory(DisplayName = "Submeter recusa o aceite repetido, fora do formulário ou de outra versão, sem mudar a inscrição nem emitir comprovante")]
    [InlineData(InscricaoErrorCodes.AceiteRepetido)]
    [InlineData(InscricaoErrorCodes.AceiteForaDoFormulario)]
    [InlineData(InscricaoErrorCodes.AceiteDeOutraVersao)]
    public void Submeter_AceiteInvalido_RecusaSemMutar(string codigoEsperado)
    {
        Inscricao inscricao = NovaInscricao();
        List<AceiteDeTermo> aceites = AceitesInvalidos(codigoEsperado);

        Result<ComprovanteInscricao> resultado = inscricao.Submeter("000123",
            Recebido(Respostas(), SemGrupos(), aceites), Admitidas,
            Apuracao(FormularioDeTeste.AvaliacaoSemOcultos), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("aceites[1]");
        resultado.Error!.Code.Should().Be(codigoEsperado);
        inscricao.Estado.Situacao.Should().Be(SituacaoInscricao.Rascunho);
        resultado.Value.Should().BeNull("a recusa não emite comprovante");
        inscricao.Formulario.Preenchimento.Aceites.Should().BeEmpty();
    }

    [Fact(DisplayName = "Submeter grava no comprovante os aceites conferidos com a versão e o hash do formulário")]
    public void Submeter_AceitesConferidos_ComprovanteRegistraOsAceites()
    {
        Inscricao inscricao = NovaInscricao();
        List<AceiteDeTermo> aceites =
        [
            new(FormularioDeTeste.CodigoDaInscricao, TermoLgpd, VersaoDoTermoLgpd, HashDoTermoLgpd),
            new(FormularioDeTeste.CodigoDaInscricao, TermoVeracidade, VersaoDoTermoVeracidade, HashDoTermoVeracidade),
        ];

        Result<ComprovanteInscricao> resultado = inscricao.Submeter("000123",
            Recebido(Respostas(), SemGrupos(), aceites), Admitidas,
            Apuracao(FormularioDeTeste.AvaliacaoSemOcultos), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.IsSuccess.Should().BeTrue();
        inscricao.Formulario.Preenchimento.Aceites.Should().Equal(aceites);
    }


    private static List<AceiteDeTermo> AceitesInvalidos(string codigoEsperado)
    {
        AceiteDeTermo valido = new(FormularioDeTeste.CodigoDaInscricao, TermoLgpd, VersaoDoTermoLgpd, HashDoTermoLgpd);
        return codigoEsperado switch
        {
            InscricaoErrorCodes.AceiteRepetido => [valido, valido with { HashVersao = "hash-forjado" }],
            InscricaoErrorCodes.AceiteForaDoFormulario => [valido, new(FormularioDeTeste.CodigoDaInscricao, "TERMO_INEXISTENTE", Guid.CreateVersion7(), "hash-qualquer")],
            _ => [valido, new(FormularioDeTeste.CodigoDaInscricao, TermoVeracidade, VersaoDoTermoVeracidade, "hash-forjado")],
        };

    }
}
