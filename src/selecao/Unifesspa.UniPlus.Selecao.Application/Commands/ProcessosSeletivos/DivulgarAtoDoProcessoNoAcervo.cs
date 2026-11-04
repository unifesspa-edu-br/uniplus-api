namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Divulga os documentos de um ato do processo que não cria versão de configuração: aviso, errata,
/// resultado declarado no edital e semelhantes.
/// </summary>
/// <remarks>
/// <para>
/// A fonte é o registro do ato do processo, gravado pela Seleção junto do envio do ato: a mensagem de
/// confirmação leva só o ato, o ano e o tipo, e não diz de qual processo nem com quais arquivos. Ato
/// sem registro é de outro domínio e não tem o que divulgar aqui.
/// </para>
/// <para>
/// Roda na mesma condição do edital (ADR-0132, emenda 1): depois de o ato estar registrado, copiando
/// o principal e os anexos ao acervo antes de gravar os endereços na divulgação do certame. Falha na
/// cópia lança, a divulgação não avança, e a reentrega repete encontrando no acervo o que já foi
/// copiado.
/// </para>
/// <para>
/// A unidade e o identificador do processo na chave do principal são os gravados no registro do ato no
/// momento da publicação: a chave é parte do endereço divulgado e não pode mudar depois.
/// </para>
/// </remarks>
internal static class DivulgarAtoDoProcessoNoAcervo
{
    public static async Task DivulgarAsync(
        AtoNormativoRegistrado mensagem,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        AcervoDaDivulgacao acervo,
        GravacaoDaDivulgacao gravacao,
        CancellationToken cancellationToken)
    {
        AtoDoProcesso? ato = await atoDoProcessoRepository
            .ObterComAnexosAsync(mensagem.AtoId, cancellationToken)
            .ConfigureAwait(false);

        if (ato is null)
        {
            return;
        }

        await gravacao.ExigirDivulgacaoAsync(ato, cancellationToken).ConfigureAwait(false);
        EditalDivulgado edital = await acervo.PublicarAtoAsync(mensagem, ato, cancellationToken).ConfigureAwait(false);
        await gravacao.RegistrarEditalDoAtoAsync(ato, edital, cancellationToken).ConfigureAwait(false);
    }
}
