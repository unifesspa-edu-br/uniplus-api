namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.TestSupport;

using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

internal static class MontagemDeTeste
{
    public static MontagemDoAtoNormativo Criar(ITipoAtoPublicadoRepository tipos, IAtoNormativoRepository atos) =>
        new(tipos, new CadeiaDeRetificacaoDoAto(atos), new ReservaDasVagasDoObjeto(atos));
}
