namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
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
/// É aqui que o certame passa a existir para o público: a publicação congela a configuração, mas a
/// publicidade depende de o ato existir, e isso só se sabe no consumo.
/// </para>
/// <para>
/// <b>Idempotente por desenho.</b> A reentrega é esperada, e uma entrega atrasada pode trazer o ato
/// de uma versão anterior à divulgada: a linha recusa retroceder.
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
            || !TentarLerEnvelope(versao.ConfiguracaoCongelada, out JsonObject? envelope))
        {
            // O documento congelado existe mas não é legível por ESTE processo. Divulgar meia
            // projeção seria pior que não divulgar — a ausência é visível e reprojetável, a
            // projeção parcial mente.
            //
            // O tipo próprio é o que permite à política de reentrega separar este caso da falha
            // transiente: aqui a causa esperada é a janela de um deploy em fases, que dura minutos,
            // e não os segundos de um blip.
            throw new EnvelopeAindaNaoLegivelException(
                $"Configuração congelada do processo {versao.ProcessoSeletivoId} não pôde ser lida para divulgação (schema {versao.SchemaVersion}).");
        }

        // O título é atributo do processo, não da configuração congelada: lê-lo aqui é o que o
        // congela contra edição sob retificação sem publicidade.
        string? nome = await processoSeletivoRepository
            .ObterNomeAsync(versao.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        if (nome is null)
        {
            throw new InvalidOperationException(
                $"Processo {versao.ProcessoSeletivoId} não encontrado para divulgar o certame.");
        }

        Result<CertamePublicadoDto> projecao = ProjecaoDoCertamePublicado.Projetar(
            versao.ProcessoSeletivoId, versao.AtoCriadorId, nome, versao.HashConfiguracao, envelope);

        if (projecao.IsFailure)
        {
            throw new InvalidOperationException(
                $"Configuração congelada do processo {versao.ProcessoSeletivoId} não tem a forma esperada: {projecao.Error!.Message}");
        }

        CertamePublicadoDto certame = projecao.Value!;
        string documento = JsonSerializer.Serialize(certame, ProjecaoDoCertamePublicado.OpcoesDoDocumento);
        DateTimeOffset agora = timeProvider.GetUtcNow();

        // Da MESMA projeção que produziu o documento: é o que impede consulta e resposta divergirem.
        FacetasDoCertameDivulgado facetas = new(
            certame.Nome,
            certame.Periodo.Numero,
            certame.ModalidadesOfertadas,
            certame.Periodo.Inicio,
            certame.Periodo.Fim);

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
                        facetas,
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
            facetas,
            documento,
            agora))
        {
            // Reentrega, ou entrega atrasada do ato de uma versão anterior: nada a fazer.
            return;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lê o documento congelado como objeto JSON, sem deixar passar exceção de análise.
    /// </summary>
    /// <remarks>
    /// Texto que não fecha como JSON faz <c>JsonNode.Parse</c> LANÇAR, e a exceção crua escaparia
    /// antes da recusa nomeada logo abaixo — a fila morta receberia um <c>JsonException</c> sem o
    /// identificador do processo, que é a única pista que alguém teria para investigar.
    /// </remarks>
    private static bool TentarLerEnvelope(string congelado, [NotNullWhen(true)] out JsonObject? envelope)
    {
        try
        {
            envelope = JsonNode.Parse(congelado) as JsonObject;
        }
        catch (JsonException)
        {
            envelope = null;
        }

        return envelope is not null;
    }
}
