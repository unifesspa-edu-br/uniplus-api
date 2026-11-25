namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Cenarios;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal sealed class CriacaoDoProcessoSeletivoBuilder(Guid unidadeId)
{
    private string? _codigoIbge = "1504208";
    private string? _cidade = "Marabá";
    private string? _uf = "PA";
    private string? _identificadorLegivel = "psiq-2026";

    public CriacaoDoProcessoSeletivoBuilder ComLocalidade(string? codigoIbge, string? cidade, string? uf)
    {
        _codigoIbge = codigoIbge;
        _cidade = cidade;
        _uf = uf;
        return this;
    }

    public CriacaoDoProcessoSeletivoBuilder ComIdentificadorLegivel(string? identificadorLegivel)
    {
        _identificadorLegivel = identificadorLegivel;
        return this;
    }

    public CriarProcessoSeletivoCommand Construir() => new(
        "PS 2026 — SiSU", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria,
        unidadeId, _codigoIbge, _cidade, _uf, _identificadorLegivel);
}
