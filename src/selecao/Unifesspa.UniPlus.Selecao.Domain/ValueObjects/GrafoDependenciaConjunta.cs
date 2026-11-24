namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>Dependências entre as partes da configuração e sua ordem de coleta.</summary>
public sealed class GrafoDependenciaConjunta
{
    private GrafoDependenciaConjunta(IReadOnlyList<ParteDaConfiguracao> partes,
        IReadOnlyList<DependenciaEntrePartes> dependencias, IReadOnlyList<ParteDaConfiguracao> ordemDeColeta)
    {
        Partes = partes;
        Dependencias = dependencias;
        OrdemDeColeta = ordemDeColeta;
    }

    public IReadOnlyList<ParteDaConfiguracao> Partes { get; }
    public IReadOnlyList<DependenciaEntrePartes> Dependencias { get; }
    public IReadOnlyList<ParteDaConfiguracao> OrdemDeColeta { get; }

    public static Result<GrafoDependenciaConjunta> Construir(
        IReadOnlyCollection<FatoColetado> fatosColetados, IReadOnlyCollection<ConfiguracaoDerivacaoFato> regrasDerivacao,
        IReadOnlyCollection<DocumentoExigido> documentosExigidos, IReadOnlyCollection<FormularioProcesso> formularios,
        IReadOnlyCollection<TermoExigidoFormulario> termos, IReadOnlyCollection<GrupoColetado>? grupos = null)
    {
        ArgumentNullException.ThrowIfNull(fatosColetados);
        ArgumentNullException.ThrowIfNull(regrasDerivacao);
        ArgumentNullException.ThrowIfNull(documentosExigidos);
        ArgumentNullException.ThrowIfNull(formularios);
        ArgumentNullException.ThrowIfNull(termos);
        MontagemDasDependencias montagem = MontagemDasDependencias.Montar(fatosColetados, regrasDerivacao, documentosExigidos, formularios, termos, grupos ?? []);
        if (montagem.PrimeiroCiclo() is { } ciclo)
        {
            return Result<GrafoDependenciaConjunta>.Failure(ErroDeCiclo(ciclo));
        }

        OrdemDasDependencias ordem = new(montagem);
        return Result<GrafoDependenciaConjunta>.Success(new(ordem.PartesEmOrdem(), ordem.DependenciasEmOrdem(), ordem.OrdemDeColeta()));
    }

    private static DomainError ErroDeCiclo(IReadOnlyList<ParteDaConfiguracao> ciclo) => new(
        GrafoDependenciaConjuntaErrorCodes.GrafoConjuntoComCiclo,
        $"As dependências entre campos, dados, exigências, seções e termos da configuração formam um ciclo; cada um depende do seguinte: {string.Join(" → ", ciclo.Select(static parte => parte.Rotulo))}.");
}

/// <summary>Códigos de erro das dependências da configuração.</summary>
public static class GrafoDependenciaConjuntaErrorCodes
{
    public const string GrafoConjuntoComCiclo = "GrafoDependenciaConjunta.GrafoConjuntoComCiclo";
}
