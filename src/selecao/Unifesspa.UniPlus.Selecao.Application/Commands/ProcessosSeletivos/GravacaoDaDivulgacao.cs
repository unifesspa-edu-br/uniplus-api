namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Monta no rastreador a divulgação do certame que a publicação cria: a linha na primeira versão, o avanço nas
/// seguintes e a entrada do ato, ainda sem endereço. Quem grava é o comando que publica, na mesma gravação da versão e
/// do ato.
/// </summary>
public sealed class GravacaoDaDivulgacao(ICertameDivulgadoRepository certames, IEnderecoNoAcervoPublico endereco)
{
    /// <summary>
    /// A divulgação vale o instante da versão (<see cref="VersaoConfiguracao.VigenteAPartirDe"/>), o mesmo do ato. A
    /// projeção que não se deixa ler recusa a publicação com <see cref="ProjecaoDoCertamePublicado.CodigoEnvelopeInesperado"/>.
    /// </summary>
    internal async Task<Result> MontarDaVersaoAsync(
        ProcessoSeletivo processo, VersaoConfiguracao versao, AtoDoProcesso ato, CancellationToken cancellationToken)
    {
        Result<CertamePublicadoDto> projecao = Projetar(processo, versao, ato);
        if (projecao.IsFailure)
        {
            return Result.Failure(projecao.Error!);
        }

        CertamePublicadoDto certame = projecao.Value!;
        string documento = JsonSerializer.Serialize(certame, ProjecaoDoCertamePublicado.OpcoesDoDocumento);

        // Da MESMA projeção que produziu o documento: é o que impede consulta e resposta divergirem.
        FacetasDoCertameDivulgado facetas = new(
            certame.IdentificadorLegivel,
            certame.Nome,
            certame.Periodo.Numero,
            certame.ModalidadesOfertadas,
            certame.Periodo.Inicio,
            certame.Periodo.Fim);

        CertameDivulgado? divulgado = await certames.ObterParaMutacaoAsync(processo.Id, cancellationToken).ConfigureAwait(false);
        if (divulgado is null)
        {
            divulgado = CertameDivulgado.Criar(
                processo.Id, versao.NumeroVersao, versao.AtoCriadorId, versao.HashConfiguracao,
                ProjecaoDoCertamePublicado.Versao, facetas, documento, versao.VigenteAPartirDe);
            await certames.AdicionarAsync(divulgado, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            divulgado.Avancar(
                versao.NumeroVersao, versao.AtoCriadorId, versao.HashConfiguracao,
                ProjecaoDoCertamePublicado.Versao, facetas, documento, versao.VigenteAPartirDe);
        }

        divulgado.RegistrarEditalPendente(ato.Id, versao.NumeroVersao);
        return Result.Success();
    }

    private Result<CertamePublicadoDto> Projetar(ProcessoSeletivo processo, VersaoConfiguracao versao, AtoDoProcesso ato) =>
        EnvelopeCongelado.TentarLer(versao.ConfiguracaoCongelada, out JsonObject? envelope)
            ? ProjecaoDoCertamePublicado.Projetar(
                processo.Id, AtoDaDivulgacao.De(ato), processo.Nome, versao.HashConfiguracao, envelope, endereco.De)
            : ProjecaoDoCertamePublicado.Recusar<CertamePublicadoDto>("configuração congelada");
}
