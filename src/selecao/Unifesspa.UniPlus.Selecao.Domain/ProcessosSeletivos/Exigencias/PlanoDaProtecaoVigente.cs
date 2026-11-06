namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A proteção vigente que cada exigência recebe ao congelar; só a raiz aplica.</summary>
internal sealed record PlanoDaProtecaoVigente(IReadOnlyList<(DocumentoExigido Exigencia, ProtecaoDoDocumento Efetiva)> Exigencias)
{
    internal void Aplicar()
    {
        foreach ((DocumentoExigido exigencia, ProtecaoDoDocumento efetiva) in Exigencias)
        {
            exigencia.RecongelarProtecao(efetiva);
        }
    }
}
