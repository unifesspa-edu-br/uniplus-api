namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A cópia do catálogo que cada item recebe: só a apresentação, para o fato que uma versão publicada já coletou, ou a cópia inteira.</summary>
internal sealed record PlanoDaRecopiaDoCatalogo(IReadOnlyList<(FatoColetado Item, CopiaDoItemNoCatalogo Copia, bool SoApresentacao)> Itens)
{
    internal void Aplicar()
    {
        foreach ((FatoColetado item, CopiaDoItemNoCatalogo copia, bool soApresentacao) in Itens)
        {
            if (soApresentacao)
            {
                item.RecopiarApresentacaoDoCatalogo(copia);
            }
            else
            {
                item.RecopiarDoCatalogo(copia);
            }
        }
    }
}
