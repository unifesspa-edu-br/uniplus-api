namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Os cadastros que a edição das etapas consulta. O tipo de banca e a regra do catálogo são lidos uma vez
/// por chave: duas etapas que requerem a mesma banca, ou que abrem janela recursal sob a mesma regra, são
/// o caso comum, e cada releitura aconteceria com o <c>SELECT ... FOR UPDATE</c> do processo na mão.
/// </summary>
internal sealed class CadastrosDasEtapas(
    ITipoEtapaReader tipoEtapaReader,
    ITipoBancaReader tipoBancaReader,
    IRegraCatalogoReader regraCatalogoReader)
{
    public ITipoEtapaReader TiposDeEtapa { get; } = tipoEtapaReader;

    public LeituraMemoizada<Guid, TipoBancaView> TiposDeBanca { get; } =
        new((tipoBancaId, cancellationToken) => tipoBancaReader.ObterPorIdAsync(tipoBancaId, cancellationToken));

    public LeituraMemoizada<(string Codigo, string Versao), RegraCatalogo> Regras { get; } =
        new((regra, cancellationToken) => regraCatalogoReader.ObterAsync(regra.Codigo, regra.Versao, cancellationToken));
}
