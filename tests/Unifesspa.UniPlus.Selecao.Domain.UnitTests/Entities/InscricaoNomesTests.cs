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

public sealed class InscricaoNomesTests
{
    [Fact(DisplayName = "Um NOME diferente do nome do login é recusado ao salvar, no campo NOME")]
    public void Salvar_NomeDiferenteDoLogin_Recusa()
    {
        Result resultado = SalvarComNome(PersonasDeTeste.NomeFernandaDrumond, ("NOME", "Outra Pessoa"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "NOME" && e.Error.Code == InscricaoErrorCodes.NomeDiferenteDoLogin);
    }

    [Fact(DisplayName = "Um NOME diferente do nome do login é recusado ao enviar, no campo NOME")]
    public void Submeter_NomeDiferenteDoLogin_Recusa()
    {
        Inscricao inscricao = NovaInscricao();

        Result<ComprovanteInscricao> resultado = inscricao.Submeter("000123",
            Recebido(Respostas(("NOME", "Outra Pessoa")), SemGrupos(), SemAceites()), AdmitidasComNome(PersonasDeTeste.NomeFernandaDrumond),
            Apuracao(FormularioDeTeste.AvaliacaoSemOcultos), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "NOME" && e.Error.Code == InscricaoErrorCodes.NomeDiferenteDoLogin);
    }

    [Fact(DisplayName = "Um rascunho com NOME divergente do login, enviado sem NOME, é recusado no campo NOME")]
    public void Submeter_RascunhoComNomeDivergente_SemNomeNoEnvio_Recusa()
    {
        Inscricao inscricao = NovaInscricao();
        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(("NOME", "Outra Pessoa")), SemGrupos(), SemAceites()), AdmitidasComNome("Outra Pessoa"), PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();

        Result<ComprovanteInscricao> resultado = inscricao.Submeter("000123",
            Recebido(Respostas(), SemGrupos(), SemAceites()), AdmitidasComNome(PersonasDeTeste.NomeFernandaDrumond),
            Apuracao(FormularioDeTeste.AvaliacaoSemOcultos), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "NOME" && e.Error.Code == InscricaoErrorCodes.NomeDiferenteDoLogin);
    }

    [Theory(DisplayName = "O NOME igual ao do login é aceito, ignorando espaços nas pontas e repetidos; maiúsculas e acentos contam")]
    [InlineData("Fernanda Laura Nicole Drumond", true)]
    [InlineData("  Fernanda  Laura Nicole   Drumond ", true)]
    [InlineData("FERNANDA LAURA NICOLE DRUMOND", false)]
    [InlineData("Fernánda Laura Nicole Drumond", false)]
    public void Salvar_NomeDoLogin_ComparaSemEspacosSobrando(string nomeEnviado, bool aceito)
    {
        Result resultado = SalvarComNome(PersonasDeTeste.NomeFernandaDrumond, ("NOME", nomeEnviado));

        resultado.IsSuccess.Should().Be(aceito);
    }

