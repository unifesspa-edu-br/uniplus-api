namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

/// <summary>
/// O número de opções de curso do documento, conferido pela regra da declaração: o que não é 1 nem 2 fica fora com a recusa no campo
/// do número. A conferência contra a quantidade de ofertas é da conformidade da configuração, que lê as duas coisas já adotadas.
/// </summary>
internal static class LeituraDasOpcoesDeCurso
{
    internal const string Caminho = "opcoesDeCurso";

    internal static OpcoesDeCursoLidas Ler(int? numero)
    {
        if (numero is not { } declarado)
        {
            return new(null, []);
        }

        FieldError? recusa = RegrasDaDistribuicaoDeVagas.RecusaDoNumeroDeOpcoes(declarado);

        return recusa is null
            ? new(declarado, [])
            : new(null, [ParteRecusada.DoBloco(Caminho, recusa.NoCaminhoDoItem(Caminho))]);
    }
}
