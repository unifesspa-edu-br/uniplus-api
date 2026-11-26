namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Coleta;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Com mais de um formulário na inscrição, o conjunto básico de dados do candidato fica em um só: o que já tem
/// dado básico, e, se nenhum tem, o primeiro pela ordem do código, contado o formulário que está sendo declarado.
/// </summary>
public sealed class ConjuntoBasicoNaInscricaoTests
{
    private readonly ProcessoSeletivo _processo = ProcessoConformeFactory.Criar();

    private Guid FaseDaInscricao => _processo.CronogramaFases.Single(static fase => fase.ColetaInscricao).Id;

    private static FormularioDeclarado Declarado(string codigo, Guid? faseId, bool obrigatorio = true) =>
        new(CodigoDoFormulario.Criar(codigo).Value!, faseId, obrigatorio);

    private bool Guarda(FormularioDeclarado declarado) => ConjuntoBasicoNaInscricao.Guarda(_processo.Declaracao(), declarado);

    private static IReadOnlyList<PaginaDoFormulario> PaginasSemDadosBasicos() =>
    [
        PaginaDoFormulario.Criar(new PaginaEstrutura("DADOS", 0, TipoPaginaFormulario.Secao, null), "Dados", null, null).Value!,
        PaginaDoFormulario.Criar(new PaginaEstrutura("REVISAO", 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite), "Revisão e aceite", null, null).Value!,
    ];

    private void DefinirSemDadosBasicos(string codigo) =>
        _processo.DefinirFormulario(Declarado(codigo, FaseDaInscricao), null, PaginasSemDadosBasicos()).IsSuccess.Should().BeTrue();

    private void ReterSomenteFormulariosSemDadosBasicos(params string[] codigos)
    {
        _processo.RemoverFormulario(CodigoDoFormulario.Criar(FormularioDeTeste.CodigoDaInscricao).Value!).IsSuccess.Should().BeTrue();
        foreach (string codigo in codigos)
        {
            DefinirSemDadosBasicos(codigo);
        }
    }

    [Fact(DisplayName = "O formulário da inscrição que já tem os dados básicos continua guardando o conjunto")]
    public void FormularioComDadosBasicos_ContinuaGuardando() =>
        Guarda(Declarado(FormularioDeTeste.CodigoDaInscricao, FaseDaInscricao)).Should().BeTrue();

    [Fact(DisplayName = "Outro formulário da inscrição não guarda o conjunto, mesmo antes na ordem do código")]
    public void OutroFormularioDaInscricao_AntesNaOrdem_NaoGuarda() =>
        Guarda(Declarado("A_ANTES_DA_INSCRICAO", FaseDaInscricao)).Should().BeFalse();

    [Fact(DisplayName = "O formulário sem fase não guarda o conjunto")]
    public void FormularioSemFase_NaoGuarda() =>
        Guarda(Declarado(FormularioDeTeste.CodigoDaInscricao, faseId: null)).Should().BeFalse();

    [Fact(DisplayName = "O formulário facultativo não guarda o conjunto")]
    public void FormularioFacultativo_NaoGuarda() =>
        Guarda(Declarado(FormularioDeTeste.CodigoDaInscricao, FaseDaInscricao, obrigatorio: false)).Should().BeFalse();

    [Fact(DisplayName = "Sem dado básico em nenhum formulário, guarda o primeiro da inscrição pela ordem do código")]
    public void SemDadosBasicos_GuardaOPrimeiroPelaOrdemDoCodigo()
    {
        ReterSomenteFormulariosSemDadosBasicos("DADOS_B", "DADOS_A");

        Guarda(Declarado("DADOS_A", FaseDaInscricao)).Should().BeTrue();
        Guarda(Declarado("DADOS_B", FaseDaInscricao)).Should().BeFalse();
    }

    [Fact(DisplayName = "O formulário declarado conta na ordem: o que vem antes dos gravados passa a guardar o conjunto")]
    public void FormularioDeclaradoAntesDosGravados_Guarda()
    {
        ReterSomenteFormulariosSemDadosBasicos("DADOS_B");

        Guarda(Declarado("DADOS_A", FaseDaInscricao)).Should().BeTrue();
        Guarda(Declarado("DADOS_C", FaseDaInscricao)).Should().BeFalse();
    }
}
