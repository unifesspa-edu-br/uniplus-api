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

public sealed class InscricaoGruposTests
{
    [Fact(DisplayName = "Salvar recusa duas ocorrências do mesmo grupo com o mesmo id")]
    public void Salvar_IdDeOcorrenciaRepetido_Recusa()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] =
            [
                new OcorrenciaDeGrupoInscricao("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)),
                new OcorrenciaDeGrupoInscricao("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)),
            ],
        };

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(), grupos, SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.GrupoIdDuplicado);
    }

    [Fact(DisplayName = "Salvar recusa o grupo que o formulário não tem, sem mutar nada")]
    public void Salvar_GrupoForaDoFormulario_RecusaSemMutar()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = new(StringComparer.Ordinal)
        {
            ["GRUPO_INVENTADO"] = [new OcorrenciaDeGrupoInscricao("1", new Dictionary<string, JsonElement>(StringComparer.Ordinal))],
        };

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(), grupos, SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("GRUPO_INVENTADO");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.GrupoForaDoFormulario);
        inscricao.Revisao.Should().Be(1);
        inscricao.Formulario.Preenchimento.Grupos.Should().BeEmpty();
    }

    [Theory(DisplayName = "Salvar recusa na ocorrência a resposta de fato que não é subitem do grupo")]
    [InlineData("RENDA_MENSAL", "subitem de outro grupo")]
    [InlineData("NOME_SOCIAL", "fato de nível superior")]
    public void Salvar_FatoForaDoGrupo_RecusaSemMutar(string fato, string origem)
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = FamiliaComMembro((fato, "valor"));

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(), grupos, SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle(origem).Which.Field.Should().Be($"COMPOSICAO_FAMILIAR.membro-1.{fato}");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.FatoForaDoGrupo);
        inscricao.Revisao.Should().Be(1);
        inscricao.Formulario.Preenchimento.Grupos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Salvar recusa no nível superior o fato que só existe como subitem de grupo, sem mutar nada")]
    public void Salvar_SubitemDeGrupoNoNivelSuperior_RecusaSemMutar()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> respostas = Respostas(("RENDA_MENSAL", "1500"));

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), respostas, SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("RENDA_MENSAL");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.FatoSoDeGrupo);
        inscricao.Revisao.Should().Be(1);
        inscricao.Formulario.Preenchimento.Respostas.Should().ContainKey("CPF").And.NotContainKey("RENDA_MENSAL");
    }

    [Fact(DisplayName = "Salvar admite no nível superior e na ocorrência o fato que o formulário coleta nos dois lugares")]
    public void Salvar_FatoNosDoisNiveis_Admite()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> respostas = Respostas(("RG_NUMERO", "123456"));
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = FamiliaComMembro(("RG_NUMERO", "654321"));

        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), respostas, grupos, SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Submeter recusa no nível superior o fato que só existe como subitem de grupo")]
    public void Submeter_SubitemDeGrupoNoNivelSuperior_Recusa()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, JsonElement> respostas = Respostas(("RENDA_MENSAL", "1500"));

        Result<ComprovanteInscricao> resultado = inscricao.Submeter("000123",
            Recebido(respostas, SemGrupos(), SemAceites()), Admitidas,
            Apuracao(FormularioDeTeste.AvaliacaoSemOcultos), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("RENDA_MENSAL");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.FatoSoDeGrupo);
    }

    [Fact(DisplayName = "Salvar preserva o grupo omitido do corpo e substitui o grupo enviado, mesmo vazio")]
    public void Salvar_GrupoOmitido_PreservaOGravado()
    {
        Inscricao inscricao = NovaInscricao();
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> comMembro = FamiliaComMembro(("PARENTESCO", "PAI_OU_MAE"));
        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(), comMembro, SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(), SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();
        inscricao.Formulario.Preenchimento.Grupos["COMPOSICAO_FAMILIAR"].Should().ContainSingle(static o => o.Id == "membro-1",
            "o grupo ausente do corpo vale o que já está gravado");

    }

    [Fact(DisplayName = "O grupo recebido sem ocorrências substitui o grupo gravado")]
    public void Salvar_GrupoVazio_RetiraOcorrencias()
    {
        Inscricao inscricao = NovaInscricao();
        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(), FamiliaComMembro(("PARENTESCO", "PAI_OU_MAE")), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupoVazio = new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] = [],
        };
        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(), grupoVazio, SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();
        inscricao.Formulario.Preenchimento.Grupos["COMPOSICAO_FAMILIAR"].Should().BeEmpty("o grupo enviado substitui o gravado, mesmo sem ocorrências");
    }
}
