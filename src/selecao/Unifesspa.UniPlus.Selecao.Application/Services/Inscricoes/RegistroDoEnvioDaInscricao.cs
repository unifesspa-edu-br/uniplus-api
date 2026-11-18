namespace Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs.Comprovantes;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

/// <summary>Registra o envio e a evidência correspondente, sem confirmar a transação do chamador.</summary>
public sealed class RegistroDoEnvioDaInscricao(
    IInscricaoRepository inscricoes,
    IComprovanteDaInscricaoRepository comprovantes,
    IGeradorDeNumeroInscricao numeros,
    DocumentosEnviadosDaInscricao documentos)
{
    public async Task<Result<ReciboDaInscricaoDto>> RegistrarAsync(
        EnvioConferido envio, PrecondicaoIfMatch precondicao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envio);
        IReadOnlyList<ArquivoConfirmado> arquivos = envio.Arquivos.Verificados();
        string numero = await numeros.ProximoAsync(cancellationToken).ConfigureAwait(false);
        Inscricao inscricao = envio.Entrada.Pedido.Inscricao;
        int emitidos = (await comprovantes.ObterEmissoesAsync([inscricao.Id], cancellationToken).ConfigureAwait(false)).Count;
        Result<ComprovanteInscricao> submetido = Submeter(envio, numero, precondicao, arquivos);
        return submetido.IsFailure
            ? Result<ReciboDaInscricaoDto>.ValidationFailure(submetido.Errors)
            : await RegistrarEvidenciaAsync(envio, submetido.Value!, arquivos, emitidos + 1, cancellationToken).ConfigureAwait(false);
    }

    private static Result<ComprovanteInscricao> Submeter(
        EnvioConferido envio, string numero, PrecondicaoIfMatch precondicao, IReadOnlyList<ArquivoConfirmado> arquivos)
    {
        EnvioPreparado pedido = envio.Entrada.Pedido;
        return pedido.Inscricao.Submeter(numero,
            PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(pedido.Request.OfertaCursoId, pedido.Request.OfertaCursoSegundaOpcaoId), pedido.Recebido.Respostas, pedido.Recebido.Grupos, pedido.Recebido.Aceites), envio.Admitidas,
            new ApuracaoDoEnvio(envio.Avaliacao, envio.Entrada.Derivados.Valores, DerivadosDoSistemaInscricao.DefinicoesDasRespostas, arquivos, new MunicipiosConferidos(envio.Municipios.Nomes, envio.Entrada.Endereco.Cidades, envio.Entrada.Derivados.Nomes)), precondicao, pedido.Instante);
    }

    private async Task<Result<ReciboDaInscricaoDto>> RegistrarEvidenciaAsync(
        EnvioConferido envio, ComprovanteInscricao comprovante, IReadOnlyList<ArquivoConfirmado> arquivos,
        int numeroDoComprovante, CancellationToken cancellationToken)
    {
        EnvioPreparado pedido = envio.Entrada.Pedido;
        await documentos.ConciliarNoEnvioAsync(envio.Arquivos, envio.Avaliacao, arquivos, pedido.Instante, cancellationToken).ConfigureAwait(false);
        await inscricoes.AtualizarAsync(pedido.Inscricao, cancellationToken).ConfigureAwait(false);
        await comprovantes.AdicionarComprovanteAsync(comprovante, cancellationToken).ConfigureAwait(false);
        return Result<ReciboDaInscricaoDto>.Success(new ReciboDaInscricaoDto(
            pedido.Inscricao.Id, comprovante.NumeroInscricao, numeroDoComprovante, comprovante.Instante));
    }
}
