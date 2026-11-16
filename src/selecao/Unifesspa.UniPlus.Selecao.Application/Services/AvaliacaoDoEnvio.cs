namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;
using DTOs.Comprovantes;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Avalia o envio: o endereço pelo CEP, antes da avaliação, porque os derivados de residência dependem dele; a avaliação da
/// definição inteira da versão fixada (a mesma do rascunho e da consulta do candidato) com as etapas da inscrição concluídas, porque
/// o envio é o candidato dizendo que terminou; e os municípios dos campos que a avaliação deu como aplicáveis. O Geo fora do ar deixa
/// os derivados de residência indeterminados e a avaliação segue.
/// </summary>
public sealed class AvaliacaoDoEnvio(
    IConferenciaEnderecoGeo geo,
    IResolvedorFusoInstitucional fuso,
    IDocumentoDaInscricaoRepository documentos)
{
    public async Task<Result<AvaliadoDoEnvio>> AvaliarAsync(PedidoDoEnvio pedido, DateTimeOffset instante, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        ConferenciaDosEnderecos doEndereco = await ConferenciaDoEnderecoDaInscricao
            .ConferirAsync(pedido.Respostas.Conferiveis, pedido.Contexto.Admitidas, geo, aoEnviar: true, cancellationToken).ConfigureAwait(false);
        Result<TimeZoneInfo> fusoInstitucional = fuso.Resolver();
        return fusoInstitucional.IsFailure
            ? Result<AvaliadoDoEnvio>.ValidationFailure(fusoInstitucional.Errors)
            : await AvaliarComOFusoAsync(pedido, doEndereco, new EnvioDasRespostas(instante, fusoInstitucional.Value!), cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AvaliadoDoEnvio>> AvaliarComOFusoAsync(
        PedidoDoEnvio pedido, ConferenciaDosEnderecos doEndereco, EnvioDasRespostas envio, CancellationToken cancellationToken)
    {
        DerivadosCalculados derivados = DerivadosDoSistemaInscricao.Calcular(pedido.Respostas.Mescladas, pedido.Contexto.DataReferenciaFatos, doEndereco);
        ArquivosDaInscricao arquivos = await ArquivosDaInscricao.CarregarAsync(documentos, pedido.Inscricao.Id, cancellationToken).ConfigureAwait(false);
        Result<ResultadoDaAvaliacaoDaInscricao> avaliacao = AvaliacaoDaInscricaoPorOpcao.Avaliar(pedido.Contexto, EntradaDaAvaliacaoDaInscricao.Montar(
            DerivadosDoSistemaInscricao.ComOsDerivados(pedido.Contexto.Admitidas.Definicoes),
            pedido.Respostas.Mescladas,
            InscricaoRequestMapper.ParaAvaliacao(pedido.Respostas.GruposMesclados),
            FatosConhecidos(derivados),
            AvaliacaoDaInscricao.CodigosDasEtapasDaInscricao(pedido.Contexto.Configuracao.Definicao),
            envio,
            arquivos.ApresentacoesVerificadas(),
            naFaseDaInscricao: true));
        return avaliacao.IsFailure
            ? Result<AvaliadoDoEnvio>.ValidationFailure(avaliacao.Errors)
            : Result<AvaliadoDoEnvio>.Success(await ComOsMunicipiosAsync(pedido, doEndereco, derivados, envio, arquivos, avaliacao.Value!, cancellationToken).ConfigureAwait(false));
    }

    private async Task<AvaliadoDoEnvio> ComOsMunicipiosAsync(
        PedidoDoEnvio pedido,
        ConferenciaDosEnderecos doEndereco,
        DerivadosCalculados derivados,
        EnvioDasRespostas envio,
        ArquivosDaInscricao arquivos,
        ResultadoDaAvaliacaoDaInscricao avaliacao,
        CancellationToken cancellationToken)
    {
        ResultadoDaConferenciaDosMunicipios dosMunicipios = await ConferenciaDosMunicipiosDaInscricao.ConferirAsync(
            pedido.Contexto.Configuracao.Definicao, pedido.Contexto.Admitidas, avaliacao.Formulario, pedido.Respostas.Mescladas,
            pedido.Respostas.GruposMesclados, pedido.Respostas.CamposRecusados, geo, cancellationToken).ConfigureAwait(false);
        return new AvaliadoDoEnvio(doEndereco, derivados, envio, arquivos, avaliacao, dosMunicipios);
    }

    private static Dictionary<string, FatoResolvido> FatosConhecidos(DerivadosCalculados derivados)
    {
        Dictionary<string, FatoResolvido> fatos = derivados.Valores.ToDictionary(static d => d.Key, static d => FatoResolvido.Resolvido(d.Value), StringComparer.Ordinal);
        foreach (string indeterminado in derivados.Indeterminados)
        {
            fatos[indeterminado] = FatoResolvido.Indeterminado();
        }

        return fatos;
    }
}
