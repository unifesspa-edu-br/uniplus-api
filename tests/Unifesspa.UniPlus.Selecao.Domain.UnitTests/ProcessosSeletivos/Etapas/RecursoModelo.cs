namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>As janelas recursais que os testes das etapas declaram.</summary>
internal static class RecursoModelo
{
    internal static RecursoDaEtapa AoAto(Guid produtoAncoraId) => Janela(EventoInicialDoPrazo.PublicacaoDoAto, produtoAncoraId);

    internal static RecursoDaEtapa NaCiencia() => Janela(EventoInicialDoPrazo.CienciaIndividual, Guid.Empty);

    private static RecursoDaEtapa Janela(EventoInicialDoPrazo ancora, Guid produtoAncoraId) => RecursoDaEtapa.Criar(
        ancora,
        ReferenciaRegra.De(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), "v1", new string('a', 64)),
        ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas),
        produtoAncoraId).Value!;
}
