namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.ApoioDoVeredictoDeSubmissao;

/// <summary>
/// As recusas de termo do envio da inscrição, juntando a recusa de cada aceite (domínio) e a do termo sem aceite
/// (veredito), como o envio as reúne: o aceite de outra versão devolve só a sua recusa, sem repetir que o termo
/// não foi aceito.
/// </summary>
public sealed class RecusasDosTermosDoEnvioTests
{
    private const string Declaracao = "DECLARACAO_VERACIDADE";
    private const string Adicional = "TERMO_ADICIONAL";

    private static readonly Guid VersaoFixada = Guid.NewGuid();
    private static readonly string HashFixado = new('a', 64);

    [Fact(DisplayName = "Aceite de outra versão devolve só a recusa do aceite, sem recusar o termo")]
    public void Recusas_AceiteDeOutraVersao_DevolveSoOAceiteDeOutraVersao()
    {
        IReadOnlyList<FieldError> recusas = RecusasDoEnvio([Declaracao], new AceiteDeTermo(FormularioDeTeste.CodigoDaInscricao, Declaracao, Guid.NewGuid(), HashFixado));

        CamposECodigos(recusas).Should().Equal(("aceites[0]", InscricaoErrorCodes.AceiteDeOutraVersao));
    }

    [Fact(DisplayName = "Aceite com o conteúdo diferente do fixado devolve só a recusa do aceite, sem recusar o termo")]
    public void Recusas_AceiteComHashDiferente_DevolveSoOAceiteDeOutraVersao()
    {
        IReadOnlyList<FieldError> recusas = RecusasDoEnvio([Declaracao], new AceiteDeTermo(FormularioDeTeste.CodigoDaInscricao, Declaracao, VersaoFixada, new string('b', 64)));

        CamposECodigos(recusas).Should().Equal(("aceites[0]", InscricaoErrorCodes.AceiteDeOutraVersao));
    }

    [Fact(DisplayName = "Termo sem aceite devolve só a recusa do termo, no campo do termo")]
    public void Recusas_TermoSemAceite_DevolveSoOTermoNaoAceito()
    {
        IReadOnlyList<FieldError> recusas = RecusasDoEnvio([Declaracao]);

        CamposECodigos(recusas).Should().Equal((CodigoPrefixadoDe(FormularioDeTeste.CodigoDaInscricao, Declaracao), InscricaoErrorCodes.TermoNaoAceito));
        recusas.Should().NotContain(static r => r.Error.Code == InscricaoErrorCodes.AceiteDeOutraVersao);
    }

    [Fact(DisplayName = "Um termo sem aceite e outro com aceite de outra versão devolvem as duas recusas, cada uma do seu termo")]
    public void Recusas_UmTermoSemAceiteEOutroDeOutraVersao_DevolveAsDuasRecusas()
    {
        IReadOnlyList<FieldError> recusas = RecusasDoEnvio(
            [Declaracao, Adicional], new AceiteDeTermo(FormularioDeTeste.CodigoDaInscricao, Declaracao, Guid.NewGuid(), HashFixado));

        CamposECodigos(recusas).Should().Equal(
            ("aceites[0]", InscricaoErrorCodes.AceiteDeOutraVersao),
            (CodigoPrefixadoDe(FormularioDeTeste.CodigoDaInscricao, Adicional), InscricaoErrorCodes.TermoNaoAceito));
    }

    [Fact(DisplayName = "Aceite na versão e no conteúdo fixados não devolve recusa")]
    public void Recusas_AceiteNaVersaoFixada_NaoDevolveRecusa()
    {
        IReadOnlyList<FieldError> recusas = RecusasDoEnvio([Declaracao], new AceiteDeTermo(FormularioDeTeste.CodigoDaInscricao, Declaracao, VersaoFixada, HashFixado));

        recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Aceite de outra versão seguido do aceite certo do mesmo termo devolve outra versão e aceite repetido, sem termo não aceito")]
    public void Recusas_OutraVersaoSeguidaDoAceiteCerto_DevolveOutraVersaoERepetido()
    {
        IReadOnlyList<FieldError> recusas = RecusasDoEnvio(
            [Declaracao],
            new AceiteDeTermo(FormularioDeTeste.CodigoDaInscricao, Declaracao, Guid.NewGuid(), HashFixado),
            new AceiteDeTermo(FormularioDeTeste.CodigoDaInscricao, Declaracao, VersaoFixada, HashFixado));

        CamposECodigos(recusas).Should().Equal(
            ("aceites[0]", InscricaoErrorCodes.AceiteDeOutraVersao),
            ("aceites[1]", InscricaoErrorCodes.AceiteRepetido));
    }

    private static IReadOnlyList<FieldError> RecusasDoEnvio(string[] termosObrigatorios, params AceiteDeTermo[] aceites)
    {
        Dictionary<TermoNoFormulario, TermoAdmitido> exigidos = termosObrigatorios.ToDictionary(
            static codigo => new TermoNoFormulario(FormularioDeTeste.CodigoDaInscricao, codigo), static _ => new TermoAdmitido(VersaoFixada, HashFixado));
        AvaliacaoFormulario avaliacao = AvaliacaoCom(
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
            termos: [.. termosObrigatorios.Select(static codigo =>
                new AvaliacaoTermo(CodigoPrefixadoDe(FormularioDeTeste.CodigoDaInscricao, codigo), Ternario.Verdadeiro, Ternario.Verdadeiro))]);
        PreenchimentoDoCandidato preenchimento = PreenchimentoDoCandidato.Criar(
            OpcoesDeCursoDaInscricao.Reidratar(null, null), new Dictionary<string, JsonElement>(),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(), aceites);

        return [.. preenchimento.RecusasDosAceites(exigidos), .. VeredictoDeSubmissao.Avaliar(avaliacao, exigidos, aceites, DiaDoEnvio)];
    }

    private static IEnumerable<(string? Campo, string Codigo)> CamposECodigos(IEnumerable<FieldError> recusas) =>
        recusas.Select(static r => (r.Field, r.Error.Code));
}
