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
/// de uma versão anterior à divulgada: a linha recusa retroceder.
/// </para>
/// <para>
/// A recusa de mérito não chega aqui: ela é terminal e fica na fila morta. O efeito é exatamente o
/// pretendido — a linha não avança, e o certame permanece no ar com o conteúdo da versão anterior.
/// </para>
/// <para>
/// <b>É também o único caminho de escrita no acervo público (ADR-0132).</b> Os modelos de documento
/// que o edital congelou são copiados para lá antes de a divulgação ser gravada, porque é aqui que o
/// registro do ato está confirmado: nada fica público antes do ato, e o link só é divulgado depois de
/// o arquivo estar no acervo. Falha na cópia lança, a divulgação não avança, e a reentrega repete —
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
        IModeloDeDocumentoRepository modeloDeDocumentoRepository,
        IAcervoPublico acervoPublico,
        IEnderecoNoAcervoPublico enderecoNoAcervo,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(modeloDeDocumentoRepository);
        ArgumentNullException.ThrowIfNull(acervoPublico);
        ArgumentNullException.ThrowIfNull(enderecoNoAcervo);
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
            versao.ProcessoSeletivoId, versao.AtoCriadorId, nome, versao.HashConfiguracao, envelope, enderecoNoAcervo.De);

        if (projecao.IsFailure)
        {
            throw new InvalidOperationException(
                $"Configuração congelada do processo {versao.ProcessoSeletivoId} não tem a forma esperada: {projecao.Error!.Message}");
        }

        // Antes de ler a linha de divulgação para mutação: a reentrega que a cópia falhada provoca
        // começa sem nada rastreado, e a cópia já feita é encontrada no acervo e não se repete.
        await PublicarModelosNoAcervoAsync(versao, envelope, modeloDeDocumentoRepository, acervoPublico, cancellationToken)
            .ConfigureAwait(false);

        CertamePublicadoDto certame = projecao.Value!;
        string documento = JsonSerializer.Serialize(certame, ProjecaoDoCertamePublicado.OpcoesDoDocumento);
        DateTimeOffset agora = timeProvider.GetUtcNow();

        // Da MESMA projeção que produziu o documento: é o que impede consulta e resposta divergirem.
        FacetasDoCertameDivulgado facetas = new(
            certame.IdentificadorLegivel,
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

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception falha) when (IdentificadorLegivelJaDivulgadoException.EhViolacaoDoIndice(falha))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();

            CertameDivulgado? dono = await certameDivulgadoRepository
                .ObterParaLeituraPorIdentificadorAsync(certame.IdentificadorLegivel, cancellationToken)
                .ConfigureAwait(false);

            // Linha do próprio processo (entregas de versões dele correndo juntas) ou já removida:
            // é corrida, que a reentrega resolve — a falha original segue para a política transiente.
            if (IdentificadorLegivelJaDivulgadoException.Classificar(
                falha, versao.ProcessoSeletivoId, dono?.Id, certame.IdentificadorLegivel) is not { } conflito)
            {
                throw;
            }

            throw conflito;
        }
    }

    /// <summary>
    /// Copia para o acervo público cada modelo de documento que a versão congelou, na chave do ato
    /// que a criou.
    /// </summary>
    /// <remarks>
    /// O que se publica vem do envelope — nome, formato e hash são os do edital —, e do cadastro só se
    /// lê onde está a cópia selada privada. Modelo sem cópia selada, ou com outro conteúdo, é dado
    /// inconsistente: divulgar o link de um arquivo que não é o congelado seria pior que não divulgar.
    /// </remarks>
    private static async Task PublicarModelosNoAcervoAsync(
        VersaoConfiguracao versao,
        JsonObject envelope,
        IModeloDeDocumentoRepository modeloDeDocumentoRepository,
        IAcervoPublico acervoPublico,
        CancellationToken cancellationToken)
    {
        if (!ProjecaoDoCertamePublicado.TentarModelosCongelados(envelope, out List<ModeloDaExigencia>? modelos))
        {
            throw new InvalidOperationException(
                $"Configuração congelada do processo {versao.ProcessoSeletivoId} não tem a forma esperada nos modelos de documento.");
        }

        if (modelos.Count == 0)
        {
            return;
        }

        IReadOnlyList<ModeloDeDocumento> cadastrados = await modeloDeDocumentoRepository
            .ListarDoProcessoAsync(versao.ProcessoSeletivoId, [.. modelos.Select(static m => m.ModeloId)], cancellationToken)
            .ConfigureAwait(false);

        foreach (ModeloDaExigencia modelo in modelos)
        {
            ModeloDeDocumento? cadastrado = cadastrados.FirstOrDefault(m => m.Id == modelo.ModeloId);
            if (cadastrado?.ObjectKeyConfirmado is not { } chavePrivada
                || !string.Equals(cadastrado.HashSha256, modelo.HashSha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"O modelo de documento {modelo.ModeloId}, congelado na versão {versao.NumeroVersao} do processo {versao.ProcessoSeletivoId}, não tem cópia selada com o conteúdo congelado.");
            }

            await acervoPublico
                .CopiarAsync(
                    chavePrivada,
                    ChaveNoAcervoPublico.DoModelo(versao.ProcessoSeletivoId, versao.AtoCriadorId, modelo),
                    new DocumentoNoAcervo(
                        ModeloDeDocumento.ContentTypeDe(modelo.Formato),
                        modelo.NomeArquivo,
                        versao.AtoCriadorId,
                        versao.ProcessoSeletivoId,
                        modelo.HashSha256),
                    cancellationToken)
                .ConfigureAwait(false);
        }
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
