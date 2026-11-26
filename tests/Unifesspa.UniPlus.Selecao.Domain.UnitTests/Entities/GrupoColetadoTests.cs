namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>A criação do grupo repetível do formulário (UNI-REQ-0146): a forma, o candidato como membro e o formulário.</summary>
public sealed class GrupoColetadoTests
{
    private static readonly OcorrenciasDoGrupo ZeroACinco = new(0, 5, IncluiCandidato: false);

    private static readonly ExibicaoEObrigatoriedade SempreNunca = new(null, Obrigatoriedade.Nunca);

    [Fact(DisplayName = "O grupo adota o formulário dos campos, que são todos do mesmo")]
    public void Criar_CamposDoMesmoFormulario_GrupoAdotaOFormulario()
    {
        FormularioProcesso habilitacao = LigacaoAoFormulario.FormularioNoPapel(PapelDoFormularioDeTeste.Habilitacao);

        GrupoColetado grupo = GrupoColetado.Criar("COMPOSICAO_FAMILIAR", 0, FormularioDeTeste.Secao, "Composição familiar", ZeroACinco, SempreNunca, CamposDe(habilitacao, habilitacao)).Value!;

        grupo.FormularioId.Should().Be(habilitacao.Id);
        grupo.Subitens.Should().OnlyContain(c => c.FormularioId == habilitacao.Id);
    }

    [Fact(DisplayName = "Campos de formulários diferentes são erro de programação: o grupo tem o formulário dos seus campos")]
    public void Criar_CamposDeFormulariosDiferentes_Lanca()
    {
        FatoColetado[] campos = CamposDe(
            LigacaoAoFormulario.FormularioNoPapel(PapelDoFormularioDeTeste.Inscricao), LigacaoAoFormulario.FormularioNoPapel(PapelDoFormularioDeTeste.Habilitacao));

        FluentActions.Invoking(() => GrupoColetado.Criar("COMPOSICAO_FAMILIAR", 0, FormularioDeTeste.Secao, "Composição familiar", ZeroACinco, SempreNunca, campos))
            .Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "A forma recusada devolve a falha antes de olhar o formulário dos campos")]
    public void Criar_FormaRecusadaComFormulariosDiferentes_DevolveAFalha()
    {
        FatoColetado[] campos = CamposDe(
            LigacaoAoFormulario.FormularioNoPapel(PapelDoFormularioDeTeste.Inscricao), LigacaoAoFormulario.FormularioNoPapel(PapelDoFormularioDeTeste.Habilitacao));

        Result<GrupoColetado> resultado = GrupoColetado.Criar(" ", 0, FormularioDeTeste.Secao, "Composição familiar", ZeroACinco, SempreNunca, campos);

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(GrupoFormularioErrorCodes.CodigoInvalido);
    }

    private static FatoColetado[] CamposDe(FormularioProcesso parentesco, FormularioProcesso renda) =>
    [
        FatoColetadoDeTeste.Coletando("PARENTESCO", 0).NoFormulario(parentesco).Construir(),
        FatoColetadoDeTeste.Coletando("RENDA", 1).NoFormulario(renda).Construir(),
    ];

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

    [Fact(DisplayName = "A seção fica como enviada e o rótulo é aparado, e cada campo é ligado ao grupo")]
    public void Criar_SecaoERotuloComEspacos_GravaSecaoELigaOsCampos()
    {
        FatoColetado campo = FatoColetadoDeTeste.Coletando("RENDA", 0).Construir();

        GrupoColetado grupo = GrupoColetado.Criar("RENDAS", 0, " DADOS ", " Rendas ", ZeroACinco, SempreNunca, [campo]).Value!;

        grupo.Codigo.Should().Be("RENDAS");
        grupo.PaginaCodigo.Should().Be(" DADOS ");
        grupo.Rotulo.Should().Be("Rendas");
        campo.GrupoColetadoId.Should().Be(grupo.Id);
    }
}
