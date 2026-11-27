namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O quadro de pesos por área do ENEM que a classificação congela, tirado da resolução que o cadastro tem agora: as linhas viram os
/// grupos congelados e os grupos sem linha ficam pelo rótulo. O domínio confere cada linha, então a recusa dele volta como resultado.
/// </summary>
internal static class CopiaDoQuadroDePesos
{
    internal static Result<QuadroDePesosCitado> De(ResolucaoPesoAreaEnemView resolucao)
    {
        ArgumentNullException.ThrowIfNull(resolucao);

        Result<GrupoPesoAreaEnemCongelado>[] grupos = [.. resolucao.Linhas.Select(QuadroDePesosResolvido.CopiarLinha)];
        Result<GrupoPesoAreaEnemCongelado>? recusado = grupos.FirstOrDefault(static grupo => grupo.IsFailure);

        return recusado is not null
            ? Result<QuadroDePesosCitado>.Failure(recusado.Error!)
            : Result<QuadroDePesosCitado>.Success(new(
                [.. grupos.Select(static grupo => grupo.Value!)],
                [.. resolucao.GruposAusentes.Select(static grupo => grupo.Rotulo)]));
    }
}
