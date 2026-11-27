namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

/// <summary>
/// Projeção <c>RegraCatalogo</c> → <c>RegraCatalogoDto</c>.
/// </summary>
public static class RegraCatalogoMapping
{
    /// <summary>Projeta a regra usando o código textual de tipo aceito pelo filtro e usado no hash.</summary>
    public static RegraCatalogoDto ToDto(RegraCatalogo regra)
    {
        ArgumentNullException.ThrowIfNull(regra);
        return new RegraCatalogoDto(
            Codigo: regra.Codigo,
            Versao: regra.Versao,
            Tipo: CodigoComRotulo<TipoRegra>.De(regra.Tipo),
            DeclaracaoDaRegra: regra.DeclaracaoDaRegra,
            Invariantes: regra.Invariantes,
            BaseLegal: regra.BaseLegal,
            Hash: regra.Hash,
            ModalidadesAdmitidas: ModalidadesAdmitidasDaDeclaracaoDaRegra.Extrair(regra.DeclaracaoDaRegra),
            RamoDaDistribuicao: regra.Tipo == TipoRegra.RegraDistribuicaoVagas ? PoliticaDaDistribuicaoDeVagas.ModoDeMontagemDe(regra.Codigo) : null)
        {
            Rotulo = RotuloDaRegra.Obter(regra),
        };
    }
}
