namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;

/// <summary>
/// Versões pendentes criadas no mesmo instante saem na fila sempre na mesma ordem, a do identificador do perfil, qualquer
/// que seja a ordem em que o banco devolve os perfis.
/// </summary>
public sealed class FilaDeVersoesNoMesmoInstanteTests
{
    [Fact(DisplayName = "As versões criadas no mesmo instante saem na fila na ordem do identificador do perfil")]
    public async Task MesmoInstante_OrdemDoIdentificadorDoPerfil()
    {
        Perfil[] perfis = [PendenteVerificada(["selecao:processos:consultar"]), PendenteVerificada(["selecao:processos:operar"]), PendenteVerificada(["selecao:processos:analisar"])];
        Perfil[] devolvidosNaOrdemInversa = [.. perfis.OrderByDescending(p => p.Id)];
        CenarioDaGovernancaDaVersao cenario = new(
            devolvidosNaOrdemInversa[0], PerfisDeTeste.Gestor, [Gestao], outrosPerfis: devolvidosNaOrdemInversa[1..]);
        FilaDeVersoesDaEtapa fila = new(cenario.Perfis, new CatalogoDaAplicacao([]), new RelogioFixo(PerfisDeTeste.Instante.AddHours(1)));

        IReadOnlyList<ItemDaFilaDeVersoesDto> itens = await fila.MontarAsync(EtapaDaVersao.GestaoDeAcessos, PerfisDeTeste.Gestor, CancellationToken.None);

        itens.Select(i => i.PerfilId).Should().Equal(perfis.Select(p => p.Id).Order());
    }
}
