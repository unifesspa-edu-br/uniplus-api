namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Materializa a divulgação pública do certame quando o ato normativo da sua versão se confirma no
/// registro central.
/// </summary>
/// <remarks>
/// <para>
/// É aqui que o certame passa a existir para o público. A publicação congela a configuração e
/// decide o identificador do ato na mesma transação, mas a publicidade depende do ato existir — e
/// isso só se sabe no consumo. Projetar neste ponto faz a leitura pública ser uma consulta de
/// tabela única, em vez de resolver linhagem e interpretar o documento congelado a cada requisição.
/// </para>
/// <para>
/// <b>Idempotente por desenho.</b> A reentrega é esperada: a mesma mensagem pode chegar duas vezes,
/// e uma entrega atrasada pode trazer o ato de uma versão ANTERIOR à que já está divulgada. A linha
/// recusa retroceder — uma retificação já divulgada não se desfaz por mensagem fora de ordem.
/// </para>
/// <para>
/// A recusa de mérito não chega aqui: ela é terminal e fica na fila morta. O efeito é exatamente o
/// pretendido — a linha não avança, e o certame permanece no ar com o conteúdo da versão anterior.
/// </para>
/// </remarks>
public static class DivulgarCertameAoRegistrarAtoHandler
{
    public static async Task Handle(
        AtoNormativoRegistrado mensagem,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        IReadOnlyList<VersaoConfiguracao> versoes = await processoSeletivoRepository
            .ObterVersoesPorAtoCriadorAsync([mensagem.AtoId], cancellationToken)
            .ConfigureAwait(false);

        // Ato que não corresponde a versão alguma de Seleção: o registro central serve outros
        // domínios, e um ato de outro objeto chega aqui sem ter o que divulgar.
        if (versoes.Count == 0)
        {
            return;
        }

        VersaoConfiguracao versao = versoes[0];

        if (!registroCodecs.SabeLer(versao.SchemaVersion)
            || JsonNode.Parse(versao.ConfiguracaoCongelada) is not JsonObject envelope)
        {
            // O documento congelado existe mas não é legível pelo codec vivo. Divulgar meia
            // projeção seria pior que não divulgar — a ausência é visível e reprojetável, a
            // projeção parcial mente. Lançar deixa o envelope na fila para nova tentativa e, se
            // persistir, na fila morta, onde alguém olha.
            throw new InvalidOperationException(
                $"Configuração congelada do processo {versao.ProcessoSeletivoId} não pôde ser lida para divulgação.");
        }

        Result<CertamePublicadoDto> projecao = ProjecaoDoCertamePublicado.Projetar(
            versao.ProcessoSeletivoId, versao.AtoCriadorId, versao.HashConfiguracao, envelope);

        if (projecao.IsFailure)
        {
            throw new InvalidOperationException(
                $"Configuração congelada do processo {versao.ProcessoSeletivoId} não tem a forma esperada: {projecao.Error!.Message}");
        }

        CertamePublicadoDto certame = projecao.Value!;
        string documento = JsonSerializer.Serialize(certame);
        DateTimeOffset agora = timeProvider.GetUtcNow();

        CertameDivulgado? divulgado = await certameDivulgadoRepository
            .ObterParaMutacaoAsync(versao.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        if (divulgado is null)
        {
            await certameDivulgadoRepository
                .AdicionarAsync(
                    CertameDivulgado.Criar(
                        versao.ProcessoSeletivoId,
                        versao.NumeroVersao,
                        versao.AtoCriadorId,
                        versao.HashConfiguracao,
                        ProjecaoDoCertamePublicado.Versao,
                        certame.Periodo.Inicio,
                        certame.Periodo.Fim,
                        documento,
                        agora),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (!divulgado.TentarAvancar(
            versao.NumeroVersao,
            versao.AtoCriadorId,
            versao.HashConfiguracao,
            ProjecaoDoCertamePublicado.Versao,
            certame.Periodo.Inicio,
            certame.Periodo.Fim,
            documento,
            agora))
        {
            // Reentrega, ou entrega atrasada do ato de uma versão anterior: nada a fazer.
            return;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
    }
}
