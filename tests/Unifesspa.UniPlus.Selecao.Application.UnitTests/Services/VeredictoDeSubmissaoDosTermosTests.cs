namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.ApoioDoVeredictoDeSubmissao;

/// <summary>
/// Os termos no resultado da submissão: o termo obrigatório não aceito, ou aceito em outra versão ou com outro
/// hash, recusa; a recusa cita o formulário que exige o termo.
/// </summary>
public sealed class VeredictoDeSubmissaoDosTermosTests
{
    [Fact(DisplayName = "Termo obrigatório não aceito recusa a submissão")]
    public void Avaliar_TermoObrigatorioNaoAceito_Recusa()
    {
        AvaliacaoTermo termo = new(CodigoPrefixado, Ternario.Verdadeiro, Ternario.Verdadeiro);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), termos: [termo]);
        IReadOnlyDictionary<TermoNoFormulario, TermoAdmitido> exigido = TermoExigido("DECLARACAO_VERACIDADE", Guid.NewGuid(), new string('a', 64));

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, exigido, SemAceites, DiaDoEnvio);

        recusas.Should().ContainSingle(r => r.Field == CodigoPrefixado && r.Error.Code == "Inscricao.TermoNaoAceito");
    }

    [Fact(DisplayName = "Termo aceito com hash diferente do da versão fixada recusa a submissão")]
    public void Avaliar_TermoAceitoComHashDiferente_Recusa()
    {
        AvaliacaoTermo termo = new(CodigoPrefixado, Ternario.Verdadeiro, Ternario.Verdadeiro);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), termos: [termo]);
        Guid versaoId = Guid.NewGuid();
        IReadOnlyDictionary<TermoNoFormulario, TermoAdmitido> exigido = TermoExigido("DECLARACAO_VERACIDADE", versaoId, new string('a', 64));
        AceiteDeTermo aceite = new(FormularioDeTeste.CodigoDaInscricao, "DECLARACAO_VERACIDADE", versaoId, new string('b', 64));

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, exigido, [aceite], DiaDoEnvio);

        recusas.Should().ContainSingle(r => r.Field == CodigoPrefixado && r.Error.Code == "Inscricao.TermoNaoAceito");
    }

    [Fact(DisplayName = "Termo aceito com a mesma versão e o mesmo hash da versão fixada não recusa")]
    public void Avaliar_TermoAceitoNaVersaoFixada_NaoRecusa()
    {
        AvaliacaoTermo termo = new(CodigoPrefixado, Ternario.Verdadeiro, Ternario.Verdadeiro);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), termos: [termo]);
        Guid versaoId = Guid.NewGuid();
        IReadOnlyDictionary<TermoNoFormulario, TermoAdmitido> exigido = TermoExigido("DECLARACAO_VERACIDADE", versaoId, new string('a', 64));
        AceiteDeTermo aceite = new(FormularioDeTeste.CodigoDaInscricao, "DECLARACAO_VERACIDADE", versaoId, new string('a', 64));

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, exigido, [aceite], DiaDoEnvio);

        recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Aceite do mesmo código de termo em outro formulário não vale para o formulário que exige o termo")]
    public void Avaliar_AceiteDoTermoEmOutroFormulario_Recusa()
    {
        AvaliacaoTermo termo = new(CodigoPrefixado, Ternario.Verdadeiro, Ternario.Verdadeiro);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), termos: [termo]);
        Guid versaoId = Guid.NewGuid();
        AceiteDeTermo aceiteDeOutroFormulario = new("COMPLEMENTO_DA_INSCRICAO", "DECLARACAO_VERACIDADE", versaoId, new string('a', 64));

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(
            avaliacao, TermoExigido("DECLARACAO_VERACIDADE", versaoId, new string('a', 64)), [aceiteDeOutroFormulario], DiaDoEnvio);

        recusas.Should().ContainSingle(r => r.Field == CodigoPrefixado && r.Error.Code == "Inscricao.TermoNaoAceito");
    }

    [Fact(DisplayName = "Dois formulários da inscrição com o termo de mesmo código: só o formulário sem aceite recusa")]
    public void Avaliar_MesmoCodigoDeTermoEmDoisFormularios_RecusaSoOSemAceite()
    {
        const string outroFormulario = "COMPLEMENTO_DA_INSCRICAO";
        Guid versaoId = Guid.NewGuid();
        AceiteDeTermo aceiteDaInscricao = new(FormularioDeTeste.CodigoDaInscricao, "DECLARACAO_VERACIDADE", versaoId, new string('a', 64));

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(
            AvaliacaoComTermoEm(FormularioDeTeste.CodigoDaInscricao, outroFormulario),
            TermoExigidoEm(versaoId, FormularioDeTeste.CodigoDaInscricao, outroFormulario), [aceiteDaInscricao], DiaDoEnvio);

        FieldError recusa = recusas.Should().ContainSingle().Subject;
        recusa.Field.Should().Be(CodigoPrefixadoDe(outroFormulario, "DECLARACAO_VERACIDADE"));
        recusa.Error.Code.Should().Be("Inscricao.TermoNaoAceito");
    }

    [Fact(DisplayName = "Dois formulários da inscrição com o termo de mesmo código, nenhum aceito: duas recusas, cada uma com o seu formulário")]
    public void Avaliar_MesmoCodigoDeTermoEmDoisFormulariosSemAceite_RecusaCadaFormulario()
    {
        const string outroFormulario = "COMPLEMENTO_DA_INSCRICAO";

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(
            AvaliacaoComTermoEm(FormularioDeTeste.CodigoDaInscricao, outroFormulario),
            TermoExigidoEm(Guid.NewGuid(), FormularioDeTeste.CodigoDaInscricao, outroFormulario), SemAceites, DiaDoEnvio);

        recusas.Select(static r => r.Field).Should().Equal(
            CodigoPrefixadoDe(FormularioDeTeste.CodigoDaInscricao, "DECLARACAO_VERACIDADE"), CodigoPrefixadoDe(outroFormulario, "DECLARACAO_VERACIDADE"));
        recusas.Should().OnlyContain(static r => r.Error.Code == "Inscricao.TermoNaoAceito");
        recusas[0].Error.Message.Should().Contain($"'{FormularioDeTeste.CodigoDaInscricao}'");
        recusas[1].Error.Message.Should().Contain($"'{outroFormulario}'");
    }

    [Fact(DisplayName = "Termo opcional não aceito não recusa")]
    public void Avaliar_TermoOpcionalNaoAceito_NaoRecusa()
    {
        AvaliacaoTermo termo = new("TERMO_OPCIONAL", Ternario.Verdadeiro, Ternario.Falso);
        AvaliacaoFormulario avaliacao = AvaliacaoCom(new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), termos: [termo]);

        IReadOnlyList<FieldError> recusas = VeredictoDeSubmissao.Avaliar(avaliacao, SemTermos, SemAceites, DiaDoEnvio);

        recusas.Should().BeEmpty();
    }
}
