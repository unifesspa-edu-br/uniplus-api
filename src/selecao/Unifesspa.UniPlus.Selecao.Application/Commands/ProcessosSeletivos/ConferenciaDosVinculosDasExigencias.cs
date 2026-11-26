namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ProcessosSeletivos.Edicao;
using Domain.ProcessosSeletivos.Referencias;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>Confere no catálogo os fatos e valores citados pelas exigências.</summary>
internal static class ConferenciaDosVinculosDasExigencias
{
    public static Result Conferir(ProcessoSeletivo processo, FatosDoGatilho fatos, IReadOnlyList<RegraDeComprovacao> regrasSemGrupo)
    {
        List<DocumentoExigido> documentos = [.. regrasSemGrupo
            .SelectMany(static r => r.EnumerarRegraEItens())
            .Select(static n => n.DocumentoExigido)
            .OfType<DocumentoExigido>()];
        return ConferenciaDeVinculoNovo.Conferir(
            fatos.Catalogo,
            VinculosExistentes(processo),
            VinculosDeFatos.De(
                documentos.SelectMany(static d => d.FatosComprovados),
                documentos.SelectMany(static d => d.Condicoes).Select(static c => (c.Fato, c.Valor))));
    }

    private static VinculosDeFatos VinculosExistentes(ProcessoSeletivo processo)
    {
        VinculosDeFatos vivos = ReferenciasVivas.Vinculos(processo.Declaracao());
        IEnumerable<string> comprovados = processo.DocumentosExigidos.SelectMany(static d => d.FatosComprovados);
        return vivos with { Fatos = new HashSet<string>(vivos.Fatos.Concat(comprovados), StringComparer.Ordinal) };
    }
}
