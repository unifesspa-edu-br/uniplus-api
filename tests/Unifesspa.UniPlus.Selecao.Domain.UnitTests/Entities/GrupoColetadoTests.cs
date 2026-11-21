namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>A criação do grupo repetível do formulário (UNI-REQ-0146): a forma, o candidato como membro e a finalidade.</summary>
public sealed class GrupoColetadoTests
{
    private static readonly OcorrenciasDoGrupo ZeroACinco = new(0, 5);

    private static readonly ExibicaoEObrigatoriedade SempreNunca = new(null, Obrigatoriedade.Nunca);

    [Fact(DisplayName = "O grupo adota a finalidade dos campos, que são todos da mesma")]
    public void Criar_CamposDaMesmaFinalidade_GrupoAdotaAFinalidade()
    {
        FatoColetado[] campos =
        [
            FatoColetadoDeTeste.Coletando("PARENTESCO", 0).DaFinalidade(FinalidadeFormulario.Habilitacao).Construir(),
            FatoColetadoDeTeste.Coletando("RENDA", 1).DaFinalidade(FinalidadeFormulario.Habilitacao).Construir(),
        ];

        GrupoColetado grupo = GrupoColetado.Criar("COMPOSICAO_FAMILIAR", 0, FormularioDeTeste.Secao, "Composição familiar", ZeroACinco, SempreNunca, campos).Value!;

        grupo.Finalidade.Should().Be(FinalidadeFormulario.Habilitacao);
        grupo.Subitens.Should().OnlyContain(static c => c.Finalidade == FinalidadeFormulario.Habilitacao);
    }

    [Fact(DisplayName = "Campos de finalidades diferentes são erro de programação: o grupo tem a finalidade dos seus campos")]
    public void Criar_CamposDeFinalidadesDiferentes_Lanca()
    {
        FatoColetado[] campos =
        [
            FatoColetadoDeTeste.Coletando("PARENTESCO", 0).DaFinalidade(FinalidadeFormulario.Inscricao).Construir(),
            FatoColetadoDeTeste.Coletando("RENDA", 1).DaFinalidade(FinalidadeFormulario.Habilitacao).Construir(),
        ];

        FluentActions.Invoking(() => GrupoColetado.Criar("COMPOSICAO_FAMILIAR", 0, FormularioDeTeste.Secao, "Composição familiar", ZeroACinco, SempreNunca, campos))
            .Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "A forma recusada devolve a falha antes de olhar a finalidade dos campos")]
    public void Criar_FormaRecusadaComFinalidadesDiferentes_DevolveAFalha()
    {
        FatoColetado[] campos =
        [
            FatoColetadoDeTeste.Coletando("PARENTESCO", 0).DaFinalidade(FinalidadeFormulario.Inscricao).Construir(),
            FatoColetadoDeTeste.Coletando("RENDA", 1).DaFinalidade(FinalidadeFormulario.Habilitacao).Construir(),
        ];

        Result<GrupoColetado> resultado = GrupoColetado.Criar(" ", 0, FormularioDeTeste.Secao, "Composição familiar", ZeroACinco, SempreNunca, campos);

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(GrupoFormularioErrorCodes.CodigoInvalido);
    }

    [Fact(DisplayName = "As violações da forma e a falta do parentesco do candidato saem juntas, na ordem da forma e depois do candidato")]
    public void Criar_FormaRecusadaEGrupoQueIncluiOCandidatoSemParentesco_AcumulaAsDuas()
    {
        Result<GrupoColetado> resultado = GrupoColetadoDeTeste.Agrupando(" ", 0)
            .IncluindoOCandidato()
            .ComOcorrencias(1, 5)
            .ComCampos(FatoColetadoDeTeste.Coletando("RENDA", 0).Construir())
            .Criar();

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(
            GrupoFormularioErrorCodes.CodigoInvalido, GrupoFormularioErrorCodes.CandidatoComoMembroIncompleto);
    }

    [Fact(DisplayName = "O grupo que não inclui o candidato não precisa do parentesco")]
    public void Criar_GrupoSemCandidatoSemParentesco_Cria() =>
        GrupoColetadoDeTeste.Agrupando("RENDAS", 0).ComCampos(FatoColetadoDeTeste.Coletando("RENDA", 0).Construir()).Criar().IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "As ocorrências e as regras calculadas refletem o que foi criado")]
    public void Criar_GrupoValido_OcorrenciasERegrasCalculadas()
    {
        GrupoColetado grupo = GrupoColetadoDeTeste.Agrupando("COMPOSICAO_FAMILIAR", 0)
            .ComOcorrencias(1, null)
            .IncluindoOCandidato()
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ComCampos(FatoColetadoDeTeste.Coletando(CandidatoComoMembro.FatoParentesco, 0).ComObrigatoriedade(Obrigatoriedade.Sempre).Construir())
            .Construir();

        grupo.Ocorrencias.Should().Be(new OcorrenciasDoGrupo(1, null, IncluiCandidato: true));
        grupo.Regras.Should().Be(new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Sempre));
    }

    [Fact(DisplayName = "O código e a seção saem normalizados e o rótulo aparado, e cada campo é ligado ao grupo")]
    public void Criar_CodigoSecaoERotuloComSobras_NormalizaELigaOsCampos()
    {
        FatoColetado campo = FatoColetadoDeTeste.Coletando("RENDA", 0).Construir();

        GrupoColetado grupo = GrupoColetado.Criar(" RENDAS ", 0, " DADOS ", " Rendas ", ZeroACinco, SempreNunca, [campo]).Value!;

        grupo.Codigo.Should().Be("RENDAS");
        grupo.EtapaCodigo.Should().Be("DADOS");
        grupo.Rotulo.Should().Be("Rendas");
        campo.GrupoColetadoId.Should().Be(grupo.Id);
    }
}