    [Fact(DisplayName = "Sem nome no token, o formulário que coleta NOME recusa no campo NOME")]
    public void Salvar_TokenSemNome_FormularioColetaNome_Recusa()
    {
        Result resultado = SalvarComNome(null, ("NOME", "Fernanda Laura Nicole Drumond"));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "NOME" && e.Error.Code == InscricaoErrorCodes.NomeAusenteNoLogin);
    }

    [Fact(DisplayName = "Sem nome no token, o formulário que não coleta NOME segue como antes")]
    public void Salvar_TokenSemNome_FormularioNaoColetaNome_Aceita()
    {
        Inscricao inscricao = NovaInscricao();

        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(), SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Omitir o NOME não é recusado, e o nome social segue livre")]
    public void Salvar_NomeOmitido_NomeSocialLivre_Aceita()
    {
        SalvarComNome(PersonasDeTeste.NomeFernandaDrumond, ("NOME_SOCIAL", "Ariel Drumond")).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Um NOME_SOCIAL diferente do nome social da conta é recusado ao salvar, no campo NOME_SOCIAL")]
    public void Salvar_NomeSocialDiferenteDaConta_Recusa()
    {
        Result resultado = SalvarComNomeSocial(PersonasDeTeste.NomeSocialFernandaDrumond, RespostasDeNomeSocial(true, "Outro Nome"));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "NOME_SOCIAL" && e.Error.Code == InscricaoErrorCodes.NomeSocialDiferenteDaConta);
    }

    [Fact(DisplayName = "Um NOME_SOCIAL diferente do nome social da conta é recusado ao enviar, no campo NOME_SOCIAL")]
    public void Submeter_NomeSocialDiferenteDaConta_Recusa()
    {
        Result<ComprovanteInscricao> resultado = SubmeterComNomeSocial(PersonasDeTeste.NomeSocialFernandaDrumond, RespostasDeNomeSocial(true, "Outro Nome"));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "NOME_SOCIAL" && e.Error.Code == InscricaoErrorCodes.NomeSocialDiferenteDaConta);
    }

    [Fact(DisplayName = "Ao enviar, o NOME_SOCIAL do rascunho que não é mais o da conta é recusado ainda que o corpo não o repita")]
    public void Submeter_NomeSocialGravadoDivergente_Recusa()
    {
        Inscricao inscricao = NovaInscricao();
        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), RespostasDeNomeSocial(true, "Nome Antigo"), SemGrupos(), SemAceites()), AdmitidasComNomeSocialDaConta("Nome Antigo"), PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        Result<ComprovanteInscricao> resultado = inscricao.Submeter("000123",
            Recebido(new Dictionary<string, JsonElement>(), SemGrupos(), SemAceites()), AdmitidasComNomeSocialDaConta(PersonasDeTeste.NomeSocialFernandaDrumond),
            Apuracao(FormularioDeTeste.AvaliacaoSemOcultos), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(InscricaoErrorCodes.NomeSocialDiferenteDaConta);
    }

    [Fact(DisplayName = "Ao enviar com sim e a conta sem nome social, a recusa orienta cadastrar em Meu perfil ou desmarcar a opção")]
    public void Submeter_SimSemNomeSocialNaConta_RecusaComOrientacao()
    {
        Result<ComprovanteInscricao> resultado = SubmeterComNomeSocial(null, RespostasDeNomeSocial(true));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "NOME_SOCIAL" && e.Error.Code == InscricaoErrorCodes.NomeSocialAusenteNaConta
                && e.Error.Message == "Você marcou que deseja usar nome social, mas sua conta não tem nome social cadastrado. Cadastre o nome social em Meu perfil ou desmarque a opção.");
    }

    [Theory(DisplayName = "Ao salvar com sim e a conta sem nome social, o campo vazio ou ausente não é recusado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Salvar_SimSemNomeSocialNaConta_CampoVazio_Aceita(string? nomeSocial)
    {
        SalvarComNomeSocial(null, RespostasDeNomeSocial(true, nomeSocial)).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Ao salvar com sim e a conta sem nome social, um NOME_SOCIAL preenchido é recusado com a orientação")]
    public void Salvar_SimSemNomeSocialNaConta_CampoPreenchido_Recusa()
    {
        Result resultado = SalvarComNomeSocial(null, RespostasDeNomeSocial(true, "Outro Nome"));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "NOME_SOCIAL" && e.Error.Code == InscricaoErrorCodes.NomeSocialAusenteNaConta);
    }

    [Theory(DisplayName = "O NOME_SOCIAL igual ao da conta é aceito, ignorando espaços nas pontas e repetidos")]
    [InlineData("Ariel Drumond")]
    [InlineData("  Ariel   Drumond ")]
    public void NomeSocialDaConta_ComparaSemEspacosSobrando(string nomeSocial)
    {
        SalvarComNomeSocial(PersonasDeTeste.NomeSocialFernandaDrumond, RespostasDeNomeSocial(true, nomeSocial)).IsSuccess.Should().BeTrue();
        SubmeterComNomeSocial(PersonasDeTeste.NomeSocialFernandaDrumond, RespostasDeNomeSocial(true, nomeSocial)).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Com não, a conta com nome social não deixa nome social gravado na inscrição enviada")]
    public void Submeter_NaoDesejaNomeSocial_NaoGravaNomeSocial()
    {
        ResultadoDaAvaliacaoDaInscricao semNomeSocial = FormularioDeTeste.AvaliacaoDoEnvio(new(
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["NOME_SOCIAL"] = FatoResolvido.NaoAplicavel() }, [], [], [], []));

        Result<ComprovanteInscricao> resultado = SubmeterComNomeSocial(
            PersonasDeTeste.NomeSocialFernandaDrumond, RespostasDeNomeSocial(false, "Outro Nome"), semNomeSocial);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Respostas.Should().NotContainKey("NOME_SOCIAL");
    }
}
