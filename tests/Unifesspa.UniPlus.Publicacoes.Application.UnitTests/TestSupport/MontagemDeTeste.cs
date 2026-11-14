namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.TestSupport;

using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class MontagemDeTeste
{
    public static MontagemDoAtoNormativo Criar(ITipoAtoPublicadoRepository tipos, IAtoNormativoRepository atos) =>
        new(tipos, new ResolvedorFusoDeTeste(), new CadeiaDeRetificacaoDoAto(atos), new ReservaDasVagasDoObjeto(atos));
}
