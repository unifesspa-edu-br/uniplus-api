namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

/// <summary>
/// A prova presencial do documento, lida como declaração do domínio com o limite de municípios configurado e conferida no Geo, uma
/// só vez por salvamento. O Geo só é consultado quando a leitura aceitou a declaração: a declaração recusada na forma não tem
/// município confiável para consultar, e a lista acima do limite é recusada na leitura, sem nenhuma consulta.
/// </summary>
public sealed class ConferenciaDaProvaPresencialDoDocumento(ConferenciaDosMunicipiosNoGeo municipios, IOptions<ProvaPresencialOptions> options)
{
    public async Task<ProvaPresencialConferida> ConferirAsync(ProvaPresencialNoDocumento? bloco, CancellationToken cancellationToken)
    {
        ProvaPresencialLida lida = ProvaPresencialLida.Ler(bloco, options.Value.LimiteDeMunicipios);
        Result<MunicipiosConferidosNoGeo> noGeo = await municipios.ConferirAsync(lida.Declaracao, cancellationToken).ConfigureAwait(false);

        return new ProvaPresencialConferida(lida, noGeo);
    }
}
