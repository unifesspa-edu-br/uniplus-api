namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ProcessosSeletivos.Edicao;
using Domain.ProcessosSeletivos.Referencias;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Confere os fatos e os valores que a árvore de exigências passa a citar contra o catálogo, como vínculo
/// novo: o valor desativado no catálogo é recusado, e o que o processo já citava continua. O fato que uma
/// exigência só comprova é vínculo existente apenas desta conferência; para os demais handlers, que o
/// coletam ou o citam, continua sendo vínculo novo.
/// </summary>
internal static class ConferenciaDosVinculosDasExigencias
{
    public static Result Conferir(ProcessoSeletivo processo, FatosDoGatilho fatos, IReadOnlyList<NoExigencia> raizes)
    {
        List<DocumentoExigido> documentos = [.. raizes
            .SelectMany(static r => r.AchatarComDescendentes())
            .Select(static n => n.DocumentoExigido)
            .OfType<DocumentoExigido>()];
        Result vinculoNovo = ConferenciaDeVinculoNovo.Conferir(
            fatos.Catalogo,
            VinculosExistentes(processo),
            VinculosDeFatos.De(
                documentos.SelectMany(static d => d.FatosComprovados),
                documentos.SelectMany(static d => d.Condicoes).Select(static c => (c.Fato, c.Valor))));
        return vinculoNovo.IsFailure ? Result.Failure(vinculoNovo.Error!) : vinculoNovo;
    }

    private static VinculosDeFatos VinculosExistentes(ProcessoSeletivo processo)
    {
        VinculosDeFatos vivos = ReferenciasVivas.Vinculos(EstadoDaConfiguracao.De(processo));
        IEnumerable<string> comprovados = processo.DocumentosExigidos.SelectMany(static d => d.FatosComprovados);
        return vivos with { Fatos = new HashSet<string>(vivos.Fatos.Concat(comprovados), StringComparer.Ordinal) };
    }
}
