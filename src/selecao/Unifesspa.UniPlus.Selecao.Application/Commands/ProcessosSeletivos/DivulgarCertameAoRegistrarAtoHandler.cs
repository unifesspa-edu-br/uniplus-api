namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

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
/// de uma versão anterior à divulgada: a linha recusa retroceder, mas registra o edital que o ato
/// atrasado divulgou — a cópia dele já está no acervo, e a linha do tempo pública precisa do endereço.
/// </para>
/// <para>
/// A recusa de mérito não chega aqui: ela é terminal e fica na fila morta. O efeito é exatamente o
/// pretendido — a linha não avança, e o certame permanece no ar com o conteúdo da versão anterior.
/// </para>
/// <para>
/// <b>É também o único caminho de escrita no acervo público (ADR-0132).</b> O documento do edital, os
/// anexos e os modelos de documento que ele congelou são copiados para lá antes de a divulgação ser gravada,
/// porque é aqui que o registro do ato está confirmado: nada fica público antes do ato, e o link só é
/// divulgado depois de o arquivo estar no acervo. Falha na cópia lança, a divulgação não avança, e a reentrega repete —
/// encontrando no acervo o que já tinha sido copiado.
/// </para>
/// </remarks>
public static class DivulgarCertameAoRegistrarAtoHandler
{
    public static async Task Handle(
        AtoNormativoRegistrado mensagem,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IModeloDeDocumentoRepository modeloDeDocumentoRepository,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        IAcervoPublico acervoPublico,
        IEnderecoNoAcervoPublico enderecoNoAcervo,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);

        AcervoDaDivulgacao acervo = new(documentoAPublicarRepository, modeloDeDocumentoRepository, acervoPublico, enderecoNoAcervo);
        GravacaoDaDivulgacao gravacao = new(certameDivulgadoRepository, unitOfWork, timeProvider);

        IReadOnlyList<VersaoConfiguracao> versoes = await processoSeletivoRepository
            .ObterVersoesPorAtoCriadorAsync([mensagem.AtoId], cancellationToken)
            .ConfigureAwait(false);

        // Ato que não cria versão: se é do processo, o registro do ato diz quais documentos divulgar;
        // se não há registro, o ato é de outro domínio — o registro central serve a mais de um — e
        // não há o que divulgar.
        if (versoes.Count > 0)
        {
            await DivulgarVersaoAsync(
                mensagem, versoes[0], processoSeletivoRepository, registroCodecs, acervo, gravacao, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await DivulgarAtoDoProcessoNoAcervo
            .DivulgarAsync(
                mensagem,
                atoDoProcessoRepository,
                acervo,
                gravacao,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task DivulgarVersaoAsync(
        AtoNormativoRegistrado mensagem,
        VersaoConfiguracao versao,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        AcervoDaDivulgacao acervo,
        GravacaoDaDivulgacao gravacao,
        CancellationToken cancellationToken)
    {
        JsonObject envelope = LerEnvelopeOuFalhar(versao, registroCodecs);
        AtoDaDivulgacao ato = new(versao.AtoCriadorId, mensagem.Ano, mensagem.TipoCodigo);
        CertamePublicadoDto certame = await ProjetarAsync(versao, ato, envelope, processoSeletivoRepository, acervo.EnderecoDe, cancellationToken)
            .ConfigureAwait(false);

        // Antes de ler a linha de divulgação para mutação: a reentrega que a cópia falhada provoca
        // começa sem nada rastreado, e a cópia já feita é encontrada no acervo e não se repete.
        // Registrado só depois da cópia: o endereço divulgado nunca aponta para arquivo ausente.
        EditalDivulgado edital = await acervo
            .PublicarVersaoAsync(versao, ato, envelope, certame.DocumentoEdital.UrlDownload, cancellationToken)
            .ConfigureAwait(false);

        await gravacao.RegistrarAsync(versao, certame, edital, cancellationToken).ConfigureAwait(false);
    }

    private static JsonObject LerEnvelopeOuFalhar(VersaoConfiguracao versao, IRegistroCodecsEnvelope registroCodecs)
    {
        if (registroCodecs.SabeLer(versao.SchemaVersion) && TentarLerEnvelope(versao.ConfiguracaoCongelada, out JsonObject? envelope))
        {
            return envelope;
        }

        // O documento congelado existe mas não é legível por ESTE processo. Divulgar meia projeção seria
        // pior que não divulgar — a ausência é visível e reprojetável, a projeção parcial mente.
        //
        // O tipo próprio é o que permite à política de reentrega separar este caso da falha transiente:
        // aqui a causa esperada é a janela de um deploy em fases, que dura minutos, e não os segundos
        // de um blip.
        throw new EnvelopeAindaNaoLegivelException(
            $"Configuração congelada do processo {versao.ProcessoSeletivoId} não pôde ser lida para divulgação (schema {versao.SchemaVersion}).");
    }

    private static async Task<CertamePublicadoDto> ProjetarAsync(
        VersaoConfiguracao versao,
        AtoDaDivulgacao ato,
        JsonObject envelope,
        IProcessoSeletivoRepository processoSeletivoRepository,
        Func<string, Uri> enderecoNoAcervo,
        CancellationToken cancellationToken)
    {
        // O título é atributo do processo, não da configuração congelada: lê-lo aqui é o que o congela
        // contra edição sob retificação sem publicidade.
        string nome = await processoSeletivoRepository
            .ObterNomeAsync(versao.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Processo {versao.ProcessoSeletivoId} não encontrado para divulgar o certame.");

        Result<CertamePublicadoDto> projecao = ProjecaoDoCertamePublicado.Projetar(
            versao.ProcessoSeletivoId, ato, nome, versao.HashConfiguracao, envelope, enderecoNoAcervo);

        return projecao.IsFailure
            ? throw new InvalidOperationException(
                $"Configuração congelada do processo {versao.ProcessoSeletivoId} não tem a forma esperada: {projecao.Error!.Message}")
            : projecao.Value!;
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
