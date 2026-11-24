namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os parâmetros do recurso que os testes declaram a uma fase ou a uma etapa: sem efeito suspensivo, guarda da vaga até o
/// resultado definitivo (a que vale quando o edital não declara) e, por padrão, sem instância superior.
/// </summary>
internal static class ParametrosDoRecursoDeTeste
{
    /// <summary>A declaração do recurso, como a fase ou a etapa a recebe para conferir.</summary>
    internal static ParametrosDeclaradosDoRecurso Declarados(
        decimal valor = 2m,
        UnidadePrazo unidade = UnidadePrazo.DiasUteis,
        bool admiteInstanciaSuperior = false,
        PrazoDoRecurso? segundaInstancia = null,
        string? efeitoSuspensivo = "SEM_EFEITO_SUSPENSIVO",
        string? guardaDaVaga = null) =>
        new(
            new PrazoDeclarado(valor, unidade.Codigo()),
            admiteInstanciaSuperior,
            segundaInstancia is null ? PrazoDeclarado.Ausente : Declarado(segundaInstancia),
            efeitoSuspensivo,
            guardaDaVaga);

    /// <summary>A declaração que repete os parâmetros já conferidos de outra regra de recurso.</summary>
    internal static ParametrosDeclaradosDoRecurso DeclaradosDe(ParametrosRegraPrazoRecurso conferidos) =>
        new(
            Declarado(conferidos.PrazoDeInterposicao),
            conferidos.AdmiteInstanciaSuperior,
            conferidos.PrazoDaSegundaInstancia is { } segunda ? Declarado(segunda) : PrazoDeclarado.Ausente,
            conferidos.EfeitoSuspensivo.Codigo(),
            conferidos.GuardaDaVaga.Codigo());

    private static PrazoDeclarado Declarado(PrazoDoRecurso prazo) => new(prazo.Valor, prazo.Unidade.Codigo());

    /// <summary>A declaração com instância superior e prazo próprio de 3 dias úteis na 2ª instância.</summary>
    internal static ParametrosDeclaradosDoRecurso ComInstanciaSuperior(decimal valor = 2m, UnidadePrazo unidade = UnidadePrazo.DiasUteis) =>
        Declarados(valor, unidade, admiteInstanciaSuperior: true, segundaInstancia: new PrazoDoRecurso(3m, UnidadePrazo.DiasUteis));

    /// <summary>Os parâmetros já conferidos, como a fase ou a etapa os guarda.</summary>
    internal static ParametrosRegraPrazoRecurso Conferidos(
        decimal valor = 2m,
        UnidadePrazo unidade = UnidadePrazo.DiasUteis,
        bool admiteInstanciaSuperior = false,
        PrazoDoRecurso? segundaInstancia = null) =>
        Declarados(valor, unidade, admiteInstanciaSuperior, segundaInstancia).Conferir().Conferidos
        ?? throw new InvalidOperationException("Os parâmetros de teste do recurso devem ser válidos.");

    /// <summary>Os parâmetros conferidos de um recurso com instância superior e prazo de 3 dias úteis na 2ª instância.</summary>
    internal static ParametrosRegraPrazoRecurso ConferidosComInstanciaSuperior(decimal valor = 2m, UnidadePrazo unidade = UnidadePrazo.DiasUteis) =>
        Conferidos(valor, unidade, admiteInstanciaSuperior: true, segundaInstancia: new PrazoDoRecurso(3m, UnidadePrazo.DiasUteis));
}
