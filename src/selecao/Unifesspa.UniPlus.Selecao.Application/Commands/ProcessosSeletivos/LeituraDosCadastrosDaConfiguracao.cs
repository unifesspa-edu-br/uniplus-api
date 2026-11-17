namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.MotivosDeDecisao;
using Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Domain.ValueObjects;

using Services;

/// <summary>
/// O que a conferência da configuração lê dos cadastros vivos, uma vez e como valor, no mesmo instante da operação: os tipos
/// de ato únicos, o calendário vigente, os corretores das regras de consolidação e os motivos escolhidos com a situação de
/// cada um no cadastro. O catálogo e a proteção já vêm lidos e recopiados.
/// </summary>
public sealed class LeituraDosCadastrosDaConfiguracao(
    ResolucaoDosTiposDeAtoUnicos tiposDeAto,
    LeituraDoContextoDeContagemDePrazos contextos,
    LeituraDosCorretoresDasEtapas corretores,
    IMotivoDoProcessoRepository motivos)
{
    // O tipo que a versão vigente já congelou mantém a classificação dela: mudar o catálogo depois não alcança o processo já
    // publicado, nem pela retificação. O catálogo e o calendário valem no dia em que a operação publica.
    internal async Task<CadastrosDaConfiguracao> LerAsync(
        ProcessoSeletivo processo,
        AtoEDocumentoConferidos ato,
        OrigemDoCongelamento origem,
        IReadOnlyDictionary<string, TipoDeAtoCongelado> jaCongelados,
        CatalogoERecopia vivo,
        CancellationToken cancellationToken)
    {
        TiposDeAtoUnicosPorObjeto tipos = await tiposDeAto
            .ParaOperacaoAsync(processo, ato.TipoDoAto, ato.AtosDaLinhagem, ato.Publicacao.Dia, jaCongelados, cancellationToken)
            .ConfigureAwait(false);
        ContextoDeContagemDePrazos contexto = await contextos.LerAsync(cancellationToken).ConfigureAwait(false);
        CorretoresDasEtapas corretoresDasEtapas = await corretores.LerAsync(processo, cancellationToken).ConfigureAwait(false);

        return new CadastrosDaConfiguracao(
            contexto,
            vivo.Catalogo.FatosDeModalidade,
            tipos,
            corretoresDasEtapas,
            vivo.Recopiado.Protecao.Exigencias,
            await LerOsMotivosEscolhidosAsync(processo.Id, origem.NumeroDaVersaoQueNasce, cancellationToken).ConfigureAwait(false),
            vivo.Recopiado.Publicadas);
    }

    private async Task<MotivosEscolhidos> LerOsMotivosEscolhidosAsync(Guid processoId, int numeroDaVersaoQueNasce, CancellationToken cancellationToken) =>
        new(await motivos.ListarEscolhaAtualComOrigemAsync(processoId, cancellationToken).ConfigureAwait(false), numeroDaVersaoQueNasce);
}
